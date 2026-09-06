import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { authService } from "../api/authService";
import { syncCompanySelection } from "../syncCompanySelection";
import { useAuthStore } from "../../../store/authStore";
import type { AccessibleCompany } from "../../../types/access";
import { useI18n } from "../../../i18n/i18n";
import { useDocumentTitle } from "../../../hooks/useDocumentTitle";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import "./CompanySelectPage.css";

const AVATAR_VARIANTS = ["primary", "secondary", "tertiary"] as const;

type TFn = (key: string, fallbackOrParams?: string | Record<string, string | number>) => string;

type StatusChip = {
  label: string;
  tone: "success" | "warning" | "neutral" | "error";
};

function getCompanyStatusChip(company: AccessibleCompany, t: TFn): StatusChip {
  if (!company.isActive) {
    return { label: t("subscriberSelect.status.inactive", "Inactiva"), tone: "error" };
  }
  switch (company.operationalStatus) {
    case "Suspended":
      return { label: t("subscriberSelect.status.suspended", "Suspendida"), tone: "warning" };
    case "PendingSetup":
      return {
        label: t("subscriberSelect.status.pendingSetup", "En configuración"),
        tone: "neutral",
      };
    case "Operational":
    default:
      return { label: t("subscriberSelect.status.operational", "Activa"), tone: "success" };
  }
}

function getCompanyBranchLabel(company: AccessibleCompany, t: TFn): string {
  const count = company.assignedBranchCount;
  if (count <= 0) {
    return t("subscriberSelect.noBranchesAssigned", "Sin sucursales asignadas");
  }
  if (count === 1) {
    return t("subscriberSelect.branchCount.singular", "1 sucursal");
  }
  return t("subscriberSelect.branchCount.plural", { count });
}

function getCompanyMetaItems(company: AccessibleCompany, t: TFn): string[] {
  const items: string[] = [];
  if (company.role) items.push(`${t("subscriberSelect.roleLabel", "Rol:")} ${company.role}`);
  items.push(getCompanyBranchLabel(company, t));
  if (company.taxRegime) items.push(company.taxRegime);
  items.push(
    company.isAccountingRequired
      ? t("subscriberSelect.accounting.required", "Lleva contabilidad")
      : t("subscriberSelect.accounting.notRequired", "No lleva contabilidad"),
  );
  return items;
}

