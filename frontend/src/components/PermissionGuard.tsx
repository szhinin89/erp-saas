import type { ReactNode } from 'react';
import { usePermissionsUi } from '../access/usePermissionsUi';

type Props = {
  /** Permission key to check, e.g. 'sales.invoices.view' */
  permission: string;
  /** Rendered when the user HAS the permission. */
  children: ReactNode;
  /** Optional fallback rendered when the user LACKS the permission.
   *  Defaults to null (nothing rendered). */
  fallback?: ReactNode;
};

/**
 * UI-only: renders children when backend permission snapshot includes the key.
 * See `frontend/src/access/README.md`.
 */
export function PermissionGuard({ permission, children, fallback = null }: Props) {
  const { canShow } = usePermissionsUi();
  return canShow(permission) ? <>{children}</> : <>{fallback}</>;
}

type MultiProps = {
  /** All listed permissions must be present. */
  permissions: string[];
  /** 'all' (default) = user must have every key. 'any' = user needs at least one. */
  mode?: 'all' | 'any';
  children: ReactNode;
  fallback?: ReactNode;
};

export function MultiPermissionGuard({ permissions, mode = 'all', children, fallback = null }: MultiProps) {
  const { canShow } = usePermissionsUi();
  const allowed =
    mode === 'all' ? permissions.every((p) => canShow(p)) : permissions.some((p) => canShow(p));
  return allowed ? <>{children}</> : <>{fallback}</>;
}
