import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { chromium } from 'playwright';
import { buildRuntime, makeArtifact, digest } from '../artifact.mjs';
const valid = JSON.parse(await readFile(new URL('../fixtures/valid.json', import.meta.url), 'utf8'));
const listen = server => new Promise(resolve => server.listen(0, '127.0.0.1', () => resolve('http://127.0.0.1:' + server.address().port)));
const close = server => new Promise(resolve => server.close(resolve));
test('real browser: render and enforce iframe/CSP isolation', { timeout: 60000 }, async () => {
  const runtime = await buildRuntime();
  const data = { displayName: 'Alex <script>bad()</script>', bio: '</script><img src="https://example.invalid/stolen">' };
  const artifact = makeArtifact(valid, data, runtime);
  const probe = `(async () => {
    const result = {};
    try { result.parentCookie = parent.document.cookie; } catch { result.parentBlocked = true; }
    try { result.cookie = document.cookie; } catch { result.cookieBlocked = true; }
    try { localStorage.setItem("probe", "x"); } catch { result.storageBlocked = true; }
    try { await fetch("/leak?secret=test"); result.networkBlocked = false; } catch { result.networkBlocked = true; }
    try { await fetch("https://example.invalid/leak"); result.externalBlocked = false; } catch { result.externalBlocked = true; }
    const image = new Image(); image.src = "/image-leak"; document.body.append(image);
    const script = document.createElement("script"); script.textContent = "window.injected = true"; document.body.append(script);
    document.body.dataset.result = JSON.stringify(result);
  })();`;
  const probeCsp = "default-src 'none'; script-src 'sha256-" + digest(probe) + "'; connect-src 'none'; img-src 'none'; base-uri 'none'; form-action 'none'; sandbox allow-scripts";
  const requests = [];
  const contentServer = createServer((req, res) => {
    requests.push(req.url);
    const isProbe = req.url === '/probe';
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Content-Security-Policy': isProbe ? probeCsp : artifact.csp, 'X-Content-Type-Options': 'nosniff', 'Referrer-Policy': 'no-referrer' });
    res.end(isProbe ? '<!doctype html><body><script>' + probe + '</script></body>' : artifact.html);
  });
  const contentOrigin = await listen(contentServer);
  const appServer = createServer((req, res) => {
    res.writeHead(200, { 'Content-Type': 'text/html', 'Set-Cookie': 'session=app-secret; HttpOnly; SameSite=Strict; Path=/' });
    res.end('<!doctype html><body><h1>Trusted application</h1><iframe title="Profile" style="width:700px;height:650px;border:0" sandbox="allow-scripts" src="' + contentOrigin + '/profile"></iframe><iframe title="Probe" style="display:none" sandbox="allow-scripts" src="' + contentOrigin + '/probe"></iframe>');
  });
  const appOrigin = await listen(appServer);
  let browser;
  try {
    browser = await chromium.launch({ channel: process.env.PROFILE_BROWSER === 'chromium' ? undefined : 'msedge', headless: true });
    const page = await browser.newPage({ viewport: { width: 1200, height: 900 } });
    await page.goto(appOrigin);
    const frame = page.frameLocator('iframe[title="Profile"]');
    await frame.locator('h1').waitFor();
    assert.equal(await frame.locator('h1').textContent(), data.displayName);
    assert.equal(await frame.locator('.card p').textContent(), data.bio);
    assert.equal(await frame.locator('img').count(), 0);
    assert.equal(await frame.locator('.profile').evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(25, 21, 45)');
    const probeFrame = page.frames().find(f => f.url() === contentOrigin + '/probe');
    await probeFrame.waitForFunction(() => Boolean(document.body.dataset.result));
    const result = await probeFrame.evaluate(() => JSON.parse(document.body.dataset.result));
    assert.equal(result.parentBlocked, true);
    assert.equal(result.cookieBlocked, true);
    assert.equal(result.storageBlocked, true);
    assert.equal(result.networkBlocked, true);
    assert.equal(result.externalBlocked, true);
    assert.equal(await probeFrame.evaluate(() => window.injected), undefined);
    assert.deepEqual(requests.sort(), ['/probe', '/profile']);
    await mkdir(new URL('../artifacts/', import.meta.url), { recursive: true });
    await page.screenshot({ path: new URL('../artifacts/preview.png', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1'), fullPage: true });
    await writeFile(new URL('../artifacts/browser-results.json', import.meta.url), JSON.stringify({ browser: browser.version(), result, contentRequests: requests, note: 'Two localhost ports test different origins, not different production sites. No production sandbox claim.' }, null, 2));
  } finally {
    if (browser) await browser.close();
    await Promise.all([close(appServer), close(contentServer)]);
  }
});

