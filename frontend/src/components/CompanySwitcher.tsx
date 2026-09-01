import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import "./CompanySwitcher.css";
import { authService } from "../modules/auth/api/authService";
import { syncCompanySelection } from "../modules/auth/syncCompanySelection";
import { useAuthStore } from "../store/authStore";
import type { AccessibleCompany } from "../types/access";

export function CompanySwitcher() {
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const [companies, setCompanies] = useState<AccessibleCompany[]>([]);
  const [switching, setSwitching] = useState(false);

  useEffect(() => {
    if (!user?.tenantId) return;
    let cancelled = false;
    (async () => {
      try {
        const list = await authService.listMyCompanies();
        if (!cancelled) setCompanies(list);
      } catch {
        if (!cancelled) setCompanies([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [user?.tenantId, user?.role]);

  if (!user?.tenantId || companies.length <= 1) {
    return null;
  }

  const currentLabel =
    companies.find((c) => c.companyId === user.companyId)?.displayName ??
    user.companyId?.slice(0, 8) ??
    "—";

  const onChange = async (companyId: string) => {
    if (!companyId || companyId === user.companyId) return;
    setSwitching(true);
    try {
      const session = await authService.switchCompany(companyId);
      await syncCompanySelection(session);
      navigate("/dashboard", { replace: true });
    } finally {
      setSwitching(false);
    }
  };

  return (
    <label className="company-switcher">
      <span className="company-switcher-label">Empresa</span>
      <select
        className="company-switcher-select"
        value={user.companyId ?? ""}
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
