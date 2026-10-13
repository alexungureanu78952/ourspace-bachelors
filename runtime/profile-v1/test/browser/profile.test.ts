import test from 'node:test';
import assert from 'node:assert/strict';
import { chromium } from 'playwright';
import type { Browser } from 'playwright';
import { AxeBuilder } from '@axe-core/playwright';
import { renderToStaticMarkup } from 'react-dom/server';
import { startPreview } from '../../server.js';
import { dataFixture, sourceFixture } from '../../fixture.js';
import { renderProfile } from '../../../renderer/src/render.js';
import { validateProfile } from '../../../validation/src/profile.js';
void test(
  'runtime-v1 renders sections and literal data under real CSP/iframe isolation',
  { timeout: 60000 },
  async () => {
    const data = {
      ...dataFixture,
      bio: '</script><img src="https://example.invalid/steal">',
      sections: [{ id: 'about', heading: 'About me', body: 'Hello <script>bad()</script>' }],
    };
    const preview = await startPreview(data);
    const channel =
      process.env['PROFILE_BROWSER'] ?? (process.platform === 'win32' ? 'msedge' : 'chromium');
    let browser: Browser | undefined;
    try {
      browser = await chromium.launch({
        headless: true,
        ...(channel === 'chromium' ? {} : { channel }),
      });
      const context = await browser.newContext();
      const page = await context.newPage();
      await page.goto(preview.origin);
      const frame = page
        .frames()
        .find((frame) => frame.url() === preview.contentOrigin + '/profile');
      assert.ok(frame);
      await frame.locator('h1').waitFor();
      assert.equal(await frame.locator('h1').textContent(), data.displayName);
      assert.equal(await frame.locator('main > p').textContent(), data.bio);
      assert.equal(
        await frame.locator('.ourspace-section p').textContent(),
        data.sections[0]?.body,
      );
      assert.equal(await frame.locator('img').count(), 0);
      assert.equal(
        await frame.locator('main').evaluate((el) => getComputedStyle(el).backgroundColor),
        'rgb(25, 21, 45)',
      );
      // DevTools runs this trusted probe; CSP still governs its requested browser capabilities.
      const result = await frame.evaluate(async () => {
        const blocked = { parent: false, cookie: false, storage: false, fetch: false };
        try {
          void parent.document.body;
        } catch {
          blocked.parent = true;
        }
        try {
          void document.cookie;
        } catch {
          blocked.cookie = true;
        }
        try {
          localStorage.setItem('probe', 'x');
        } catch {
          blocked.storage = true;
        }
        try {
          await fetch('/leak');
        } catch {
          blocked.fetch = true;
        }
        const injected = document.createElement('script');
        injected.textContent = 'document.body.dataset.compromised="yes"';
        document.body.append(injected);
        return blocked;
      });
      assert.deepEqual(result, { parent: true, cookie: true, storage: true, fetch: true });
      assert.equal(await frame.evaluate(() => document.body.dataset['compromised']), undefined);
      assert.deepEqual(preview.requests, ['/profile']);
      // Scan the same trusted render in a standalone page: axe cannot reliably traverse an opaque sandbox.
      const profile = validateProfile(sourceFixture, dataFixture);
      const accessibility = await context.newPage();
      await accessibility.setContent(
        '<!doctype html><html lang="en"><head><title>Profile accessibility fixture</title><style>' +
          profile.css +
          '</style></head><body>' +
          renderToStaticMarkup(renderProfile(profile.tree, profile.data)) +
          '</body></html>',
      );
      const report = await new AxeBuilder({ page: accessibility })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
        .analyze();
      assert.deepEqual(report.violations, []);
    } finally {
      await browser?.close();
      await preview.close();
    }
  },
);
