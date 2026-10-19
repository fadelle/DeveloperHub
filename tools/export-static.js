// Static export for GitHub Pages (or any static host).
//
// Boots the real app on a scratch port, crawls every page it reports via
// /__routes.json, saves each page's actual rendered HTML to disk, and copies
// wwwroot (css/js/search-index.json) alongside it. This is a snapshot, not a
// live mirror — re-run it whenever Content/ changes and you want the static
// copy to catch up.
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

// Every fetch in this script goes through this — a hung request (the server
// deadlocked, a route that never responds) must fail loudly in seconds, not
// run until CI's hard job ceiling kills the whole workflow hours later. That
// exact failure mode is why this exists: an earlier version of this script
// had no timeout here and no reliable child-process cleanup (see
// killProcessTree below), and a stuck run was only caught by GitHub Actions'
// 6-hour job ceiling.
const FETCH_TIMEOUT_MS = 20000;
async function fetchWithTimeout(url, timeoutMs = FETCH_TIMEOUT_MS) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(url, { signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
}

async function waitForServer(url, timeoutMs) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      const res = await fetchWithTimeout(url, 3000);
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

function copyWwwroot() {
  const srcDir = path.join(PROJECT_DIR, "wwwroot");

  function copyDir(from, to) {
    fs.mkdirSync(to, { recursive: true });
    for (const entry of fs.readdirSync(from, { withFileTypes: true })) {
      const srcPath = path.join(from, entry.name);
      const destPath = path.join(to, entry.name);
      if (entry.isDirectory()) {
        copyDir(srcPath, destPath);
        continue;
      }
      if (entry.name === "site.js" && BASE_PATH) {
        // site.js fetches "/search-index.json" at runtime — a literal
        // string, not an href/src the HTML rewrite pass ever touches — so
        // it needs the same base-path prefix applied here, in the source.
        const js = fs
          .readFileSync(srcPath, "utf8")
          .replace('"/search-index.json"', `"${BASE_PATH}/search-index.json"`);
        fs.writeFileSync(destPath, js, "utf8");
      } else if (entry.name === "search-index.json" && BASE_PATH) {
        // Each entry's "url" is a root-relative path baked in by the server
        // at runtime (e.g. "/topics/oop-design/adapter-pattern") — site.js
        // builds result links straight from it, so it needs the prefix too.
        const json = fs
          .readFileSync(srcPath, "utf8")
          .replace(/"url":"\//g, `"url":"${BASE_PATH}/`);
        fs.writeFileSync(destPath, json, "utf8");
      } else {
        fs.copyFileSync(srcPath, destPath);
      }
    }
  }

  copyDir(srcDir, OUT_DIR);
}

// Spawning with detached:true makes the child (and, critically, the real
// dotnet/Kestrel process it starts) the leader of its own process group, so
// killing -pid (the negated pid = "the whole group") actually reaches it.
// Without this, the POSIX branch below silently killed nothing — the real
// server process lived on as an orphan holding stdout open, which is why
// the CI job hung until GitHub's 6-hour ceiling force-cancelled it instead
// of the script's own (apparently successful) cleanup ever taking effect.
function killProcessTree(pid) {
  if (process.platform === "win32") {
    spawn("taskkill", ["/pid", String(pid), "/t", "/f"]);
  } else {
    try {
      process.kill(-pid, "SIGKILL");
    } catch (e) {
      /* already gone */
    }
  }
}

async function main() {
  console.log(`Starting app on ${BASE_URL} ...`);
  const child = spawn(
    "dotnet",
    ["run", "--urls", BASE_URL, "--no-launch-profile"],
    {
      cwd: PROJECT_DIR,
      shell: true,
      detached: process.platform !== "win32",
      env: { ...process.env, ASPNETCORE_ENVIRONMENT: "Development" },
    }
  );
  child.stdout.on("data", (d) => process.stdout.write(`[server] ${d}`));
  child.stderr.on("data", (d) => process.stderr.write(`[server] ${d}`));

  try {
    await waitForServer(`${BASE_URL}/`, 60000);
    console.log("Server is up. Fetching route list...");

    const routes = await fetchWithTimeout(`${BASE_URL}/__routes.json`).then((r) => r.json());
    console.log(`Found ${routes.length} routes.`);

    fs.rmSync(OUT_DIR, { recursive: true, force: true });
    fs.mkdirSync(OUT_DIR, { recursive: true });

    for (const route of routes) {
      const res = await fetchWithTimeout(`${BASE_URL}${route}`);
      if (!res.ok) {
        console.warn(`  SKIP ${route} (HTTP ${res.status})`);
        continue;
      }
      const html = rewriteBasePaths(await res.text());
      const outPath = routeToFilePath(route);
      fs.mkdirSync(path.dirname(outPath), { recursive: true });
      fs.writeFileSync(outPath, html, "utf8");
      console.log(`  wrote ${path.relative(OUT_DIR, outPath)}`);
    }

    console.log("Copying wwwroot assets...");
    copyWwwroot();

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
