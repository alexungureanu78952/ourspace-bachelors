import { spawnSync } from 'node:child_process';

if (!process.env.OURSPACE_TEST_POSTGRES) {
  throw new Error(
    'OURSPACE_TEST_POSTGRES is required. On Windows run scripts/test-backend-local.ps1 for a disposable database.',
  );
}
const steps = [
  ['Locked restore and dependency audit', ['restore', 'OurSpace.slnx', '--locked-mode']],
  ['Backend build and analyzers', ['build', 'OurSpace.slnx', '--no-restore', '-warnaserror']],
  ['Backend formatting', ['format', 'OurSpace.slnx', '--verify-no-changes', '--no-restore']],
  [
    'Unit and PostgreSQL integration tests',
    ['test', 'OurSpace.slnx', '--no-build', '--no-restore'],
  ],
];
for (const [name, args] of steps) {
  console.log('\n' + name);
  const result = spawnSync('dotnet', args, { stdio: 'inherit' });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
}
