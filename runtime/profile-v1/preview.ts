import { startPreview } from './server.js';
const preview = await startPreview();
console.log('Open ' + preview.origin + ' — local fixture only; Ctrl+C stops preview.');
let stopping = false;
const stop = (): void => {
  if (stopping) return;
  stopping = true;
  void preview.close().catch((error: unknown) => {
    console.error(error);
    process.exitCode = 1;
  });
};
process.on('SIGINT', stop);
process.on('SIGTERM', stop);
