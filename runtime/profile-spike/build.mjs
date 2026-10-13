import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { buildRuntime, makeArtifact } from './artifact.mjs';
const bundle = JSON.parse(await readFile(new URL('./fixtures/valid.json', import.meta.url), 'utf8'));
const artifact = makeArtifact(bundle, { displayName: 'Alex', bio: 'Building a little corner of the web.' }, await buildRuntime());
await mkdir(new URL('./artifacts/', import.meta.url), { recursive: true });
await writeFile(new URL('./artifacts/profile.html', import.meta.url), artifact.html);
await writeFile(new URL('./artifacts/headers.json', import.meta.url), JSON.stringify({ 'Content-Security-Policy': artifact.csp, 'X-Content-Type-Options': 'nosniff', 'Referrer-Policy': 'no-referrer' }, null, 2));
console.log(JSON.stringify({ artifactBytes: Buffer.byteLength(artifact.html), profileBytes: artifact.profileBytes, note: 'Use the browser test server: opening HTML directly omits required response headers.' }));

