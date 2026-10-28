import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

// Review HTML captured from real MVC integration responses; no database writes.
const root = new URL('../', import.meta.url);
const routes = new Map([
    ['/', ['artifacts/blog-review/reader.html', 'text/html']],
    ...['reader', 'editor', 'dashboard', 'explore'].map(name => [`/${name}.html`, [`artifacts/blog-review/${name}.html`, 'text/html']]),
    ['/css/site.css', ['src/DevSync.Web/wwwroot/css/site.css', 'text/css']],
    ['/js/site.js', ['src/DevSync.Web/wwwroot/js/site.js', 'text/javascript']],
    ['/favicon.svg', ['src/DevSync.Web/wwwroot/favicon.svg', 'image/svg+xml']]
]);
await readFile(fileURLToPath(new URL('artifacts/blog-review/reader.html', root)));
createServer(async (request, response) => {
    const route = routes.get(new URL(request.url, 'http://localhost').pathname);
    if (!route || !['GET', 'HEAD'].includes(request.method)) { response.writeHead(404).end(); return; }
    try {
        const bytes = await readFile(new URL(route[0], root));
        response.writeHead(200, { 'Content-Type': route[1] + '; charset=utf-8', 'Cache-Control': 'no-store' });
        response.end(request.method === 'HEAD' ? undefined : bytes);
    } catch { response.writeHead(404).end(); }
}).listen(5181, '127.0.0.1', () => console.log('Read-only blog review: http://localhost:5181/reader.html'));
