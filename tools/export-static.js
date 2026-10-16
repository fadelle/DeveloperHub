// Static export for GitHub Pages (or any static host).
//
// Boots the real app on a scratch port, crawls every page it reports via
// /__routes.json, saves each page's actual rendered HTML to disk, and fetches
// every asset (css/js/search-index.json) those pages actually reference —
// by its real served URL, not by copying wwwroot (see collectAssetPaths
// below for why that distinction matters). This is a snapshot,
// not a live mirror — re-run it whenever Content/ changes and you want the
// static copy to catch up.
//
// Usage:
//   node tools/export-static.js [outDir] [basePath]
//
//   outDir    default: dist
//   basePath  default: "" (root hosting, e.g. a custom domain or a
//             <username>.github.io user/org page). For a GitHub Pages
//             PROJECT page served at https://<user>.github.io/<repo>/,
//             pass "/<repo>" so root-relative links resolve correctly.
//
// Example for this repo (fadelle/DeveloperHub):
//   node tools/export-static.js dist /DeveloperHub

const { spawn } = require("child_process");
const fs = require("fs");
const path = require("path");

const PROJECT_DIR = path.resolve(__dirname, "..");
const OUT_DIR = path.resolve(PROJECT_DIR, process.argv[2] || "dist");
const BASE_PATH = (process.argv[3] || "").replace(/\/$/, "");
const PORT = 5199;
const BASE_URL = `http://localhost:${PORT}`;

async function waitForServer(url, timeoutMs) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      const res = await fetch(url);
      if (res.ok) return;
    } catch (e) {
      // not up yet
    }
    await new Promise((r) => setTimeout(r, 500));
  }
  throw new Error(`Server at ${url} did not respond within ${timeoutMs}ms`);
}

function routeToFilePath(route) {
  if (route === "/") return path.join(OUT_DIR, "index.html");
  const clean = route.replace(/^\//, "").replace(/\/$/, "");
  return path.join(OUT_DIR, clean, "index.html");
}

function rewriteBasePaths(html) {
  if (!BASE_PATH) return html;
  // Rewrites root-relative href="/..." / src="/..." to href="{base}/...".
  // The negative lookahead skips protocol-relative URLs (href="//cdn...").
  return html.replace(/(href|src)="\/(?!\/)/g, `$1="${BASE_PATH}/`);
}

const ASSET_EXTENSIONS = "css|js|json|ico|png|jpe?g|svg|gif|webp|woff2?|ttf";
const ASSET_REF_PATTERN = new RegExp(
  `(?:href|src)="(/(?!/)[^"]+\\.(?:${ASSET_EXTENSIONS}))"`,
  "g"
);

// ASP.NET Core's MapStaticAssets serves CSS/JS under content-hashed virtual
// URLs (e.g. /css/site.34dw87a5u2.css) that only the live server's static-
// assets middleware can resolve back to the real wwwroot/css/site.css file —
// there's no file on disk by that hashed name. A plain host like GitHub Pages
// doesn't have that middleware, so instead of copying wwwroot, this fetches
// each asset from the live server at the EXACT hashed URL the rendered HTML
// actually references, and saves it under that same name — guaranteed to
// match regardless of how the hashing works internally.
function collectAssetPaths(rawHtmlPages) {
  const found = new Set(["/search-index.json"]);
  for (const html of rawHtmlPages) {
    let m;
    while ((m = ASSET_REF_PATTERN.exec(html)) !== null) found.add(m[1]);
  }
  return found;
}

async function fetchAssets(assetPaths) {
  for (const assetPath of assetPaths) {
    const res = await fetch(`${BASE_URL}${assetPath}`);
    if (!res.ok) {
      console.warn(`  SKIP asset ${assetPath} (HTTP ${res.status})`);
      continue;
    }
    const outPath = path.join(OUT_DIR, assetPath.replace(/^\//, ""));
    fs.mkdirSync(path.dirname(outPath), { recursive: true });

    if (assetPath.endsWith(".js") && BASE_PATH) {
      // site.js fetches "/search-index.json" at runtime — a literal string,
      // not an href/src the HTML rewrite pass above ever touches — so it
      // needs the same base-path prefix applied here, in the JS source itself.
      const js = (await res.text()).replace(
        '"/search-index.json"',
        `"${BASE_PATH}/search-index.json"`
      );
      fs.writeFileSync(outPath, js, "utf8");
    } else if (assetPath.endsWith(".json") && BASE_PATH) {
      // Each entry's "url" is a root-relative path baked in by the server at
      // runtime (e.g. "/topics/oop-design/adapter-pattern") — site.js builds
      // result links straight from it, so it needs the same prefix too.
      const json = (await res.text()).replace(/"url":"\//g, `"url":"${BASE_PATH}/`);
      fs.writeFileSync(outPath, json, "utf8");
    } else {
      fs.writeFileSync(outPath, Buffer.from(await res.arrayBuffer()));
    }
    console.log(`  asset ${assetPath}`);
  }
}

function killProcessTree(pid) {
  if (process.platform === "win32") {
    spawn("taskkill", ["/pid", String(pid), "/t", "/f"]);
  } else {
    try { process.kill(-pid, "SIGKILL"); } catch (e) { /* already gone */ }
  }
}

async function main() {
  console.log(`Starting app on ${BASE_URL} ...`);
  // ASPNETCORE_ENVIRONMENT must be Development here: MapStaticAssets only
  // resolves its content-hashed URLs against the loose build output in
  // Development — in Production it expects a `dotnet publish` output and
  // silently serves empty bodies for every fingerprinted asset otherwise.
  const child = spawn(
    "dotnet",
    ["run", "--urls", BASE_URL, "--no-launch-profile"],
    { cwd: PROJECT_DIR, shell: true, env: { ...process.env, ASPNETCORE_ENVIRONMENT: "Development" } }
  );
  child.stdout.on("data", (d) => process.stdout.write(`[server] ${d}`));
  child.stderr.on("data", (d) => process.stderr.write(`[server] ${d}`));

  try {
    await waitForServer(`${BASE_URL}/`, 60000);
    console.log("Server is up. Fetching route list...");

    const routes = await fetch(`${BASE_URL}/__routes.json`).then((r) => r.json());
    console.log(`Found ${routes.length} routes.`);

    fs.rmSync(OUT_DIR, { recursive: true, force: true });
    fs.mkdirSync(OUT_DIR, { recursive: true });

    const rawHtmlPages = [];
    for (const route of routes) {
      const res = await fetch(`${BASE_URL}${route}`);
      if (!res.ok) {
        console.warn(`  SKIP ${route} (HTTP ${res.status})`);
        continue;
      }
      const rawHtml = await res.text();
      rawHtmlPages.push(rawHtml);
      const outPath = routeToFilePath(route);
      fs.mkdirSync(path.dirname(outPath), { recursive: true });
      fs.writeFileSync(outPath, rewriteBasePaths(rawHtml), "utf8");
      console.log(`  wrote ${path.relative(OUT_DIR, outPath)}`);
    }

    console.log("Fetching referenced assets (css/js/search index)...");
    await fetchAssets(collectAssetPaths(rawHtmlPages));

    fs.writeFileSync(path.join(OUT_DIR, ".nojekyll"), "");

    console.log(`\nDone. ${routes.length} pages exported to ${OUT_DIR}`);
    if (BASE_PATH) console.log(`Links rewritten for base path "${BASE_PATH}".`);
  } finally {
    killProcessTree(child.pid);
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
