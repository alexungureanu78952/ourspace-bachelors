import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { renderToStaticMarkup } from 'react-dom/server';
import { ContractError } from '../../../contracts/src/diagnostics.js';
import { LIMITS, VERSIONS } from '../../../contracts/src/policy.js';
import { parseJsx } from '../../../authoring/src/jsx.js';
import { validateData, validateSource, validateTree } from '../../../validation/src/contracts.js';
import { validateCss } from '../../../validation/src/css.js';
import { validateProfile } from '../../../validation/src/profile.js';
import { renderProfile } from '../../../renderer/src/render.js';
import { assembleArtifact, canonicalJson, sha256 } from '../../../artifacts/src/assemble.js';
import { dataFixture, sourceFixture } from '../../fixture.js';
import type { ProfileNode } from '../../../contracts/src/schemas.js';
const runtime = {
  runtimeVersion: VERSIONS.runtime,
  policyVersion: VERSIONS.policy,
  treeSchemaVersion: VERSIONS.tree,
  code: 'console.log("trusted-runtime")',
  sha256: sha256('console.log("trusted-runtime")'),
};
const changeSource = (source: string) => ({
  ...sourceFixture,
  files: { ...sourceFixture.files, 'Profile.tsx': source },
});
const wrapper = (expression: string) =>
  'export default function Profile({ data }) { return <main>' + expression + '</main>; }';
void test('baseline authoring yields the exact stable tree', () => {
  assert.deepEqual(parseJsx(wrapper('<h1>{data.displayName}</h1>')), {
    schemaVersion: VERSIONS.tree,
    root: {
      kind: 'element',
      tag: 'main',
      props: {},
      children: [
        {
          kind: 'element',
          tag: 'h1',
          props: {},
          children: [{ kind: 'data', field: 'displayName' }],
        },
      ],
    },
  });
});
void test('sections capability extends rendering without artifact-lifecycle changes', () => {
  const data = {
    ...dataFixture,
    sections: [{ id: 'x', heading: '<script>bad()</script>', body: 'A & B' }],
  };
  const artifact = assembleArtifact(sourceFixture, data, runtime, 'https://app.example.test');
  const profile = validateProfile(sourceFixture, data);
  const html = renderToStaticMarkup(renderProfile(profile.tree, profile.data));
  assert.match(html, /&lt;script&gt;bad\(\)&lt;\/script&gt;/);
  assert.match(html, /A &amp; B/);
  assert.equal(artifact.metadata.artifactFormatVersion, VERSIONS.artifact);
});
void test('text entities and surrounding inline whitespace have defined semantics', () => {
  const tree = validateTree(parseJsx(wrapper('Hello &amp; <strong>friends</strong> today')));
  const html = renderToStaticMarkup(renderProfile(tree, dataFixture));
  assert.equal(html, '<main>Hello &amp; <strong>friends</strong> today</main>');
});
const expressions = [
  'fetch("/steal")',
  'globalThis["fetch"]("/steal")',
  'document.cookie',
  'localStorage.getItem("token")',
  'Profile({data})',
  'import("node:fs")',
  'data["bio"]',
  'data?.bio',
  'data.unknown',
  'data.sections.map(x => x)',
  '...data',
];
for (const expression of expressions)
  void test('reject expression: ' + expression, () => {
    assert.throws(
      () => validateProfile(changeSource(wrapper('{' + expression + '}')), dataFixture),
      ContractError,
    );
  });
const modules = [
  'import fs from "node:fs";' + wrapper('x'),
  'export default function Profile({ data }) { while(true) {} return <main/>; }',
  'export default async function Profile({ data }) { return <main/>; }',
  'export default function Profile({ data } = fetch("/steal")) { return <main/>; }',
  'export default function Profile({ data }) { return <main><p></main>; }',
  'export default function Profile({ data }) { return <script/>; }',
  wrapper('<iframe/>'),
  wrapper('<img src="https://example.test"/>'),
  wrapper('<div {...data}/>'),
  wrapper('<div onClick="bad"/>'),
  wrapper('<div dangerouslySetInnerHTML="bad"/>'),
  wrapper('<Custom/>'),
  wrapper('<div className="a" className="b"/>'),
];
for (const [index, source] of modules.entries())
  void test('reject unsupported module/element ' + String(index), () => {
    assert.throws(() => validateProfile(changeSource(source), dataFixture), ContractError);
  });
for (const css of [
  '@import "https://example.test/a";',
  '.x { background: url(/steal); }',
  String.raw`.x { background: u\72l(/steal); }`,
  '.x { width: expression(alert(1)); }',
  '.x { color: var(--secret); }',
  '/* </style><script>bad()</script> */',
  '.x { color: red !important; }',
  '.x { --custom: red; }',
  '.x { position: fixed; }',
])
  void test('reject unsupported CSS: ' + css, () =>
    assert.throws(() => validateCss(css), ContractError),
  );
