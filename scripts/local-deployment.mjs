import { spawnSync } from 'node:child_process';
import { mkdirSync, writeFileSync, readFileSync, cpSync } from 'node:fs';
import { resolve } from 'node:path';

const action = process.argv[2] ?? 'up';
const run = (command, args, options = {}) => {
  const result = spawnSync(command, args, { stdio: 'inherit', ...options });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(command + ' failed (exit ' + result.status + ').');
  return result.stdout;
};
const docker = (...args) => run('docker', ['compose', '-f', 'infra/compose.yml', ...args]);
if (action === 'up') {
  run('dotnet', ['run', '--project', 'tools/OurSpace.Deployment', '--', 'initialize']);
  docker('up', '-d', '--build', '--wait', '--wait-timeout', '180');
  console.log(
    'Application: https://app.localhost:7443 | Accounts: https://identity.localhost:7443 | Mail: http://localhost:8025',
  );
} else if (action === 'stop') {
  docker('stop');
} else if (action === 'status') {
  docker('ps', '--all');
} else if (action === 'backup') {
  const directory = resolve('.local/backups/' + new Date().toISOString().replaceAll(':', '-'));
  mkdirSync(directory, { recursive: true, mode: 0o700 });
  docker('stop', 'bff', 'identity', 'profiles');
  try {
    for (const service of ['profiles', 'identity', 'bff']) {
      const data = run(
        'docker',
        [
          'compose',
          '-f',
          'infra/compose.yml',
          'exec',
          '-T',
          service + '-db',
          'pg_dump',
          '-U',
          'ourspace_' + service,
          '-d',
          'ourspace_' + service,
          '-Fc',
        ],
        { stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 256 * 1024 * 1024 },
      );
      writeFileSync(directory + '/' + service + '.dump', data);
    }
    for (const service of ['identity', 'bff']) {
      const data = run(
        'docker',
        [
          'run',
          '--rm',
          '--mount',
          'type=volume,src=ourspace-local_' + service + '-keys,dst=/keys,readonly',
          'mcr.microsoft.com/dotnet/aspnet:10.0.13@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac',
          'tar',
          '-C',
          '/keys',
          '-czf',
          '-',
          '.',
        ],
        { stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 16 * 1024 * 1024 },
      );
      writeFileSync(directory + '/' + service + '-keys.tar.gz', data);
    }
    cpSync('.local/deployment', directory + '/secrets', { recursive: true, errorOnExist: true });
    writeFileSync(
      directory + '/manifest.json',
      JSON.stringify(
        {
          version: 1,
          createdAt: new Date().toISOString(),
          services: ['profiles', 'identity', 'bff'],
        },
        null,
        2,
      ) + '\n',
    );
    console.log('Sensitive local backup created: ' + directory);
  } finally {
    docker('up', '-d', '--no-deps', '--wait', 'identity', 'profiles', 'bff');
  }
} else if (action === 'verify-backup') {
  const directory = resolve(process.argv[3] ?? '');
  const root = resolve('.local/backups') + '/';
  if (!(directory.replaceAll('\\', '/') + '/').startsWith(root.replaceAll('\\', '/')))
    throw new Error('Choose a backup under .local/backups.');
  const manifest = JSON.parse(readFileSync(directory + '/manifest.json', 'utf8'));
  if (manifest.version !== 1) throw new Error('Unsupported backup version.');
  for (const service of ['profiles', 'identity', 'bff']) {
    const name = 'ourspace_restore_' + crypto.randomUUID().replaceAll('-', '');
    if (!/^ourspace_restore_[a-f0-9]{32}$/.test(name)) throw new Error('Invalid scratch database.');
    docker('exec', '-T', service + '-db', 'createdb', '-U', 'ourspace_' + service, name);
    try {
      run(
        'docker',
        [
          'compose',
          '-f',
          'infra/compose.yml',
          'exec',
          '-T',
          service + '-db',
          'pg_restore',
          '-U',
          'ourspace_' + service,
          '-d',
          name,
          '--no-owner',
          '--exit-on-error',
        ],
        {
          stdio: ['pipe', 'inherit', 'inherit'],
          input: readFileSync(directory + '/' + service + '.dump'),
        },
      );
      const table = { profiles: 'Profiles', identity: 'AspNetUsers', bff: 'BffSessions' }[service];
      docker(
        'exec',
        '-T',
        service + '-db',
        'psql',
        '-U',
        'ourspace_' + service,
        '-d',
        name,
        '-v',
        'ON_ERROR_STOP=1',
        '-c',
        'SELECT count(*) FROM "' + table + '";',
      );
    } finally {
      docker('exec', '-T', service + '-db', 'dropdb', '-U', 'ourspace_' + service, name);
    }
  }
  console.log(
    'All three dumps restored and queried in isolated scratch databases; running databases were untouched.',
  );
} else throw new Error('Use up, stop, status, backup or verify-backup.');
