const config = window.__INSIGHTA_CONFIG__ ?? {};
const state = {
  apiBaseUrl: normalizeBaseUrl(config.apiBaseUrl),
  user: null,
  error: "",
  notice: "",
  currentPath: window.location.pathname
};

const routes = [
  { path: "/", title: "Dashboard", handler: renderDashboardPage, protected: true },
  { path: "/login", title: "Login", handler: renderLoginPage, protected: false },
  { path: "/dashboard", title: "Dashboard", handler: renderDashboardPage, protected: true },
  { path: "/profiles", title: "Profiles", handler: renderProfilesPage, protected: true },
  { path: "/profiles/:id", title: "Profile Detail", handler: renderProfileDetailPage, protected: true },
  { path: "/search", title: "Search", handler: renderSearchPage, protected: true },
  { path: "/account", title: "Account", handler: renderAccountPage, protected: true },
  { path: "/auth/callback", title: "Signing In", handler: renderAuthCallbackPage, protected: false }
];

document.addEventListener("click", onDocumentClick);
window.addEventListener("popstate", () => navigate(getCurrentBrowserPath(), { replace: true }));

boot().catch(handleFatalError);

async function boot()
{
  if (!state.apiBaseUrl)
  {
    throw new Error("Missing INSIGHTA_API_URL configuration.");
  }

  await tryHydrateSession();
  await navigate(getCurrentBrowserPath(), { replace: true });
}

async function navigate(path, { replace = false } = {})
{
  const route = resolveRoute(path);
  state.currentPath = path;
  state.error = "";

  if (!route)
  {
    renderNotFound();
    return;
  }

  if (route.protected && !state.user)
  {
    redirectTo("/login", replace);
    return;
  }

  document.title = `${route.title} | Insighta Labs+`;
  renderLoadingFrame();

  try
  {
    const content = await route.handler(route.params ?? {});
    renderShell(content, route.path);
    if (!replace && getCurrentBrowserPath() !== path)
    {
      window.history.pushState({}, "", path);
    }
    if (replace && getCurrentBrowserPath() !== path)
    {
      window.history.replaceState({}, "", path);
    }
  }
  catch (error)
  {
    if (error instanceof UnauthorizedError)
    {
      state.user = null;
      renderShell(renderLoginRequired(), "/login");
      redirectTo("/login", true);
      return;
    }

    handlePageError(error);
  }
}

function redirectTo(path, replace = false)
{
  if (replace)
  {
    window.history.replaceState({}, "", path);
  }
  else
  {
    window.history.pushState({}, "", path);
  }

  navigate(path, { replace: true }).catch(handleFatalError);
}

function onDocumentClick(event)
{
  const link = event.target.closest("[data-link]");
  if (link)
  {
    event.preventDefault();
    const href = link.getAttribute("href");
    if (href)
    {
      redirectTo(href);
    }
    return;
  }

  const action = event.target.closest("[data-action]");
  if (!action)
  {
    return;
  }

  event.preventDefault();
  const actionName = action.getAttribute("data-action");

  if (actionName === "login")
  {
    startLogin().catch(handlePageError);
  }
  else if (actionName === "logout")
  {
    logout().catch(handlePageError);
  }
  else if (actionName === "export")
  {
    exportProfilesFromCurrentForm(action.closest("form")).catch(handlePageError);
  }
}

async function renderLoginPage()
{
  if (state.user)
  {
    redirectTo("/dashboard", true);
    return "<div></div>";
  }

  return `
    <section class="page">
      <article class="split-card">
        <div>
          <span class="tag">Internal Platform</span>
          <h1 class="section-title">Secure access for analysts, engineers, and stakeholders.</h1>
          <p class="section-lead">Insighta Labs+ wraps the Profile Intelligence System in a real session layer with GitHub OAuth, HTTP-only cookies, CSRF protection, and role-aware access.</p>
          <div class="cta-row">
            <button class="button" data-action="login">Continue with GitHub</button>
          </div>
          ${state.error ? `<p class="status">${escapeHtml(state.error)}</p>` : ""}
        </div>
        <div class="accent-pane">
          <h2>What you can do</h2>
          <div class="metric-grid">
            <article class="metric"><span>Profiles</span><strong>Query</strong></article>
            <article class="metric"><span>Search</span><strong>Natural language</strong></article>
            <article class="metric"><span>Sessions</span><strong>Cookie-secured</strong></article>
            <article class="metric"><span>Roles</span><strong>Predictable</strong></article>
          </div>
          <p class="footer-note">The browser never reads your auth tokens directly. This portal relies on secure cookies issued by the backend.</p>
        </div>
      </article>
    </section>
  `;
}

