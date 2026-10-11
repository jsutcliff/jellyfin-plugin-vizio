#!/usr/bin/env node
// Serves dist/ (manifest.json and plugin zips) so a Jellyfin server on the LAN can
// install the plugin from this machine. See release.sh.
//
//   node serve-dist.js        (PORT=8098 by default)

const http = require('http');
const fs = require('fs');
const path = require('path');

const dir = path.join(__dirname, 'dist');
const port = Number(process.env.PORT || 8098);
const types = { '.json': 'application/json', '.zip': 'application/zip' };

http.createServer((req, res) => {
    const name = path.basename(decodeURIComponent(req.url.split('?')[0]));
    const file = path.join(dir, name);
    console.log(req.socket.remoteAddress, req.method, req.url);
    fs.stat(file, (err, stat) => {
        if (err || !stat.isFile()) {
            res.writeHead(404).end();
            return;
        }
        res.writeHead(200, {
            'Content-Type': types[path.extname(name)] || 'application/octet-stream',
            'Content-Length': stat.size,
            'Cache-Control': 'no-store'
        });
        fs.createReadStream(file).pipe(res);
    });
}).listen(port, '0.0.0.0', () => console.log(`Serving ${dir} on http://0.0.0.0:${port}/`));
