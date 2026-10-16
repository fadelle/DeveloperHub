// Minimal static file server for previewing the exported site locally —
// zero dependencies, since `tools/export-static.js` has no dev-server of
// its own. Usage: node tools/static-serve.js [dir] [port]
const http = require("http");
const fs = require("fs");
const path = require("path");

const root = process.argv[2] || process.cwd();
const port = Number(process.argv[3] || 8080);

const types = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "application/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".ico": "image/x-icon",
  ".svg": "image/svg+xml"
};

http.createServer((req, res) => {
  let urlPath = decodeURIComponent(req.url.split("?")[0]);
  let filePath = path.join(root, urlPath);

  if (urlPath.endsWith("/") || !path.extname(urlPath)) {
    filePath = path.join(filePath, "index.html");
  }

  fs.readFile(filePath, (err, data) => {
    if (err) {
      res.writeHead(404, { "Content-Type": "text/plain" });
      res.end("404 Not Found: " + urlPath);
      return;
    }
    const ext = path.extname(filePath);
    res.writeHead(200, { "Content-Type": types[ext] || "application/octet-stream" });
    res.end(data);
  });
}).listen(port, () => console.log("Static server on http://localhost:" + port));