async function renderAuthCallbackPage()
{
  const fragment = new URLSearchParams(window.location.hash.replace(/^#/, ""));
  const expectedState = sessionStorage.getItem("insighta_oauth_state");
  const returnedState = fragment.get("state");
  const error = fragment.get("error");
  const login = fragment.get("login");

  sessionStorage.removeItem("insighta_oauth_state");

  if (error)
  {
    state.error = error;
    redirectTo("/login", true);
    return "<div></div>";
  }

  if (!expectedState || returnedState !== expectedState || login !== "success")
  {
    state.error = "OAuth state validation failed.";
    redirectTo("/login", true);
    return "<div></div>";
  }

  await tryHydrateSession(true);
  redirectTo("/dashboard", true);
  return "<div></div>";
}

async function renderDashboardPage()
{
  const [total, male, female, adult] = await Promise.all([
    getProfilePage({ limit: "1" }),
    getProfilePage({ limit: "1", gender: "male" }),
    getProfilePage({ limit: "1", gender: "female" }),
    getProfilePage({ limit: "1", age_group: "adult" })
  ]);

  return `
    <section class="page">
      <article class="hero">
        <span class="tag ${escapeHtml(state.user.role)}">${escapeHtml(state.user.role)}</span>
        <h1>Welcome back, ${escapeHtml(state.user.username)}.</h1>
        <p>Everything here flows through the same backend rules as the CLI: versioned profile APIs, authenticated requests, and predictable role enforcement.</p>
        <div class="inline-actions">
          <a class="button" data-link href="/profiles">Browse profiles</a>
          <a class="button-secondary" data-link href="/search">Run search</a>
        </div>
      </article>

      <section class="metric-grid">
        <article class="metric">
          <span>Total profiles</span>
          <strong>${total.total}</strong>
        </article>
        <article class="metric">
          <span>Male profiles</span>
          <strong>${male.total}</strong>
        </article>
        <article class="metric">
          <span>Female profiles</span>
          <strong>${female.total}</strong>
        </article>
        <article class="metric">
          <span>Adults</span>
          <strong>${adult.total}</strong>
        </article>
      </section>

      <article class="panel">
        <h2>Operational notes</h2>
        <div class="stats-row">
          <span class="tag">Backend: ${escapeHtml(state.apiBaseUrl)}</span>
          <span class="tag">Access token rotation enabled</span>
          <span class="tag">Profile API version header enforced</span>
        </div>
      </article>
    </section>
  `;
}

async function renderProfilesPage()
{
  const params = new URLSearchParams(window.location.search);
  const filters = {
    gender: params.get("gender") ?? "",
    country: params.get("country_id") ?? "",
    ageGroup: params.get("age_group") ?? "",
    minAge: params.get("min_age") ?? "",
    maxAge: params.get("max_age") ?? "",
    sortBy: params.get("sort_by") ?? "created_at",
    order: params.get("order") ?? "desc",
    page: params.get("page") ?? "1",
    limit: params.get("limit") ?? "10"
  };

  const response = await getProfilePage({
    gender: filters.gender,
    country_id: filters.country,
    age_group: filters.ageGroup,
    min_age: filters.minAge,
    max_age: filters.maxAge,
    sort_by: filters.sortBy,
    order: filters.order,
    page: filters.page,
    limit: filters.limit
  });

  return `
    <section class="page">
      <article class="panel">
        <h1 class="section-title">Profiles</h1>
        <p class="section-lead">Filter, page, export, and inspect profiles without leaving the browser.</p>
        ${state.notice ? `<p class="status success">${escapeHtml(state.notice)}</p>` : ""}
        <form id="profiles-filter-form">
          <div class="filter-grid">
            ${textField("gender", "Gender", filters.gender, "male or female")}
            ${textField("country", "Country", filters.country, "NG, US, KE")}
            ${textField("age-group", "Age group", filters.ageGroup, "adult")}
            ${textField("min-age", "Min age", filters.minAge, "18")}
            ${textField("max-age", "Max age", filters.maxAge, "40")}
            ${selectField("sort-by", "Sort by", filters.sortBy, [["created_at", "Created at"], ["age", "Age"], ["gender_probability", "Gender probability"]])}
            ${selectField("order", "Order", filters.order, [["desc", "Descending"], ["asc", "Ascending"]])}
            ${selectField("limit", "Limit", filters.limit, [["10", "10"], ["20", "20"], ["50", "50"]])}
          </div>
          <div class="inline-actions" style="margin-top:16px;">
            <button class="button" type="submit">Apply filters</button>
            <button class="button-secondary" type="button" data-action="export">Export CSV</button>
            ${state.user.role === "admin" ? '<button class="button-secondary" type="button" id="toggle-create">Create profile</button>' : ""}
          </div>
        </form>
      </article>

      ${state.user.role === "admin" ? `
        <article class="panel" id="create-panel" hidden>
          <h2>Create profile</h2>
          <form id="create-profile-form" class="filter-grid">
            ${textField("create-name", "Name", "", "Harriet Tubman")}
          </form>
          <div class="inline-actions" style="margin-top:16px;">
            <button class="button" id="create-submit">Create</button>
          </div>
        </article>
      ` : ""}

      ${renderProfilesTable(response)}
    </section>
  `;
}

async function renderProfileDetailPage(params)
{
  const profile = await apiFetch(`/api/profiles/${encodeURIComponent(params.id)}`, { versioned: true });

  return `
    <section class="page">
      <article class="panel">
        <div class="inline-actions">
          <a class="ghost-link" data-link href="/profiles">Back to profiles</a>
        </div>
        <h1 class="section-title">${escapeHtml(profile.data.name)}</h1>
        <p class="section-lead">Profile detail from the shared backend contract.</p>
        <div class="detail-grid" style="margin-top:22px;">
          ${detailItem("ID", profile.data.id)}
          ${detailItem("Gender", profile.data.gender)}
          ${detailItem("Gender probability", Number(profile.data.gender_probability).toFixed(2))}
          ${detailItem("Age", String(profile.data.age))}
          ${detailItem("Age group", profile.data.age_group)}
          ${detailItem("Country ID", profile.data.country_id)}
          ${detailItem("Country name", profile.data.country_name)}
          ${detailItem("Country probability", Number(profile.data.country_probability).toFixed(2))}
          ${detailItem("Created at", profile.data.created_at)}
        </div>
      </article>
    </section>
  `;
}

async function renderSearchPage()
{
  const params = new URLSearchParams(window.location.search);
  const query = params.get("q") ?? "";
  const page = params.get("page") ?? "1";
  const limit = params.get("limit") ?? "10";

  let resultsMarkup = `
    <article class="empty">
      <h2>No search yet</h2>
      <p class="muted">Try a phrase like <em>young males from nigeria</em>.</p>
    </article>
  `;

  if (query)
  {
    const response = await apiFetch(`/api/profiles/search?q=${encodeURIComponent(query)}&page=${encodeURIComponent(page)}&limit=${encodeURIComponent(limit)}`, { versioned: true });
    resultsMarkup = renderProfilesTable(response, false, "/search");
  }

  return `
    <section class="page">
      <article class="panel">
        <h1 class="section-title">Natural-language search</h1>
        <p class="section-lead">This uses the same deterministic parser as the backend and CLI.</p>
        <form id="search-form">
          <div class="filter-grid">
            ${textField("search-query", "Query", query, "young males from nigeria")}
            ${selectField("search-limit", "Limit", limit, [["10", "10"], ["20", "20"], ["50", "50"]])}
          </div>
          <div class="inline-actions" style="margin-top:16px;">
            <button class="button" type="submit">Search</button>
          </div>
        </form>
      </article>
      ${resultsMarkup}
    </section>
  `;
}

async function renderAccountPage()
{
  return `
    <section class="page">
      <article class="panel">
        <h1 class="section-title">Account</h1>
        <p class="section-lead">Your current browser session is backed by HTTP-only cookies managed by the backend.</p>
        <div class="detail-grid" style="margin-top:20px;">
          ${detailItem("Username", state.user.username)}
          ${detailItem("GitHub ID", state.user.github_id)}
          ${detailItem("Email", state.user.email ?? "Not available")}
          ${detailItem("Role", state.user.role)}
          ${detailItem("Active", String(state.user.is_active))}
          ${detailItem("Last login", state.user.last_login_at)}
        </div>
        <div class="inline-actions" style="margin-top:18px;">
          <button class="button-secondary" data-action="logout">Logout</button>
        </div>
      </article>
    </section>
  `;
}

function renderProfilesTable(response, includeHeader = true, basePath = "/profiles")
{
  const rows = response.data ?? [];
  const header = includeHeader ? `
    <article class="panel">
      <h2>Results</h2>
      <p class="muted">Page ${response.page} of ${Math.max(response.total_pages, 1)} with ${response.total} total profiles.</p>
    </article>
  ` : "";

  if (rows.length === 0)
  {
    return `${header}<article class="empty"><h2>No profiles matched</h2><p class="muted">Adjust the filters and try again.</p></article>`;
  }

  return `
    ${header}
    <article class="table-card">
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Gender</th>
              <th>Age</th>
              <th>Age group</th>
              <th>Country</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            ${rows.map((profile) => `
              <tr>
                <td><a data-link href="/profiles/${encodeURIComponent(profile.id)}">${escapeHtml(profile.name)}</a></td>
                <td>${escapeHtml(profile.gender)}</td>
                <td>${escapeHtml(String(profile.age))}</td>
                <td>${escapeHtml(profile.age_group)}</td>
                <td>${escapeHtml(profile.country_name)} (${escapeHtml(profile.country_id)})</td>
                <td>${escapeHtml(formatDate(profile.created_at))}</td>
              </tr>
            `).join("")}
          </tbody>
        </table>
      </div>
    </article>
    <article class="panel">
      <div class="inline-actions">
        ${response.links.prev ? `<a class="ghost-link" data-link href="${buildRelativeLink(basePath, response.links.prev)}">Previous</a>` : ""}
        ${response.links.next ? `<a class="ghost-link" data-link href="${buildRelativeLink(basePath, response.links.next)}">Next</a>` : ""}
      </div>
    </article>
  `;
}

function renderShell(content, activeRoute)
{
  const app = document.getElementById("app");
  const nav = state.user ? `
    <header class="nav">
      <div class="brand">
        <div class="brand-mark">I+</div>
        <div class="brand-copy">
          <strong>Insighta Labs+</strong>
          <span>Secure profile intelligence</span>
        </div>
      </div>
      <nav class="nav-links">
        ${navLink("/dashboard", "Dashboard", activeRoute)}
        ${navLink("/profiles", "Profiles", activeRoute)}
        ${navLink("/search", "Search", activeRoute)}
        ${navLink("/account", "Account", activeRoute)}
      </nav>
      <div class="nav-user">
        <strong>@${escapeHtml(state.user.username)}</strong><br>
        <span>${escapeHtml(state.user.role)}</span>
      </div>
    </header>
  ` : "";

  app.innerHTML = `<div class="shell">${nav}${content}</div>`;
  wirePageBehaviors();
}

function renderLoadingFrame()
{
  const app = document.getElementById("app");
  app.innerHTML = `
    <div class="shell">
      <section class="page">
        <article class="panel">
          <h1 class="section-title">Loading...</h1>
          <p class="section-lead">Talking to the backend.</p>
        </article>
      </section>
    </div>
  `;
}

function renderNotFound()
{
  renderShell(`
    <section class="page">
      <article class="empty">
        <h1 class="section-title">Page not found</h1>
        <p class="muted">The route you requested does not exist.</p>
      </article>
    </section>
  `, "");
}

function renderLoginRequired()
{
  return `
    <section class="page">
      <article class="empty">
        <h1 class="section-title">Session required</h1>
        <p class="muted">Please sign in again to continue.</p>
      </article>
    </section>
  `;
}

function handlePageError(error)
{
  const message = error instanceof Error ? error.message : "Unknown error.";
  state.error = message;
  renderShell(`
    <section class="page">
      <article class="panel">
        <h1 class="section-title">Something went wrong</h1>
        <p class="status">${escapeHtml(message)}</p>
        <div class="inline-actions">
          <a class="button-secondary" data-link href="${state.user ? "/dashboard" : "/login"}">Go back</a>
        </div>
      </article>
    </section>
  `, state.currentPath);
}

function handleFatalError(error)
{
  handlePageError(error);
}

async function tryHydrateSession()
{
  try
  {
    state.user = await fetchCurrentUser(true);
  }
  catch
  {
    state.user = null;
  }
}

async function fetchCurrentUser(allowRefresh = true)
{
  const response = await apiFetch("/api/users/me", { versioned: false, allowRefresh });
  return response.data;
}

async function startLogin()
{
  const oauthState = crypto.randomUUID();
  sessionStorage.setItem("insighta_oauth_state", oauthState);
  const redirectUrl = `${window.location.origin}/auth/callback`;
  const location = `${state.apiBaseUrl}/auth/github?client_redirect_uri=${encodeURIComponent(redirectUrl)}&state=${encodeURIComponent(oauthState)}`;
  window.location.assign(location);
}

async function logout()
{
  await apiFetch("/auth/logout", {
    method: "POST",
    body: {},
    versioned: false,
    allowRefresh: false
  });

  state.user = null;
  state.notice = "";
  state.error = "";
  redirectTo("/login", true);
}

async function apiFetch(path, { method = "GET", body = null, versioned = false, allowRefresh = true } = {})
{
  const headers = { Accept: "application/json" };
  if (versioned)
  {
    headers["X-API-Version"] = "1";
  }

  const isMutation = !["GET", "HEAD"].includes(method.toUpperCase());
  if (isMutation)
  {
    headers["Content-Type"] = "application/json";
    const csrf = readCookie("insighta_csrf");
    if (csrf)
    {
      headers["X-CSRF-Token"] = csrf;
    }
  }

  const response = await fetch(`${state.apiBaseUrl}${path}`, {
    method,
    credentials: "include",
    headers,
    body: body ? JSON.stringify(body) : null
  });

  if (response.status === 401 && allowRefresh)
  {
    const refreshed = await refreshSession();
    if (refreshed)
    {
      return apiFetch(path, { method, body, versioned, allowRefresh: false });
    }
    throw new UnauthorizedError();
  }

  if (!response.ok)
  {
    throw new Error(await readErrorMessage(response));
  }

  return response.json();
}

async function refreshSession()
{
  const csrf = readCookie("insighta_csrf");
  const response = await fetch(`${state.apiBaseUrl}/auth/refresh`, {
    method: "POST",
    credentials: "include",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      ...(csrf ? { "X-CSRF-Token": csrf } : {})
    },
    body: JSON.stringify({})
  });

  return response.ok;
}

async function readErrorMessage(response)
{
  try
  {
    const payload = await response.json();
    return payload.message ?? `Request failed with status ${response.status}.`;
  }
  catch
  {
    return `Request failed with status ${response.status}.`;
  }
}

async function getProfilePage(query)
{
  const params = new URLSearchParams(
    Object.entries(query).filter(([, value]) => value !== undefined && value !== null && String(value).trim() !== "")
  );
  return apiFetch(`/api/profiles?${params.toString()}`, { versioned: true });
}

async function exportProfilesFromCurrentForm(form)
{
  if (!form)
  {
    return;
  }

  const params = collectProfileFormParams(form);
  params.set("format", "csv");
  const url = `${state.apiBaseUrl}/api/profiles/export?${params.toString()}`;
  const response = await fetch(url, {
    method: "GET",
    credentials: "include",
    headers: {
      "X-API-Version": "1"
    }
  });

  if (!response.ok)
  {
    throw new Error(await readErrorMessage(response));
  }

  const blob = await response.blob();
  const downloadUrl = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = downloadUrl;
  anchor.download = `profiles_${Date.now()}.csv`;
  anchor.click();
  URL.revokeObjectURL(downloadUrl);
}

function wirePageBehaviors()
{
  const profilesForm = document.getElementById("profiles-filter-form");
  if (profilesForm)
  {
    profilesForm.addEventListener("submit", (event) =>
    {
      event.preventDefault();
      const params = collectProfileFormParams(profilesForm);
      redirectTo(`/profiles?${params.toString()}`);
    });
  }

  const searchForm = document.getElementById("search-form");
  if (searchForm)
  {
    searchForm.addEventListener("submit", (event) =>
    {
      event.preventDefault();
      const query = document.getElementById("search-query").value.trim();
      const limit = document.getElementById("search-limit").value;
      const params = new URLSearchParams();
      if (query)
      {
        params.set("q", query);
      }
      params.set("limit", limit);
      redirectTo(`/search?${params.toString()}`);
    });
  }

  const toggleCreate = document.getElementById("toggle-create");
  const createPanel = document.getElementById("create-panel");
  if (toggleCreate && createPanel)
  {
    toggleCreate.addEventListener("click", () =>
    {
      createPanel.hidden = !createPanel.hidden;
    });
  }

  const createSubmit = document.getElementById("create-submit");
  if (createSubmit)
  {
    createSubmit.addEventListener("click", async () =>
    {
      const input = document.getElementById("create-name");
      const name = input.value.trim();
      if (!name)
      {
        alert("Please provide a name.");
        return;
      }

      const response = await apiFetch("/api/profiles", {
        method: "POST",
        body: { name },
        versioned: true
      });

      state.notice = `Saved profile for ${response.data.name}.`;
      redirectTo("/profiles", true);
    });
  }
}

function collectProfileFormParams(form)
{
  const params = new URLSearchParams();
  const mappings = [
    ["gender", "gender"],
    ["country", "country_id"],
    ["age-group", "age_group"],
    ["min-age", "min_age"],
    ["max-age", "max_age"],
    ["sort-by", "sort_by"],
    ["order", "order"],
    ["limit", "limit"]
  ];

  for (const [elementId, key] of mappings)
  {
    const element = form.querySelector(`#${elementId}`);
    if (element && element.value.trim())
    {
      params.set(key, element.value.trim());
    }
  }

  params.set("page", "1");
  return params;
}

function resolveRoute(path)
{
  const pathname = path.split("?")[0];
  for (const route of routes)
  {
    const match = matchRoute(route.path, pathname);
    if (match)
    {
      return { ...route, params: match };
    }
  }
  return null;
}

function matchRoute(routePath, pathname)
{
  const routeParts = routePath.split("/").filter(Boolean);
  const pathParts = pathname.split("/").filter(Boolean);
  if (routeParts.length !== pathParts.length)
  {
    return null;
  }

  const params = {};
  for (let i = 0; i < routeParts.length; i += 1)
  {
    const routePart = routeParts[i];
    const pathPart = pathParts[i];
    if (routePart.startsWith(":"))
    {
      params[routePart.slice(1)] = decodeURIComponent(pathPart);
      continue;
    }

    if (routePart !== pathPart)
    {
      return null;
    }
  }

  return params;
}

function navLink(href, label, activeRoute)
{
  const isActive = activeRoute === href || (href !== "/dashboard" && state.currentPath.startsWith(href));
  return `<a class="${isActive ? "active" : ""}" data-link href="${href}">${label}</a>`;
}

function textField(id, label, value, placeholder)
{
  return `
    <div class="field">
      <label for="${id}">${label}</label>
      <input id="${id}" value="${escapeAttribute(value)}" placeholder="${escapeAttribute(placeholder)}">
    </div>
  `;
}

function selectField(id, label, value, options)
{
  return `
    <div class="field">
      <label for="${id}">${label}</label>
      <select id="${id}">
        ${options.map(([optionValue, optionLabel]) => `<option value="${escapeAttribute(optionValue)}" ${optionValue === value ? "selected" : ""}>${escapeHtml(optionLabel)}</option>`).join("")}
      </select>
    </div>
  `;
}

function detailItem(title, value)
{
  return `<div><strong>${escapeHtml(title)}</strong><div>${escapeHtml(value ?? "")}</div></div>`;
}

function readCookie(name)
{
  const prefix = `${name}=`;
  return document.cookie
    .split(";")
    .map((item) => item.trim())
    .find((item) => item.startsWith(prefix))
    ?.slice(prefix.length) ?? "";
}

function buildRelativeLink(basePath, backendLink)
{
  const query = backendLink.includes("?") ? backendLink.slice(backendLink.indexOf("?")) : "";
  return `${basePath}${query}`;
}

function getCurrentBrowserPath()
{
  return `${window.location.pathname}${window.location.search}`;
}

function formatDate(value)
{
  try
  {
    return new Date(value).toLocaleString();
  }
  catch
  {
    return value;
  }
}

function normalizeBaseUrl(baseUrl)
{
  return (baseUrl ?? "").trim().replace(/\/$/, "");
}

function escapeHtml(value)
{
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll("\"", "&quot;")
    .replaceAll("'", "&#39;");
}

function escapeAttribute(value)
{
  return escapeHtml(value);
}

class UnauthorizedError extends Error {}
