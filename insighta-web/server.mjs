import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const publicDir = path.join(__dirname, "public");
const port = Number.parseInt(process.env.PORT ?? "4173", 10);
const apiBaseUrl = (process.env.INSIGHTA_API_URL ?? "http://localhost:5000").trim().replace(/\/$/, "");

const mimeTypes = new Map([
  [".html", "text/html; charset=utf-8"],
  [".css", "text/css; charset=utf-8"],
  [".js", "application/javascript; charset=utf-8"],
  [".json", "application/json; charset=utf-8"],
  [".svg", "image/svg+xml"],
  [".png", "image/png"],
  [".jpg", "image/jpeg"],
  [".jpeg", "image/jpeg"],
  [".ico", "image/x-icon"]
]);

createServer(async (request, response) =>
{
  try
  {
    const url = new URL(request.url ?? "/", `http://${request.headers.host}`);

    if (url.pathname === "/config.js")
    {
      const body = `window.__INSIGHTA_CONFIG__ = ${JSON.stringify({ apiBaseUrl })};`;
      response.writeHead(200, { "Content-Type": "application/javascript; charset=utf-8" });
      response.end(body);
      return;
    }

    const candidate = url.pathname === "/"
      ? path.join(publicDir, "index.html")
      : path.join(publicDir, url.pathname);

    const resolved = path.normalize(candidate);
    const isInsidePublic = resolved.startsWith(publicDir);
    const filePath = isInsidePublic && existsSync(resolved) ? resolved : path.join(publicDir, "index.html");
    const ext = path.extname(filePath);
    const contentType = mimeTypes.get(ext) ?? "application/octet-stream";
    const body = await readFile(filePath);

    response.writeHead(200, { "Content-Type": contentType });
    response.end(body);
  }
  catch (error)
  {
    response.writeHead(500, { "Content-Type": "text/plain; charset=utf-8" });
    response.end(`Server error: ${error instanceof Error ? error.message : "Unknown error"}`);
  }
}).listen(port, () =>
{
  console.log(`Insighta web portal running at http://localhost:${port}`);
  console.log(`Using backend ${apiBaseUrl}`);
});
