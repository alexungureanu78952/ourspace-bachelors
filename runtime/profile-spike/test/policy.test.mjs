import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { validateBundle, validateData, PolicyError } from '../policy.mjs';
import { hostileFixtures } from '../fixtures/hostile.mjs';
import { requireExecutionSandbox } from '../sandbox.mjs';
const valid = JSON.parse(await readFile(new URL('../fixtures/valid.json', import.meta.url), 'utf8'));
test('supported layout becomes data, not executable code', () => {
  const result = validateBundle(valid);
  assert.equal(result.tree.tag, 'main');
  assert.equal(result.tree.children[0].children[1].children[0].field, 'displayName');
  assert.equal(result.css, valid.files['styles.css']);
});
for (const fixture of hostileFixtures(valid)) test('reject: ' + fixture.name, () => {
  assert.throws(() => validateBundle(fixture.bundle), PolicyError);
});
test('profile data contract rejects nontext and unknown keys', () => {
  assert.throws(() => validateData({ displayName: {}, bio: '' }), PolicyError);
  assert.throws(() => validateData({ displayName: '', bio: '', credential: 'secret' }), PolicyError);
});
test('executable user JS stays disabled without a verified sandbox', () => {
  assert.throws(() => requireExecutionSandbox(), { code: 'SANDBOX_UNAVAILABLE' });
});

