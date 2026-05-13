import { accessService } from './accessService';
import type { SessionMenuGroupDto } from '../types/access';

/**
 * Vista previa del menú de sesión (mismo contrato que el sidebar de empresa).
 * Útil tras impersonación o para comprobar el árbol resuelto.
 */
export const menuPreviewService = {
  getSessionMenu: (): Promise<SessionMenuGroupDto[]> => accessService.getSessionMenu(),
};
