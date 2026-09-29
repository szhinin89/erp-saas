/**
 * globalAdminAuthFacade — superficie pública de autenticación de la consola global
 * (admin-core: login de AdminGlobalCore y entrada a operar una empresa).
 *
 * Expone únicamente el login global, el cambio a operación de empresa y la navegación
 * post-login compartida con /login (una sola implementación de la rama de redirección).
 * Los módulos externos deben importar desde aquí, nunca directamente de auth/api/authService
 * ni de auth/completeLoginNavigation.
 */
import { authService } from "../api/authService";

export { completeLoginNavigation } from "../completeLoginNavigation";

export const globalAdminAuthFacade = {
  globalLogin: authService.globalLogin,
  operateCompany: authService.operateCompany,
};