void test('bound source, CSS, tree depth and node count', () => {
  assert.throws(
    () => validateSource(changeSource(' '.repeat(LIMITS.sourceBytes + 1))),
    ContractError,
  );
  assert.throws(() => validateCss(' '.repeat(LIMITS.cssBytes + 1)), ContractError);
  assert.throws(
    () =>
      validateProfile(
        changeSource(wrapper('<div>'.repeat(25) + 'x' + '</div>'.repeat(25))),
        dataFixture,
      ),
    ContractError,
  );
  assert.throws(
    () => validateProfile(changeSource(wrapper('<br/>'.repeat(501))), dataFixture),
    ContractError,
  );
  assert.throws(
    () => validateProfile(changeSource(wrapper('é'.repeat(20000))), dataFixture),
    ContractError,
  );
});
void test('tree schema independently rejects invalid input and cycles', () => {
  const valid = parseJsx(wrapper('hello'));
  for (const root of [
    { kind: 'element', tag: 'script', props: {}, children: [] },
    { kind: 'element', tag: 'div', props: { onClick: 'bad' }, children: [] },
    { kind: 'data', field: 'bio' },
    { kind: 'element', tag: 'br', props: {}, children: [{ kind: 'text', value: 'bad' }] },
  ])
    assert.throws(() => validateTree({ ...valid, root }), ContractError);
  const cyclic: ProfileNode = { kind: 'element', tag: 'div', props: {}, children: [] };
  cyclic.children.push(cyclic);
  assert.throws(() => validateTree({ ...valid, root: cyclic }), ContractError);
});
void test('byte limits and unique IDs apply to profile data', () => {
  assert.throws(() => validateData({ ...dataFixture, bio: 'é'.repeat(10000) }), ContractError);
  assert.throws(
    () =>
      validateData({
        ...dataFixture,
        sections: [...dataFixture.sections, ...dataFixture.sections],
      }),
    ContractError,
  );
  assert.throws(() => validateData({ ...dataFixture, token: 'secret' }), ContractError);
});
void test('unknown versions, unsupported files and capabilities fail explicitly', () => {
  assert.throws(
    () =>
      validateSource({
        ...sourceFixture,
        manifest: { ...sourceFixture.manifest, policyVersion: 'future' },
      }),
    ContractError,
  );
  assert.throws(
    () =>
      validateSource({ ...sourceFixture, files: { ...sourceFixture.files, '../outside': 'bad' } }),
    ContractError,
  );
  assert.throws(
    () => validateTree({ schemaVersion: 'future', root: { kind: 'text', value: 'x' } }),
    ContractError,
  );
});
void test('diagnostics use the same public shape with source locations', () => {
  try {
    validateProfile(changeSource(wrapper('{fetch("/x")}')), dataFixture);
    assert.fail('Expected rejection');
  } catch (error) {
    assert.ok(error instanceof ContractError);
    const diagnostic = error.diagnostics[0];
    assert.equal(diagnostic?.severity, 'error');
    assert.equal(diagnostic.code, 'SOURCE_EXPRESSION');
    assert.match(diagnostic.path, /^Profile.tsx:\d+:\d+$/);
    assert.ok(diagnostic.recommendation.length > 0);
  }
});
void test('artifacts are deterministic and bind to content, runtime and response headers', () => {
  const a = assembleArtifact(sourceFixture, dataFixture, runtime, 'https://app.example.test');
  const b = assembleArtifact(sourceFixture, dataFixture, runtime, 'https://app.example.test');
  assert.deepEqual(a, b);
  assert.equal(a.metadata.htmlDigest, sha256(a.html));
  assert.equal(a.metadata.headersDigest, sha256(canonicalJson(a.headers)));
  const changed = assembleArtifact(
    sourceFixture,
    { ...dataFixture, bio: 'Changed' },
    runtime,
    'https://app.example.test',
  );
  assert.notEqual(changed.metadata.htmlDigest, a.metadata.htmlDigest);
  assert.equal(changed.metadata.sourceDigest, a.metadata.sourceDigest);
  const differentHost = assembleArtifact(
    sourceFixture,
    dataFixture,
    runtime,
    'https://other.example.test',
  );
  assert.notEqual(differentHost.metadata.headersDigest, a.metadata.headersDigest);
  assert.equal(canonicalJson({ b: 2, a: 1 }), canonicalJson({ a: 1, b: 2 }));
});
void test('runtime version/integrity and framing origin cannot bypass assembly checks', () => {
  for (const bad of [
    { ...runtime, code: 'tampered' },
    { ...runtime, runtimeVersion: 'future' },
  ]) {
    assert.throws(
      () => assembleArtifact(sourceFixture, dataFixture, bad, 'https://app.example.test'),
      ContractError,
    );
  }
  for (const origin of [
    '*',
    'javascript:bad',
    'https://app.example.test/path',
    'https://app.example.test; script-src *',
  ]) {
    assert.throws(
      () => assembleArtifact(sourceFixture, dataFixture, runtime, origin),
      ContractError,
    );
  }
});
void test('HTML-shaped profile data cannot break out of the JSON script', () => {
  const artifact = assembleArtifact(
    sourceFixture,
    { ...dataFixture, bio: '</script><script>steal()</script>' },
    runtime,
    'https://app.example.test',
  );
  assert.ok(!artifact.html.includes('<script>steal()'));
  assert.ok(artifact.html.includes('\\u003c/script>'));
});
void test('spike-v0 fixture remains unchanged and requires explicit version adaptation', async () => {
  const value: unknown = JSON.parse(
    await readFile('runtime/profile-spike/fixtures/valid.json', 'utf8'),
  );
  assert.throws(() => validateSource(value), ContractError);
  const legacy = value as { files: SourceBundleFiles };
  const adapted = { ...sourceFixture, files: legacy.files };
  assert.equal(validateProfile(adapted, dataFixture).tree.schemaVersion, VERSIONS.tree);
});
type SourceBundleFiles = typeof sourceFixture.files;
