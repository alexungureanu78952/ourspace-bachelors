import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { performance } from 'node:perf_hooks';
import { cpus, totalmem } from 'node:os';
import { gzipSync } from 'node:zlib';
import { build } from 'esbuild';
import { fileURLToPath } from 'node:url';
import { validateBundle, validateData } from './policy.mjs';
import { buildRuntime, makeArtifact } from './artifact.mjs';
const fixture = JSON.parse(await readFile(new URL('./fixtures/valid.json', import.meta.url), 'utf8'));
const directory = fileURLToPath(new URL('.', import.meta.url));
const iterations = 30;
const summary = samples => {
  const sorted = [...samples].sort((a, b) => a - b);
  return { samples: samples.length, medianMs: +sorted[Math.floor(sorted.length / 2)].toFixed(3), p95Ms: +sorted[Math.ceil(sorted.length * .95) - 1].toFixed(3) };
};
const coldStart = performance.now();
const runtime = await buildRuntime();
const coldRuntimeMs = performance.now() - coldStart;
const validation = [], shared = [], standalone = [];
let standaloneBytes = 0, profileBytes = 0;
for (let index = 0; index < iterations + 5; index++) {
  const bundle = structuredClone(fixture);
  // Distinct revisions: no source-result caching.
  bundle.files['Profile.tsx'] = bundle.files['Profile.tsx'].replace('OURSPACE', 'OURSPACE ' + index);
  const data = { displayName: 'Creator ' + index, bio: 'My personal profile.' };
  let start = performance.now();
  const compiled = validateBundle(bundle);
  validateData(data);
  const validationMs = performance.now() - start;
  start = performance.now();
  const artifact = makeArtifact(bundle, data, runtime);
  const sharedMs = performance.now() - start;
  profileBytes = artifact.profileBytes;
  // Profile-specific bundle comparison: include React in every artifact.
  // Build only trusted renderer + validated JSON, never execute supplied TSX.
  const entry = 'import {createRoot} from "react-dom/client"; import {renderNode} from "./renderer.mjs"; createRoot(document.getElementById("root")).render(renderNode(' + JSON.stringify(compiled.tree) + ',' + JSON.stringify(data) + '));';
  start = performance.now();
  const output = await build({ absWorkingDir: directory, stdin: { contents: entry, resolveDir: directory, sourcefile: 'trusted-entry.mjs' }, bundle: true, write: false, minify: true, platform: 'browser', format: 'iife', define: { 'process.env.NODE_ENV': '"production"' } });
  const standaloneMs = performance.now() - start;
  standaloneBytes = output.outputFiles[0].contents.length;
  if (index >= 5) { validation.push(validationMs); shared.push(sharedMs); standalone.push(standaloneMs); }
}
const output = {
  measuredAt: new Date().toISOString(), node: process.version, cpu: cpus()[0].model, logicalCpus: cpus().length, memoryGiB: +(totalmem() / 2**30).toFixed(1),
  fixtureSourceBytes: Buffer.byteLength(fixture.files['Profile.tsx']), coldSharedRuntimeBuildMs: +coldRuntimeMs.toFixed(3),
  warmValidation: summary(validation), sharedArtifactAssemblyIncludingValidation: summary(shared), profileSpecificTrustedBundle: summary(standalone),
  sharedRuntimeBytes: Buffer.byteLength(runtime), sharedRuntimeGzipBytes: gzipSync(runtime).length, perProfileJsonAndCssBytes: profileBytes, profileSpecificBundleBytes: standaloneBytes,
  limitations: ['Single tiny fixture; 5 warmups, 30 sequential samples; no production throughput claim.', 'No AI calls, type-checking of arbitrary programs, semantic evaluation, queueing, isolated-worker startup or CDN measurements.', 'Shared runtime is embedded in test HTML for CSP simplicity; external immutable delivery/cache is not implemented.', 'Profile-specific comparison bundles trusted renderer plus validated JSON; it does not measure unrestricted user React compilation.']
};
await mkdir(new URL('./artifacts/', import.meta.url), { recursive: true });
await writeFile(new URL('./artifacts/benchmark.json', import.meta.url), JSON.stringify(output, null, 2));
console.log(JSON.stringify(output, null, 2));

