import type { AxiosError } from 'axios';

/** MasterData no disponible (permiso, ruta, entorno) → facade puede usar legacy. */
export function isMasterDataFallbackError(err: unknown): boolean {
  const status = (err as AxiosError)?.response?.status;
  return status === 403 || status === 404 || status === 501;
}
