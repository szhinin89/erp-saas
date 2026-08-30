import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { authService } from "../api/authService";
import { bumpCompanyOperationalSession } from "../../../lib/session/companySession";
import { logDevSessionContext } from "../../../lib/session/devSessionLog";
import { useAuthStore } from "../../../store/authStore";
import type { AuthResponse } from "../../../types/auth";
import type { AccessibleCompany } from "../../../types/access";
import { useI18n } from "../../../i18n/i18n";
import { useDocumentTitle } from "../../../hooks/useDocumentTitle";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHBtn } from "../../../components/zh/ZHForm";
import "./CompanySelectPage.css";

const AVATAR_VARIANTS = ["primary", "secondary", "tertiary"] as const;

export function CompanySelectPage() {
  const navigate = useNavigate();
  const { t } = useI18n();
  useDocumentTitle(t("subscriberSelect.title", "Selecciona una empresa"));
  const login = useAuthStore((s) => s.login);
  const user = useAuthStore((s) => s.user);

  const [companies, setCompanies] = useState<AccessibleCompany[]>([]);
  const [q, setQ] = useState("");
  const [loading, setLoading] = useState(false);
  const [loadingList, setLoadingList] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoadingList(true);
      setError("");
      try {
        const list = await authService.listMyCompanies();
        if (!cancelled) setCompanies(list);
      } catch {
        if (!cancelled) setError("No se pudieron cargar las empresas.");
      } finally {
        if (!cancelled) setLoadingList(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const filtered = useMemo(() => {
    const query = q.trim().toLowerCase();
    if (!query) return companies;
    return companies.filter((x) =>
      `${x.displayName} ${x.legalName} ${x.ruc} ${x.companyId}`
        .toLowerCase()
        .includes(query),
    );
  }, [q, companies]);

  const choose = async (companyId: string) => {
    setError("");
    setLoading(true);
    try {
      const session = await authService.switchCompany(companyId);
      const auth: AuthResponse = {
        userId: session.userId,
        fullName: session.fullName,
        username: session.username,
        email: session.email,
        role: session.role,
        tenantId: session.tenantId,
        companyId: session.companyId,
        token: session.token,
        refreshToken: session.refreshToken,
        refreshTokenExpiry: session.refreshTokenExpiry,
      };
      login(auth);
      bumpCompanyOperationalSession();
      logDevSessionContext("switch-company");
      navigate("/dashboard", { replace: true });
    } catch (err: unknown) {
      const ax = err as {
        response?: { status?: number; data?: { message?: string } };
      };
      if (ax?.response?.status === 401) {
        navigate("/login", { replace: true });
        return;
      }
      setError(ax?.response?.data?.message ?? "No se pudo cambiar de empresa.");
    } finally {
      setLoading(false);
    }
  };

  if (!user?.tenantId) {
    return (
      <div className="zh-auth-bg">
        <div className="zh-auth-wrapper ts-wrapper">
          <div className="ts-card">
            <div className="ts-card-body">
              <p>Inicie sesión y seleccione un suscriptor primero.</p>
              <ZHBtn variant="primary" onClick={() => navigate("/login")}>
                Ir al login
              </ZHBtn>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="zh-auth-bg">
      <div className="zh-auth-bg-orb zh-auth-bg-orb--tr" aria-hidden="true" />
      <div className="zh-auth-bg-orb zh-auth-bg-orb--bl" aria-hidden="true" />
      <div className="zh-auth-bg-grid" aria-hidden="true" />

      <div className="zh-auth-wrapper ts-wrapper">
        <header className="zh-auth-brand">
          <div className="zh-auth-brand-icon" aria-hidden="true">
            <span className="material-symbols-outlined">apartment</span>
          </div>
          <h1 className="zh-auth-brand-name">ZH Technologies</h1>
          <p className="zh-auth-brand-sub">Seleccione la empresa operativa</p>
        </header>

        <div className="ts-card">
          <div className="ts-card-body">
            <div className="ts-card-head">
              <h2 className="ts-title">Empresa operativa</h2>
              <span className="ts-count">
                {companies.length}{" "}
                {companies.length === 1 ? "empresa" : "empresas"}
              </span>
            </div>

            <input
              className="ts-search"
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Buscar por nombre o RUC"
              disabled={loading || loadingList}
            />

            {error && (
              <ZHPageNotice
                variant="error"
                message={t("common.errorPrefix")}
                detail={error}
              />
            )}

            {loadingList ? (
              <p className="ts-empty">Cargando empresas…</p>
            ) : (
              <div className="ts-list" role="list">
                {filtered.map((x, i) => (
                  <div
                    key={x.companyId}
                    className="zh-entity-item"
                    role="listitem"
                  >
                    <div
                      className={`zh-avatar zh-avatar--${AVATAR_VARIANTS[i % AVATAR_VARIANTS.length]}`}
                      aria-hidden="true"
                    >
                      {x.displayName.charAt(0).toUpperCase()}
                    </div>
                    <div className="zh-entity-item-info">
                      <span className="zh-entity-item-name">
                        {x.displayName}
                      </span>
                      <span className="zh-entity-item-sub mono">{x.ruc}</span>
                    </div>
                    <div className="zh-entity-item-right">
                      <ZHBtn
                        variant="primary"
                        size="sm"
                        disabled={loading}
                        onClick={() => choose(x.companyId)}
                      >
                        Entrar
                      </ZHBtn>
                    </div>
                  </div>
                ))}
                {filtered.length === 0 && !loadingList && (
                  <p className="ts-empty">No se encontraron empresas</p>
                )}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