export function CompanySelectPage() {
  const navigate = useNavigate();
  const { t } = useI18n();
  useDocumentTitle(t("subscriberSelect.title", "Selecciona una empresa"));
  const user = useAuthStore((s) => s.user);

  const [companies, setCompanies] = useState<AccessibleCompany[]>([]);
  const [q, setQ] = useState("");
  const [loading, setLoading] = useState(false);
  const [pendingId, setPendingId] = useState<string | null>(null);
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
        if (!cancelled)
          setError(t("subscriberSelect.loadError", "No se pudieron cargar las empresas."));
      } finally {
        if (!cancelled) setLoadingList(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [t]);

  const filtered = useMemo(() => {
    const query = q.trim().toLowerCase();
    if (!query) return companies;
    return companies.filter((x) =>
      `${x.displayName} ${x.legalName} ${x.ruc} ${x.companyId} ${x.role} ${x.taxRegime ?? ""} ${x.operationalStatus}`
        .toLowerCase()
        .includes(query),
    );
  }, [q, companies]);

  const choose = async (companyId: string) => {
    setError("");
    setLoading(true);
    setPendingId(companyId);
    try {
      const session = await authService.switchCompany(companyId);
      await syncCompanySelection(session);
      navigate("/dashboard", { replace: true });
    } catch (err: unknown) {
      const ax = err as {
        response?: { status?: number; data?: { message?: string } };
      };
      if (ax?.response?.status === 401) {
        navigate("/login", { replace: true });
        return;
      }
      setError(
        ax?.response?.data?.message ??
          t("subscriberSelect.error.default", "No se pudo cambiar de empresa."),
      );
    } finally {
      setLoading(false);
      setPendingId(null);
    }
  };

  if (!user?.tenantId) {
    return (
      <div className="zh-auth-bg">
        <div className="zh-auth-wrapper">
          <div className="zh-auth-card">
            <div className="zh-auth-card-body zh-auth-card-body--center">
              <p className="zh-auth-card-desc">
                {t(
                  "subscriberSelect.missing",
                  "Inicie sesión y seleccione un suscriptor primero.",
                )}
              </p>
              <ZHBtn variant="primary" onClick={() => navigate("/login")}>
                {t("subscriberSelect.back", "Ir al login")}
              </ZHBtn>
            </div>
          </div>
        </div>
      </div>
    );
  }

  const countLabel = t(
    companies.length === 1
      ? "subscriberSelect.count.singular"
      : "subscriberSelect.count.plural",
    companies.length === 1 ? "empresa disponible" : "empresas disponibles",
  );

  return (
    <div className="zh-auth-bg">
      <div className="zh-auth-bg-orb zh-auth-bg-orb--tr" aria-hidden="true" />
      <div className="zh-auth-bg-orb zh-auth-bg-orb--bl" aria-hidden="true" />
      <div className="zh-auth-bg-grid" aria-hidden="true" />

      <p className="cs-bg-copy cs-bg-copy--left" aria-hidden="true">
        {t("subscriberSelect.bgLeft", "Soluciones que impulsan tu negocio")}
        <span className="cs-bg-copy-accent" />
      </p>
      <p className="cs-bg-copy cs-bg-copy--right" aria-hidden="true">
        {t("subscriberSelect.bgRightLabel", "ERP")}
        <br />
        ZH Technologies
        <span className="cs-bg-copy-accent" />
      </p>

      <div className="zh-auth-wrapper cs-wrapper">
        <header className="zh-auth-brand">
          <div className="zh-auth-brand-icon" aria-hidden="true">
            <span className="material-symbols-outlined">apartment</span>
          </div>
          <h1 className="zh-auth-brand-name">ZH Technologies</h1>
        </header>

        <div className="zh-auth-card cs-card">
          <div className="zh-auth-card-header">
            <h2 className="zh-auth-card-title">
              {t("subscriberSelect.title", "Selecciona una empresa")}
            </h2>
            <p className="zh-auth-card-desc">
              {t(
                "subscriberSelect.heroSubtitle",
                "Seleccione la empresa operativa para continuar",
              )}
            </p>
          </div>

          <div className="cs-summary">
            <div className="cs-summary-item">
              <span className="cs-summary-value">
                {!loadingList && !error ? (
                  <>
                    {companies.length} {countLabel}
                  </>
                ) : (
                  countLabel
                )}
              </span>
              <span className="cs-summary-hint">
                {t(
                  "subscriberSelect.summaryHint",
                  "Elija la empresa con la que desea trabajar hoy.",
                )}
              </span>
            </div>
            <div className="cs-summary-item cs-summary-item--secure">
              <span className="cs-summary-value">
                <span className="material-symbols-outlined" aria-hidden="true">
                  lock
                </span>
                {t("subscriberSelect.secureAccess.title", "Acceso seguro")}
              </span>
              <span className="cs-summary-hint">
                {t(
                  "subscriberSelect.secureAccess.text",
                  "Está usando una sesión autenticada.",
                )}
              </span>
            </div>
          </div>

          <div className="zh-input-group cs-search">
            <span className="zh-input-group__prefix" aria-hidden="true">
              <span className="material-symbols-outlined">search</span>
            </span>
            <ZhTextInput
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder={t(
                "subscriberSelect.searchPlaceholder",
                "Buscar por nombre o RUC",
              )}
              aria-label={t(
                "subscriberSelect.searchPlaceholder",
                "Buscar por nombre o RUC",
              )}
              disabled={loading || loadingList}
            />
          </div>

          {error && (
            <ZHPageNotice
              variant="error"
              message={t("common.errorPrefix")}
              detail={error}
            />
          )}

          {renderCompanyArea()}

          <p className="cs-footer-help">
            {t(
              "subscriberSelect.footerHelp",
              "Si no encuentra su empresa, contacte al administrador.",
            )}
          </p>
        </div>
      </div>
    </div>
  );

  function renderCompanyArea() {
    if (loadingList) {
      return (
        <div className="cs-loading">
          <span className="cs-spinner" aria-hidden="true" />
          <span>{t("subscriberSelect.loading", "Cargando empresas…")}</span>
        </div>
      );
    }

    if (companies.length === 0 && !error) {
      return (
        <ZHPageNotice
          variant="info"
          message={t(
            "subscriberSelect.noCompanies.title",
            "No tienes empresas asignadas",
          )}
          detail={t(
            "subscriberSelect.noCompanies.detail",
            "Contacta a tu administrador para que te asigne acceso a una empresa.",
          )}
        />
      );
    }

    return (
      <div className="cs-list" role="list">
        {filtered.map((x, i) => {
          const statusChip = getCompanyStatusChip(x, t);
          const metaItems = getCompanyMetaItems(x, t);
          return (
            <div key={x.companyId} className="zh-entity-item cs-company-card" role="listitem">
              <div
                className={`zh-avatar zh-avatar--${AVATAR_VARIANTS[i % AVATAR_VARIANTS.length]}`}
                aria-hidden="true"
              >
                {x.displayName.charAt(0).toUpperCase()}
              </div>
              <div className="zh-entity-item-info">
                <span className="zh-entity-item-name-row">
                  <span className="zh-entity-item-name">{x.displayName}</span>
                  <span className={`cs-chip cs-chip--${statusChip.tone}`}>
                    {statusChip.label}
                  </span>
                </span>
                <span className="zh-entity-item-sub mono">
                  {t("subscriberSelect.rucLabel", "RUC:")} {x.ruc}
                </span>
                <span className="cs-company-meta">
                  {metaItems.map((item) => (
                    <span key={item} className="cs-chip cs-chip--neutral">
                      {item}
                    </span>
                  ))}
                </span>
              </div>
              <div className="zh-entity-item-right">
                <ZHBtn
                  variant="primary"
                  disabled={loading}
                  onClick={() => choose(x.companyId)}
                >
                  {pendingId === x.companyId ? (
                    <span className="zh-auth-submit-spinner" aria-hidden="true" />
                  ) : (
                    <span className="material-symbols-outlined" aria-hidden="true">
                      arrow_forward
                    </span>
                  )}
                  {t("subscriberSelect.enter", "Entrar")}
                </ZHBtn>
              </div>
            </div>
          );
        })}
        {filtered.length === 0 && (
          <div className="cs-empty">
            <span className="material-symbols-outlined" aria-hidden="true">
              search_off
            </span>
            <span>
              {t(
                "subscriberSelect.emptySearch",
                "No se encontraron empresas con ese criterio.",
              )}
            </span>
          </div>
        )}
      </div>
    );
  }
}
