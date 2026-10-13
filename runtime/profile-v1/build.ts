import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { assembleArtifact } from '../artifacts/src/assemble.js';
import { sourceFixture, dataFixture } from './fixture.js';
import { buildRuntime } from './runtime.js';
const artifact = assembleArtifact(
  sourceFixture,
  dataFixture,
  await buildRuntime(),
  'http://localhost:3000',
);
const target = resolve('runtime/profile-v1/artifacts');
await mkdir(target, { recursive: true });
await writeFile(resolve(target, 'profile.html'), artifact.html);
await writeFile(resolve(target, 'headers.json'), JSON.stringify(artifact.headers, null, 2));
await writeFile(resolve(target, 'metadata.json'), JSON.stringify(artifact.metadata, null, 2));
console.log('Built runtime-v1 fixture. Serve with its required headers; use npm run preview.');
