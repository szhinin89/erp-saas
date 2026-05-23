import { accessService } from '../modules/auth/api/accessService';
import { entitlementsService } from '../modules/auth/api/entitlementsService';
import { usePermissionsStore } from '../store/permissionsStore';

/** Carga snapshot SaaS + permisos RBAC del perfil (única rutina post-login / switch-subscriber / switch-company). */
export async function syncSessionEntitlements(): Promise<void> {
  const store = usePermissionsStore.getState();
  store.setPermissionsSyncing(true);
  try {
    const [snap, perms] = await Promise.all([
      entitlementsService.getMe(),
      accessService.getMyPermissions().catch(() => null),
    ]);

    store.setEntitlementsSnapshot({
      permissions: perms?.permissions ?? [],
      planCode: snap.planCode,
      planName: snap.planName,
      enabledModules: snap.enabledModules,
      enabledFeatures: snap.enabledFeatures,
      limits: snap.limits,
      hasModuleRestrictions: snap.hasModuleRestrictions,
    });
  } finally {
    usePermissionsStore.getState().setPermissionsSyncing(false);
  }
}
