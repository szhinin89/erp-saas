import { accessService } from '../services/accessService';
import { entitlementsService } from '../services/entitlementsService';
import { usePermissionsStore } from '../store/permissionsStore';

/** Carga snapshot SaaS + permisos RBAC del perfil (única rutina post-login / switch-subscriber). */
export async function syncSessionEntitlements(): Promise<void> {
  const [snap, perms] = await Promise.all([
    entitlementsService.getMe(),
    accessService.getMyPermissions().catch(() => null),
  ]);

  usePermissionsStore.getState().setEntitlementsSnapshot({
    permissions: perms?.permissions ?? [],
    planCode: snap.planCode,
    planName: snap.planName,
    enabledModules: snap.enabledModules,
    enabledFeatures: snap.enabledFeatures,
    limits: snap.limits,
    hasModuleRestrictions: snap.hasModuleRestrictions,
  });
}
