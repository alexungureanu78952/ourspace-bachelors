import { createHash } from 'node:crypto';
import { fail } from '../../contracts/src/diagnostics.js';
import { VERSIONS } from '../../contracts/src/policy.js';
import { artifactMetadataSchema, runtimeDescriptorSchema } from '../../contracts/src/schemas.js';
import type { ArtifactMetadata, RuntimeDescriptor } from '../../contracts/src/schemas.js';
import { parseContract } from '../../validation/src/contracts.js';
import { validateProfile } from '../../validation/src/profile.js';
export const sha256 = (value: string): string =>
  createHash('sha256').update(value, 'utf8').digest('hex');
const cspHash = (value: string): string =>
  createHash('sha256').update(value, 'utf8').digest('base64');
export function canonicalJson(value: unknown): string {
  if (value === null || typeof value !== 'object') {
    const encoded = JSON.stringify(value);
    if (encoded === undefined) fail('CANONICAL_VALUE', 'Unsupported canonical value');
    return encoded;
  }
  if (Array.isArray(value)) return '[' + value.map(canonicalJson).join(',') + ']';
  return (
    '{' +
    Object.entries(value)
      .sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0))
      .map(([key, item]) => JSON.stringify(key) + ':' + canonicalJson(item))
      .join(',') +
    '}'
  );
}
export interface ProfileArtifact {
  html: string;
  headers: Readonly<Record<string, string>>;
  metadata: ArtifactMetadata;
}
export function assembleArtifact(
  source: unknown,
  data: unknown,
  runtimeInput: unknown,
  framingOrigin: string,
): ProfileArtifact {
  const profile = validateProfile(source, data);
  const runtime: RuntimeDescriptor = parseContract(runtimeDescriptorSchema, runtimeInput);
  if (sha256(runtime.code) !== runtime.sha256)
    fail('RUNTIME_INTEGRITY', 'Runtime digest does not match');
  if (/<\/script/i.test(runtime.code))
    fail('RUNTIME_ENCODING', 'Runtime contains an unsafe inline-script delimiter');
  let origin: URL;
  try {
    origin = new URL(framingOrigin);
  } catch {
    fail('FRAME_ORIGIN', 'Expected an exact trusted framing origin');
  }
  if (
    !['https:', 'http:'].includes(origin.protocol) ||
    origin.origin !== framingOrigin ||
    origin.username ||
    origin.password
  )
    fail('FRAME_ORIGIN', 'Expected an exact HTTP(S) origin');
  const payload = canonicalJson({ tree: profile.tree, data: profile.data }).replaceAll(
    '<',
    '\\u003c',
  );
  const html =
    '<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>OurSpace profile</title><style>' +
    profile.css +
    '</style></head><body><div id="root"></div><script id="profile-data" type="application/json">' +
    payload +
    '</script><script>' +
    runtime.code +
    '</script></body></html>';
  const headers = {
    'Content-Type': 'text/html; charset=utf-8',
    'Content-Security-Policy':
      "default-src 'none'; script-src 'sha256-" +
      cspHash(runtime.code) +
      "'; style-src 'sha256-" +
      cspHash(profile.css) +
      "'; connect-src 'none'; img-src 'none'; font-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors " +
      framingOrigin +
      '; sandbox allow-scripts',
    'X-Content-Type-Options': 'nosniff',
    'Referrer-Policy': 'no-referrer',
    'Cache-Control': 'no-store',
  };
  const metadata = parseContract(artifactMetadataSchema, {
    artifactFormatVersion: VERSIONS.artifact,
    sourceSchemaVersion: VERSIONS.source,
    treeSchemaVersion: VERSIONS.tree,
    dataSchemaVersion: VERSIONS.data,
    runtimeVersion: VERSIONS.runtime,
    policyVersion: VERSIONS.policy,
    sourceDigest: sha256(canonicalJson(profile.source)),
    runtimeDigest: runtime.sha256,
    htmlDigest: sha256(html),
    headersDigest: sha256(canonicalJson(headers)),
  });
  return { html, headers, metadata };
}
