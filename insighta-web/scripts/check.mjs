import { access } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const root = path.resolve(__dirname, "..");

const requiredPaths = [
  "package.json",
  "server.mjs",
  "README.md",
  "public/index.html",
  "public/app.js",
  "public/styles.css"
];

for (const relativePath of requiredPaths)
{
  await access(path.join(root, relativePath));
}

console.log("Insighta web portal structure looks good.");
