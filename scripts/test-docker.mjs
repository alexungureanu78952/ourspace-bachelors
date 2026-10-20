import dns from 'node:dns';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { chromium } from 'playwright';

// Test-process DNS adapter: browsers resolve .localhost automatically, Node may not.
const nativeLookup = dns.promises.lookup.bind(dns.promises);
const localNames = new Set(['app.localhost', 'identity.localhost', 'profiles.localhost']);
dns.promises.lookup = (hostname, ...args) =>
  nativeLookup(localNames.has(hostname) ? '127.0.0.1' : hostname, ...args);
const identity = 'https://identity.localhost:7443';
const app = 'https://app.localhost:7443';
const browser = await chromium.launch({
  headless: true,
  ...(process.platform === 'win32' ? { channel: 'msedge' } : {}),
});
const context = await browser.newContext({ ignoreHTTPSErrors: true });
const page = await context.newPage();
page.setDefaultTimeout(15000);
const email = 'docker-' + crypto.randomUUID() + '@example.test';
const password = 'Docker-OurSpace-123!';
const replacement = 'Docker-OurSpace-456!';
const submit = () => page.getByRole('button', { name: 'Continue', exact: true }).click();
const compose = (...args) => {
  const result = spawnSync('docker', ['compose', '-f', 'infra/compose.yml', ...args], {
    encoding: 'utf8',
    timeout: 180000,
  });
  assert.equal(result.status, 0, result.stderr);
  return result.stdout;
};
const waitReady = async () => {
  for (let attempt = 0; attempt < 90; attempt++) {
    try {
      if (
        (await page.request.get(app + '/health/ready', { timeout: 3000 })).status() === 200 &&
        (await page.request.get(identity + '/health/ready', { timeout: 3000 })).status() === 200
      )
        return;
    } catch {
      /* Container restart briefly interrupts the gateway. */
    }
    await new Promise((resolve) => setTimeout(resolve, 1000));
  }
  throw new Error('Deployment did not become ready.');
};
const mailLink = async (subject) => {
  for (let attempt = 0; attempt < 20; attempt++) {
    const response = await page.request.get('http://127.0.0.1:8025/api/v1/messages?limit=100');
    assert.equal(response.status(), 200);
    const list = await response.json();
    const message = list.messages.find(
      (item) =>
        item.Subject === subject && item.To.some((recipient) => recipient.Address === email),
    );
    if (message) {
      const detail = await (
        await page.request.get('http://127.0.0.1:8025/api/v1/message/' + message.ID)
      ).json();
      const match = detail.Text.match(/https:\/\/identity\.localhost:7443\/account\/[^\s]+/);
      assert.ok(match, 'SMTP email must contain its account link.');
      return match[0];
    }
    await new Promise((resolve) => setTimeout(resolve, 500));
  }
  throw new Error('SMTP email did not reach Mailpit.');
};
const login = async (credential) => {
  await page.goto(app + '/bff/login');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Password', { exact: true }).fill(credential);
  await submit();
};
try {
  await waitReady();
  await page.goto(identity + '/account/register');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await submit();
  await page.getByRole('heading', { name: 'Check your email' }).waitFor();
  const confirmation = await mailLink('Confirm your OurSpace email');
  console.log('Registration email arrived through STARTTLS.');
  // Recreate containers rather than merely restarting their processes: volumes must preserve keys.
  compose('up', '-d', '--no-deps', '--force-recreate', '--wait', 'identity', 'bff');
  await waitReady();
  console.log('Identity and BFF containers recreated.');
  await page.goto(confirmation);
  await submit();
  await page.getByRole('status').filter({ hasText: 'Email confirmed' }).waitFor();
  await login(password);
  await page.waitForURL(app + '/');
  const session = (await context.cookies(app)).find(
    (cookie) => cookie.name === '__Host-OurSpace.Bff',
  );
  assert.ok(session?.httpOnly && session.secure);
  assert.equal(
    (
      await page.request.post(app + '/bff/profiles', { data: { displayName: 'Missing CSRF' } })
    ).status(),
    400,
  );
  const csrf = await (await page.request.get(app + '/bff/csrf')).json();
  const created = await page.request.post(app + '/bff/profiles', {
    headers: { 'X-CSRF-TOKEN': csrf.token },
    data: { displayName: 'Docker deployment profile' },
  });
  assert.equal(
    created.status(),
    201,
    'Profile creation through the container JWT validator failed.',
  );
  const profile = await created.json();
  compose('up', '-d', '--no-deps', '--force-recreate', '--wait', 'identity', 'bff', 'profiles');
  await waitReady();
  assert.equal((await page.request.get(app + '/bff/me')).status(), 200);
  console.log('Authenticated session survived container recreation.');
  assert.equal((await page.request.get(app + '/bff/profiles/' + profile.id)).status(), 200);
  await page.goto(identity + '/account/forgot');
  await page.getByLabel('Email', { exact: true }).fill(email);
  await submit();
  await page.getByRole('heading', { name: 'Check your email' }).waitFor();
  await page.goto(await mailLink('Reset your OurSpace password'));
  await page.getByLabel('Password', { exact: true }).fill(replacement);
  await submit();
  await page.getByRole('status').filter({ hasText: 'Password changed' }).waitFor();
  await page.goto(app + '/');
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
  await page.getByRole('heading', { name: 'Sign out of OurSpace' }).waitFor();
  await submit();
  await page.waitForURL(app + '/');
  assert.equal((await page.request.get(app + '/bff/me')).status(), 401);
  await login(replacement);
  await page.waitForURL(app + '/');
  await page.getByRole('button', { name: 'Sign out', exact: true }).click();
  await page.getByRole('heading', { name: 'Sign out of OurSpace' }).waitFor();
  await submit();
  await page.waitForURL(app + '/');

  for (const name of ['identity', 'bff']) {
    const keys = compose('exec', '-T', name, 'sh', '-c', 'cat /var/ourspace/keys/key-*.xml');
    assert.ok(keys.includes('encryptedSecret'), 'Persisted key rings must be encrypted.');
    assert.ok(!keys.includes('<masterKey'), 'Plaintext master keys must not be persisted.');
  }
  for (const service of ['profiles', 'identity', 'bff']) {
    const result = compose(
      'exec',
      '-T',
      service + '-db',
      'psql',
      '-U',
      'ourspace_' + service,
      '-d',
      'ourspace_' + service,
      '-Atc',
      "SELECT rolsuper OR rolcreatedb OR rolcreaterole FROM pg_roles WHERE rolname='ourspace_" +
        service +
        "_app'",
    );
    assert.equal(result.trim(), 'f');
  }
  console.log(
    'Docker deployment passed: HTTPS, real STARTTLS email, account flows, CSRF, JWT profile access, container recreation, encrypted persistent keys and limited database roles.',
  );
} finally {
  await browser.close();
}
