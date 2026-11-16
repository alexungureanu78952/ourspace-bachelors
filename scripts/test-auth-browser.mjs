import assert from 'node:assert/strict';
import { chromium } from 'playwright';

const identity = process.env.OURSPACE_IDENTITY_ORIGIN;
const bff = process.env.OURSPACE_BFF_ORIGIN;
assert.ok(identity && bff, 'Test hosts are required.');
const channel =
  process.env.PROFILE_BROWSER ?? (process.platform === 'win32' ? 'msedge' : 'chromium');
const browser = await chromium.launch({
  headless: true,
  ...(channel === 'chromium' ? {} : { channel }),
});
try {
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();
  const navigation = [];
  page.on('response', (response) => {
    if (response.request().isNavigationRequest())
      navigation.push({
        path: new URL(response.url()).pathname,
        status: response.status(),
        cookies: response.headers()['set-cookie']?.split('=')[0],
      });
  });
  const email = 'learner-' + crypto.randomUUID() + '@example.test';
  const password = 'OurSpace-browser-123!';
  const replacement = 'OurSpace-renewed-456!';
  const submit = async () => page.getByRole('button', { name: 'Continue', exact: true }).click();
  const login = async (credential) => {
    const response = await page.goto(bff + '/bff/login');
    assert.equal(response.status(), 200, await page.locator('body').innerText());
    assert.equal(
      await page.getByLabel('Email', { exact: true }).count(),
      1,
      await page.locator('body').innerText(),
    );
    await page.getByLabel('Email', { exact: true }).fill(email);
    await page.getByLabel('Password', { exact: true }).fill(credential);
    await submit();
  };
  assert.equal(
    (
      await page.request.get(identity + '/account/login?returnUrl=https://untrusted.example/')
    ).status(),
    400,
  );
  assert.equal(
    (
      await page.request.post(identity + '/account/register', {
        form: { Email: email, Password: password },
      })
    ).status(),
    400,
  );
  assert.equal(
    (
      await page.request.post(bff + '/bff/profiles', {
        headers: { 'Content-Type': 'application/json' },
        data: '{"displayName":"Anonymous","bio":""}',
      })
    ).status(),
    401,
  );
  const registrationPage = await page.goto(identity + '/account/register');
  assert.equal(registrationPage.status(), 200, await page.locator('body').innerText());
  assert.equal(
    await page.getByLabel('Email', { exact: true }).count(),
    1,
    (await page.content()).slice(0, 2000),
  );
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await submit();
  await page.getByRole('heading', { name: 'Check your email' }).waitFor();
  await login(password);
  await page.getByRole('status').filter({ hasText: 'Sign-in failed' }).waitFor();
  const confirmation = await (
    await page.request.get(identity + '/__test/mail?email=' + encodeURIComponent(email))
  ).json();
  await page.goto(confirmation.link);
  await submit();
  await page.getByRole('status').filter({ hasText: 'Email confirmed' }).waitFor();
  await page.goto(confirmation.link);
  await submit();
  await page.getByRole('status').filter({ hasText: 'already been used' }).waitFor();
  await login(password);
  await page.waitForURL(bff + '/', { timeout: 10000 }).catch(async () => {
    throw new Error(
      JSON.stringify(navigation.slice(-12)) + '\n' + (await page.locator('body').innerText()),
    );
  });
  const demoName = 'Demo <img src=x onerror=alert(1)>';
  const csrfData = await (await page.request.get(bff + '/bff/csrf')).json();
  const createResponse = await page.request.post(bff + '/bff/profiles', {
    headers: { 'X-CSRF-TOKEN': csrfData.token, 'Content-Type': 'application/json' },
    data: JSON.stringify({ displayName: demoName, bio: '' }),
  });
  assert.equal(createResponse.status(), 201, await createResponse.text());
  const demoProfile = await createResponse.json();
  const demoId = demoProfile.id;
  // Display name must be stored verbatim (the frontend is responsible for escaping on render).
  assert.equal(demoProfile.displayName, demoName);
  // Duplicate profile → 409.
  assert.equal(
    (
      await page.request.post(bff + '/bff/profiles', {
        headers: { 'X-CSRF-TOKEN': csrfData.token, 'Content-Type': 'application/json' },
        data: JSON.stringify({ displayName: 'Second profile', bio: '' }),
      })
    ).status(),
    409,
  );
  // Non-existent profile → 404.
  assert.equal(
    (await page.request.get(bff + '/bff/profiles/' + crypto.randomUUID())).status(),
    404,
  );
  // Whitespace-only display name → 400.
  assert.equal(
    (
      await page.request.post(bff + '/bff/profiles', {
        headers: { 'X-CSRF-TOKEN': csrfData.token, 'Content-Type': 'application/json' },
        data: JSON.stringify({ displayName: '   ', bio: '' }),
      })
    ).status(),
    400,
  );
  assert.equal(
    (
      await page.request.post(bff + '/bff/profiles', {
        headers: { 'Content-Type': 'application/json' },
        data: '{"displayName":"Missing CSRF","bio":""}',
      })
    ).status(),
    400,
  );
  const session = (await context.cookies(bff)).find(
    (cookie) => cookie.name === '__Host-OurSpace.Bff',
  );
  assert.ok(session?.secure && session.httpOnly);
  assert.equal((await page.request.get(bff + '/bff/me')).status(), 200);
  const rejected = await page.evaluate(
    async () =>
      (
        await fetch('/bff/profiles', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: '{"displayName":"Missing CSRF"}',
        })
      ).status,
  );
  assert.equal(rejected, 400);
  const csrf = await (await page.request.get(bff + '/bff/csrf')).json();
  const created = await page.request.post(bff + '/bff/profiles', {
    headers: { 'X-CSRF-TOKEN': csrf.token },
    data: { displayName: 'Browser profile' },
  });
  assert.equal(
    created.status(),
    409,
    'An account cannot create a second profile through the real token validator.',
  );
  const profileResponse = await page.request.get(bff + '/bff/profiles/' + demoId);
  assert.equal(profileResponse.status(), 200);
  const profile = await profileResponse.json();
  assert.equal(profile.ownerId, (await (await page.request.get(bff + '/bff/me')).json()).userId);
  assert.ok(!JSON.stringify(profile).includes('access_token'));
  await page.request.post(bff + '/__test/expire-access');
  const reads = await Promise.all([
    page.request.get(bff + '/bff/profiles/' + profile.id),
    page.request.get(bff + '/bff/profiles/' + profile.id),
  ]);
  assert.deepEqual(
    reads.map((response) => response.status()),
    [200, 200],
  );
  await page.goto(identity + '/account/forgot');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await submit();
  await page.getByRole('heading', { name: 'Check your email' }).waitFor();
  const reset = await (
    await page.request.get(identity + '/__test/mail?email=' + encodeURIComponent(email))
  ).json();
  await page.goto(reset.link);
  await page.getByLabel('Password', { exact: true }).fill(replacement);
  await submit();
  await page.getByRole('status').filter({ hasText: 'Password changed' }).waitFor();
  await page.goto(reset.link);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await submit();
  await page.getByRole('status').filter({ hasText: 'This reset request is invalid' }).waitFor();
  await page.request.post(bff + '/__test/expire-access');
  assert.equal((await page.request.get(bff + '/bff/profiles/' + profile.id)).status(), 401);
  await login(password);
  await page.getByRole('status').filter({ hasText: 'Sign-in failed' }).waitFor();
  await login(replacement);
  await page.waitForURL(bff + '/', { timeout: 10000 }).catch(async () => {
    throw new Error(
      JSON.stringify(navigation.slice(-12)) + '\n' + (await page.locator('body').innerText()),
    );
  });
  // Sign out via programmatic form submission — matches the Next.js frontend's signOut().
  const signOutCsrf = await (await page.request.get(bff + '/bff/csrf')).json();
  await page.goto(bff + '/');
  await page.evaluate(
    ({ token }) => {
      const form = document.createElement('form');
      form.method = 'POST';
      form.action = '/bff/logout';
      const input = document.createElement('input');
      input.type = 'hidden';
      input.name = '__RequestVerificationToken';
      input.value = token;
      form.appendChild(input);
      document.body.appendChild(form);
      form.submit();
    },
    { token: signOutCsrf.token },
  );
  await page.getByRole('heading', { name: 'Sign out of OurSpace' }).waitFor();
  await submit();
  await page.waitForURL(bff + '/', { timeout: 10000 }).catch(async () => {
    throw new Error(
      JSON.stringify(navigation.slice(-12)) + '\n' + (await page.locator('body').innerText()),
    );
  });
  assert.equal((await page.request.get(bff + '/bff/me')).status(), 401);
  const replay = await browser.newContext({ ignoreHTTPSErrors: true });
  await replay.addCookies([session]);
  assert.equal((await replay.request.get(bff + '/bff/me')).status(), 401);
  assert.equal(
    (await page.request.get(bff + '/signin-oidc?code=invalid&state=invalid')).status(),
    400,
  );
  process.stdout.write(JSON.stringify({ cookie: session.value }));
} finally {
  await browser.close();
}
