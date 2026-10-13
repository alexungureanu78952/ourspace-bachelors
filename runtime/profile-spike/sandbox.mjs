// No implementation in this spike qualifies as an arbitrary-code sandbox.
// Keep this gate closed; installing Docker is not enough to open it.
export function requireExecutionSandbox() {
  const error = new Error('Executable user JavaScript is disabled: no verified execution sandbox configured.');
  error.code = 'SANDBOX_UNAVAILABLE';
  throw error;
}

