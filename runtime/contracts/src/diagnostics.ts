export interface Diagnostic {
  code: string;
  severity: 'error';
  message: string;
  path: string;
  recommendation: string;
}
export class ContractError extends Error {
  readonly diagnostics: readonly Diagnostic[];
  constructor(
    code: string,
    message: string,
    path = '$',
    recommendation = 'Use the supported runtime contract.',
  ) {
    super(message);
    this.name = 'ContractError';
    this.diagnostics = [{ code, severity: 'error', message, path, recommendation }];
  }
}
export function fail(code: string, message: string, path = '$'): never {
  throw new ContractError(code, message, path);
}
