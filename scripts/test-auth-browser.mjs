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
      await page.request.post(bff + '/bff/demo/profiles', { form: { displayName: 'Anonymous' } })
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
  await page.getByLabel('Display name', { exact: true }).fill(demoName);
  await page.getByRole('button', { name: 'Create profile', exact: true }).click();
  await page.getByRole('status').filter({ hasText: 'Profile loaded.' }).waitFor();
  const demoId = await page.locator('#profile-id').innerText();
  assert.equal(await page.getByText(demoName, { exact: true }).count(), 1);
  assert.equal(await page.locator('img').count(), 0);
  await page.getByLabel('Display name', { exact: true }).fill('Second profile');
  await page.getByRole('button', { name: 'Create profile', exact: true }).click();
  await page.getByRole('alert').filter({ hasText: 'This account already has a profile' }).waitFor();
  await page.getByLabel('Profile ID', { exact: true }).fill(demoId);
  await page.getByRole('button', { name: 'Load profile', exact: true }).click();
  await page.getByRole('status').filter({ hasText: 'Profile loaded.' }).waitFor();
  await page.reload();
  assert.equal(await page.locator('#profile-id').innerText(), demoId);
  await page.getByLabel('Profile ID', { exact: true }).fill(crypto.randomUUID());
  await page.getByRole('button', { name: 'Load profile', exact: true }).click();
  await page.getByRole('alert').filter({ hasText: 'Profile not found or unavailable' }).waitFor();
  await page.getByLabel('Profile ID', { exact: true }).fill('invalid-id');
  await page.getByRole('button', { name: 'Load profile', exact: true }).click();
  await page.getByRole('alert').filter({ hasText: 'Enter a valid profile ID' }).waitFor();
  await page.getByLabel('Display name', { exact: true }).fill('   ');
  await page.getByRole('button', { name: 'Create profile', exact: true }).click();
  await page.getByRole('alert').filter({ hasText: 'Use a display name' }).waitFor();
  assert.equal(
    (
      await page.request.post(bff + '/bff/demo/profiles', { form: { displayName: 'Missing CSRF' } })
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
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
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
