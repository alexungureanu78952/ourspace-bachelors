import { build } from 'esbuild';
import { fileURLToPath } from 'node:url';
import { VERSIONS } from '../contracts/src/policy.js';
import type { RuntimeDescriptor } from '../contracts/src/schemas.js';
import { sha256 } from '../artifacts/src/assemble.js';
export async function buildRuntime(): Promise<RuntimeDescriptor> {
  const result = await build({
    entryPoints: [fileURLToPath(new URL('../renderer/src/browser.js', import.meta.url))],
    bundle: true,
    write: false,
    minify: true,
    platform: 'browser',
    format: 'iife',
    define: { 'process.env.NODE_ENV': '"production"' },
  });
  const code = result.outputFiles[0]?.text;
  if (!code) throw new Error('Runtime build produced no output');
  return {
    runtimeVersion: VERSIONS.runtime,
    treeSchemaVersion: VERSIONS.tree,
    policyVersion: VERSIONS.policy,
    code,
    sha256: sha256(code),
  };
}
