#!/usr/bin/env node
// Dev proxy: serves the Jellyfin server's web UI through this machine and injects
// the plugin's vizio.js into the page, so it can be iterated on without rebuilding
// and reinstalling the plugin.
//
//   JELLYFIN_URL=http://<jellyfin-server>:8096 node proxy.js
//
// Then launch http://<this-machine>:8097/web/ on the TV.

const http = require('http');
const net = require('net');
const fs = require('fs');
const path = require('path');

const upstream = new URL(process.env.JELLYFIN_URL || 'http://localhost:8096');
const port = Number(process.env.PORT || 8097);
const scriptPath = path.join(__dirname, 'Jellyfin.Plugin.Vizio', 'Web', 'vizio.js');

function isIndexPage(url) {
    const p = url.split('?')[0];
    return p === '/web/' || p === '/web/index.html';
}

const server = http.createServer((req, res) => {
    // Remote log sink, since retail TVs have no devtools
    if (req.url.toLowerCase() === '/vizio/log' && req.method === 'POST') {
        let body = '';
        req.on('data', (c) => body += c);
        req.on('end', () => {
            console.log('[tv]', body);
            res.writeHead(204).end();
        });
        return;
    }

    const inject = isIndexPage(req.url);
    const headers = { ...req.headers, host: upstream.host };
    if (inject) delete headers['accept-encoding']; // need plain text to rewrite

    const proxyReq = http.request({
        hostname: upstream.hostname,
        port: upstream.port || 80,
        path: req.url,
        method: req.method,
        headers
    }, (proxyRes) => {
        // Jellyfin's redirects (e.g. /web -> /web/) are absolute and point at the
        // upstream; keep the client on the proxy.
        const location = proxyRes.headers.location;
        if (location && location.startsWith(upstream.origin)) {
            proxyRes.headers.location = location.slice(upstream.origin.length) || '/';
        }

        if (!inject || !(proxyRes.headers['content-type'] || '').includes('text/html')) {
            res.writeHead(proxyRes.statusCode, proxyRes.headers);
            proxyRes.pipe(res);
            return;
        }

        let html = '';
        proxyRes.setEncoding('utf8');
        proxyRes.on('data', (c) => html += c);
        proxyRes.on('end', () => {
            if (res.headersSent) return; // upstream errored mid-response
            // Inject first thing in <head> so it runs before jellyfin-web's bundles
            // Same script the plugin injects, re-read each time so edits show up on reload
            const script = '<script>window.VIZIO_DEBUG=true;\n' + fs.readFileSync(scriptPath, 'utf8') + '</script>';
            html = html.replace(/<head[^>]*>/i, (m) => m + script);
            const outHeaders = { ...proxyRes.headers, 'cache-control': 'no-store' };
            delete outHeaders['content-length'];
            delete outHeaders['etag'];
            res.writeHead(proxyRes.statusCode, outHeaders);
            res.end(html);
        });
    });

    proxyReq.on('error', (err) => {
        console.error('upstream error:', err.message);
        if (!res.headersSent) res.writeHead(502);
        res.end();
    });
    req.pipe(proxyReq);
});

// Jellyfin's /socket websocket: pass the raw connection through
server.on('upgrade', (req, socket, head) => {
    const up = net.connect(upstream.port || 80, upstream.hostname, () => {
        const lines = [`${req.method} ${req.url} HTTP/1.1`];
        for (let i = 0; i < req.rawHeaders.length; i += 2) {
            const name = req.rawHeaders[i];
            const value = name.toLowerCase() === 'host' ? upstream.host : req.rawHeaders[i + 1];
            lines.push(`${name}: ${value}`);
        }
        up.write(lines.join('\r\n') + '\r\n\r\n');
        if (head.length) up.write(head);
        up.pipe(socket);
        socket.pipe(up);
    });
    up.on('error', () => socket.destroy());
    socket.on('error', () => up.destroy());
});

server.listen(port, '0.0.0.0', () => {
    console.log(`Proxying ${upstream.origin} on http://0.0.0.0:${port}/web/`);
});
