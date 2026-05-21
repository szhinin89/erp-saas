import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authService } from '../modules/auth/api/authService';
import { companyManagementService } from '../modules/company-management/api/companyManagementService';
import { syncSessionEntitlements } from '../lib/syncSessionEntitlements';
import { useAuthStore } from '../store/authStore';
import { usePermissionsStore } from '../store/permissionsStore';
import type { AccessibleCompany } from '../types/access';
import type { AuthResponse } from '../types/auth';

export function CompanySwitcher() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const login = useAuthStore((s) => s.login);
  const clearPermissions = usePermissionsStore((s) => s.clearPermissions);
  const [companies, setCompanies] = useState<AccessibleCompany[]>([]);
  const [switching, setSwitching] = useState(false);

  useEffect(() => {
    if (!user?.subscriberId || user.role === 'SuperAdmin') return;
    let cancelled = false;
    (async () => {
      try {
        const list = await authService.listMyCompanies();
        if (!cancelled) setCompanies(list);
      } catch {
        if (!cancelled) setCompanies([]);
      }
    })();
    return () => { cancelled = true; };
  }, [user?.subscriberId, user?.role]);

  if (!user?.subscriberId || user.role === 'SuperAdmin' || companies.length <= 1) {
    return null;
  }

  const currentLabel =
    companies.find((c) => c.companyId === user.companyId)?.displayName ??
    user.companyId?.slice(0, 8) ??
    '—';

  const onChange = async (companyId: string) => {
    if (!companyId || companyId === user.companyId) return;
    setSwitching(true);
    try {
      const session = await authService.switchCompany(companyId);
      const auth: AuthResponse = {
        userId: session.userId,
        fullName: session.fullName,
        email: session.email,
        role: session.role,
        subscriberId: session.subscriberId,
        companyId: session.companyId,
        token: session.token,
        planCode: session.planCode,
        enabledModules: session.enabledModules ?? [],
        refreshToken: session.refreshToken,
        refreshTokenExpiry: session.refreshTokenExpiry,
      };
      clearPermissions();
      login(auth);
      await syncSessionEntitlements();
      void companyManagementService.getCurrent();
      navigate('/dashboard', { replace: true });
    } finally {
      setSwitching(false);
    }
  };

  return (
    <label className="company-switcher">
      <span className="company-switcher-label">Empresa</span>
      <select
        className="company-switcher-select"
        value={user.companyId ?? ''}
        disabled={switching}
        onChange={(e) => void onChange(e.target.value)}
        aria-label="Cambiar empresa operativa"
      >
        {companies.map((c) => (
          <option key={c.companyId} value={c.companyId}>
            {c.displayName} ({c.ruc})
          </option>
        ))}
      </select>
      <span className="company-switcher-current subtle">{currentLabel}</span>
    </label>
  );
}
