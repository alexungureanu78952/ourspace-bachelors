import { createServer } from 'node:http';
import type { Server } from 'node:http';
import { assembleArtifact } from '../artifacts/src/assemble.js';
import type { ProfileArtifact } from '../artifacts/src/assemble.js';
import { sourceFixture, dataFixture } from './fixture.js';
import { buildRuntime } from './runtime.js';
function listen(server: Server): Promise<string> {
  return new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      if (!address || typeof address === 'string') {
        reject(new Error('Missing local address'));
        return;
      }
      resolve('http://127.0.0.1:' + String(address.port));
    });
  });
}
function close(server: Server): Promise<void> {
  server.closeAllConnections();
  return new Promise((resolve, reject) =>
    server.close((error) => (error ? reject(error) : resolve())),
  );
}
export async function startPreview(data: unknown = dataFixture): Promise<{
  origin: string;
  contentOrigin: string;
  requests: string[];
  artifact: ProfileArtifact;
  close: () => Promise<void>;
}> {
  const runtime = await buildRuntime();
  const requests: string[] = [];
  let artifact: ProfileArtifact | undefined;
  let contentOrigin = '';
  const content = createServer((request, response) => {
    requests.push(request.url ?? '');
    if (request.url !== '/profile' || !artifact) {
      response.writeHead(404);
      response.end();
      return;
    }
    response.writeHead(200, artifact.headers);
    response.end(artifact.html);
  });
  const app = createServer((_request, response) => {
    response.writeHead(200, {
      'Content-Type': 'text/html; charset=utf-8',
      'Set-Cookie': 'session=fixture-secret; HttpOnly; SameSite=Strict; Path=/',
    });
    response.end(
      '<!doctype html><html lang="en"><head><title>OurSpace runtime-v1 preview</title></head><body><iframe title="Profile" sandbox="allow-scripts" style="width:100%;height:700px;border:0" src="' +
        contentOrigin +
        '/profile"></iframe></body></html>',
    );
  });
  try {
    contentOrigin = await listen(content);
    const origin = await listen(app);
    artifact = assembleArtifact(sourceFixture, data, runtime, origin);
    return {
      origin,
      contentOrigin,
      requests,
      artifact,
      close: async () => {
        await Promise.all([close(app), close(content)]);
      },
    };
  } catch (error) {
    if (app.listening) await close(app);
    if (content.listening) await close(content);
    throw error;
  }
}
