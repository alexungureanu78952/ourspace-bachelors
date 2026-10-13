import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { buildRuntime, makeArtifact } from './artifact.mjs';
const fixture = JSON.parse(await readFile(new URL('./fixtures/valid.json', import.meta.url), 'utf8'));
const artifact = makeArtifact(fixture, { displayName: 'Alex', bio: 'A custom layout rendered by the shared OurSpace runtime.' }, await buildRuntime());
const listen = server => new Promise(resolve => server.listen(0, '127.0.0.1', () => resolve('http://127.0.0.1:' + server.address().port)));
const content = createServer((req, res) => {
  if (req.url !== '/profile') { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Content-Security-Policy': artifact.csp, 'X-Content-Type-Options': 'nosniff', 'Referrer-Policy': 'no-referrer', 'Cache-Control': 'no-store' });
  res.end(artifact.html);
});
const origin = await listen(content);
const app = createServer((req, res) => {
  res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' });
  res.end('<!doctype html><html><head><title>OurSpace runtime experiment</title></head><body style="margin:24px;font-family:sans-serif"><h1>OurSpace profile preview</h1><iframe title="Profile" sandbox="allow-scripts" style="width:100%;height:650px;border:0" src="' + origin + '/profile"></iframe></body></html>');
});
console.log('Open ' + await listen(app) + ' — local fixture only; Ctrl+C stops both servers.');
const stop = () => { app.close(); content.close(); };
process.on('SIGINT', stop);
process.on('SIGTERM', stop);

