import { build } from 'esbuild';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { validateBundle, validateData } from './policy.mjs';
const root = fileURLToPath(new URL('.', import.meta.url));
export const digest = value => createHash('sha256').update(value).digest('base64');
export async function buildRuntime() {
  const result = await build({ absWorkingDir: root, entryPoints: ['browser-entry.mjs'], bundle: true, write: false, minify: true, platform: 'browser', format: 'iife', define: { 'process.env.NODE_ENV': '"production"' } });
  return result.outputFiles[0].text;
}
export function makeArtifact(bundle, data, runtime) {
  const compiled = validateBundle(bundle);
  validateData(data);
  const payload = JSON.stringify({ tree: compiled.tree, data }).replaceAll('<', '\\u003c');
  const csp = "default-src 'none'; script-src 'sha256-" + digest(runtime) + "'; style-src 'sha256-" + digest(compiled.css) + "'; connect-src 'none'; img-src 'none'; font-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors *; sandbox allow-scripts";
  return {
    html: '<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><style>' + compiled.css + '</style></head><body><div id="root"></div><script id="profile-data" type="application/json">' + payload + '</script><script>' + runtime.replaceAll('</script', '<\\/script') + '</script></body></html>',
    csp, tree: compiled.tree, css: compiled.css,
    // JSON + CSS are per profile; runtime is identical across profiles.
    profileBytes: Buffer.byteLength(payload) + Buffer.byteLength(compiled.css)
  };
}

