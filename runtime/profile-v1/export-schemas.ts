import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { z } from 'zod';
import { SCHEMAS, capabilityPolicy } from '../contracts/src/schemas.js';
import { VERSIONS } from '../contracts/src/policy.js';
import { canonicalJson } from '../artifacts/src/assemble.js';
const target = resolve('runtime/contracts/schemas');
const check = process.argv.includes('--check');
if (!check) await mkdir(target, { recursive: true });
const documents: Record<string, unknown> = Object.fromEntries(
  Object.entries(SCHEMAS).map(([name, schema]) => [name, z.toJSONSchema(schema)]),
);
documents['capabilities'] = { versions: VERSIONS, policy: capabilityPolicy };
for (const [name, document] of Object.entries(documents)) {
  const path = resolve(target, name + '.json');
  const content = canonicalJson(document) + '\n';
  if (check) {
    if ((await readFile(path, 'utf8')) !== content)
      throw new Error('Generated contract drift: ' + path);
  } else await writeFile(path, content);
}
