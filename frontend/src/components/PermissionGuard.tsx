import type { ReactNode } from 'react';
import { usePermissionsStore } from '../store/permissionsStore';

type Props = {
  /** Permission key to check, e.g. 'ventas.invoice.view' */
  permission: string;
  /** Rendered when the user HAS the permission. */
  children: ReactNode;
  /** Optional fallback rendered when the user LACKS the permission.
   *  Defaults to null (nothing rendered). */
  fallback?: ReactNode;
};

/**
 * Conditionally renders children based on a single permission key.
 *
 * Usage — hide a section:
 *   <PermissionGuard permission="ventas.invoice.view">
 *     <InvoiceList />
 *   </PermissionGuard>
 *
 * Usage — show a disabled state instead:
 *   <PermissionGuard
 *     permission="ventas.invoice.create"
 *     fallback={<button disabled>Nueva factura</button>}
 *   >
 *     <button onClick={openModal}>Nueva factura</button>
 *   </PermissionGuard>
 */
export function PermissionGuard({ permission, children, fallback = null }: Props) {
  const hasPerm = usePermissionsStore(s => s.has);
  return hasPerm(permission) ? <>{children}</> : <>{fallback}</>;
}

// ── Multi-permission variant ─────────────────────────────────────────────────

type MultiProps = {
  /** All listed permissions must be present. */
  permissions: string[];
  /** 'all' (default) = user must have every key. 'any' = user needs at least one. */
  mode?: 'all' | 'any';
  children: ReactNode;
  fallback?: ReactNode;
};

/**
 * Guards that require multiple permissions simultaneously.
 *
 * Usage — require both view AND create:
 *   <MultiPermissionGuard permissions={['ventas.invoice.view', 'ventas.invoice.create']}>
 *     ...
 *   </MultiPermissionGuard>
 */
export function MultiPermissionGuard({ permissions, mode = 'all', children, fallback = null }: MultiProps) {
  const hasPerm = usePermissionsStore(s => s.has);
  const allowed = mode === 'all'
    ? permissions.every(p => hasPerm(p))
    : permissions.some(p => hasPerm(p));
  return allowed ? <>{children}</> : <>{fallback}</>;
}
