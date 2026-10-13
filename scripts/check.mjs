import { spawnSync } from 'node:child_process';
const steps = [
  ['Type checks', ['node_modules/typescript/bin/tsc', '--noEmit']],
  ['Lint', ['node_modules/eslint/bin/eslint.js', '.', '--max-warnings', '0']],
  ['Formatting', ['node_modules/prettier/bin/prettier.cjs', '--check', '.']],
  ['Compile', ['node_modules/typescript/bin/tsc']],
  ['Schema drift', ['.build/runtime/profile-v1/export-schemas.js', '--check']],
  ['Unit and contract tests', ['--test', '.build/runtime/profile-v1/test/unit/*.test.js']],
  ['Spike regression', ['--test', 'runtime/profile-spike/test/policy.test.mjs']],
  ['Browser tests', ['--test', '.build/runtime/profile-v1/test/browser/*.test.js']],
];
for (const [name, args] of steps) {
  console.log('\n' + name);
  const result = spawnSync(process.execPath, args, { stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
}
