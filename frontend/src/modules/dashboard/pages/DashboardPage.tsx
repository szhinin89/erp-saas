import { useNavigate } from "react-router-dom";
import { useI18n } from "../../../i18n/i18n";
import { useAuthStore } from "../../../store/authStore";
import { RuntimeModeBadge } from "../../../components/RuntimeModeBadge";
import { Badge } from "../../../components/PageShell";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { useDashboardKpis } from "../hooks/useDashboardData";
import "./DashboardPage.css";

function fmt(n: number | undefined, decimals = 2) {
  if (n === undefined || n === null) return "—";
  return `$${n.toFixed(decimals)}`;
}

function fmtN(n: number | undefined) {
  if (n === undefined || n === null) return "—";
  return String(n);
}

export function DashboardPage() {
  const { t, locale } = useI18n();
  const user = useAuthStore((s) => s.user);
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);
  const navigate = useNavigate();

  const kpis = useDashboardKpis();

  const today = new Date().toLocaleDateString(
    locale === "en" ? "en-US" : "es-ES",
    {
      weekday: "long",
      year: "numeric",
      month: "long",
      day: "numeric",
    },
  );

  const d = kpis.data;
  const loading = kpis.loading;
  const periodLabel = d ? `${d.month}/${d.year}` : "";

  return (
    <ErpPageTemplate
      key={`dashboard-${companySessionVersion}`}
      title={`${t("dashboard.welcome")} ${user?.fullName ?? ""}`.trim()}
      subtitle={today}
      kicker={t("dashboard.title")}
      action={<RuntimeModeBadge />}
      pageClassName="dsh-page"
    >
      {kpis.error && (
        <ZHPageNotice
          variant="error"
          message="Error al cargar KPIs"
          detail={kpis.error}
        />
      )}

      {/* ── KPI cards ── */}
      <div className="pg-kpis">
        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">payments</span>
            </div>
            {periodLabel && <Badge label={periodLabel} variant="blue" />}
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Ventas MTD</p>
            <p className="pg-kpi-value">{loading ? "…" : fmt(d?.salesMtd)}</p>
            <p className="subtle">
              {loading ? "" : `${fmtN(d?.invoicesMtd)} facturas`}
            </p>
          </div>
        </div>

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--warning">
              <span className="material-symbols-outlined">account_balance</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">CxC Pendiente</p>
            <p className="pg-kpi-value">
              {loading ? "…" : fmt(d?.pendingArTotal)}
            </p>
            {!loading && !!d?.overdueArTotal && (
              <div className="dsh-kpi-trend dsh-kpi-trend--warning">
                <span className="material-symbols-outlined">warning</span>
                <span>{fmt(d.overdueArTotal)} vencido</span>
              </div>
            )}
          </div>
        </div>

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--error">
              <span className="material-symbols-outlined">inventory_2</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t("dashboard.kpis.lowStock")}</p>
            <p className="pg-kpi-value">
              {loading ? "…" : fmtN(d?.lowStockSkuCount)}
            </p>
            <p className="subtle">
              {loading ? "" : `${fmtN(d?.outOfStockSkuCount)} sin stock`}
            </p>
          </div>
        </div>

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">shopping_cart</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">CxP Pendiente</p>
            <p className="pg-kpi-value">
              {loading ? "…" : fmt(d?.pendingApTotal)}
            </p>
            {!loading && !!d?.overdueApTotal && (
              <div className="dsh-kpi-trend dsh-kpi-trend--warning">
                <span className="material-symbols-outlined">warning</span>
                <span>{fmt(d.overdueApTotal)} vencido</span>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ── Aging charts + quick access ── */}
      <div className="dsh-body">
        <div className="dsh-charts-col">
          {/* AR Aging */}
          <div className="card card--xl">
            <div className="dsh-activity-head">
              <h2 className="dsh-section-title">Antigüedad CxC (AR)</h2>
            </div>
            <div className="dsh-chart-wrap"></div>
          </div>

          {/* AP Aging */}
          <div className="card card--xl">
            <div className="dsh-activity-head">
              <h2 className="dsh-section-title">Antigüedad CxP (AP)</h2>
            </div>
          </div>
        </div>

        <div className="dsh-right">
          <div className="card card--xl">
            <div className="card-header">
              <h2 className="dsh-section-title">
                {t("dashboard.quickAccess.title")}
              </h2>
            </div>
            <div className="dsh-card-body">
              <ZHBtn
                type="button"
                variant="secondary"
                className="dsh-quick-btn"
                onClick={() => navigate("/inventory/items")}
              >
                <span className="material-symbols-outlined dsh-quick-icon">
                  inventory
                </span>
                <div>
                  <p className="dsh-quick-label">
                    {t("dashboard.quickAccess.addItem")}
                  </p>
                  <p className="dsh-quick-sub">
                    {t("dashboard.quickAccess.addItem.sub")}
                  </p>
                </div>
              </ZHBtn>
            </div>
          </div>

          {/* YTD summary */}
          <div className="card card--xl">
            <div className="card-header">
              <h2 className="dsh-section-title">Resumen Anual</h2>
              {d && <Badge label={d.year} variant="blue" />}
            </div>
            <div className="dsh-card-body">
              <div className="dsh-summary-row">
                <span className="dsh-summary-label">Ventas YTD</span>
                <span className="dsh-summary-value">
                  {loading ? "…" : fmt(d?.salesYtd)}
                </span>
              </div>
              <div className="dsh-summary-row">
                <span className="dsh-summary-label">Facturas MTD</span>
                <span className="dsh-summary-value">
                  {loading ? "…" : fmtN(d?.invoicesMtd)}
                </span>
              </div>
              <div className="dsh-summary-row">
                <span className="dsh-summary-label">CxC vencida</span>
                <span className="dsh-summary-value dsh-summary-value--warn">
                  {loading ? "…" : fmt(d?.overdueArTotal)}
                </span>
              </div>
              <div className="dsh-summary-row">
                <span className="dsh-summary-label">CxP vencida</span>
                <span className="dsh-summary-value dsh-summary-value--warn">
                  {loading ? "…" : fmt(d?.overdueApTotal)}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <footer className="dsh-footer">
        <div className="dsh-footer-left">
          <span className="dsh-footer-brand">ZH Technologies</span>
          <span className="dsh-footer-copy">
            {t("dashboard.footer.rights")}
          </span>
        </div>
        <nav className="dsh-footer-links">
          <a href="#">{t("dashboard.footer.support")}</a>
          <a href="#">{t("dashboard.footer.docs")}</a>
          <a href="#">{t("dashboard.footer.privacy")}</a>
        </nav>
      </footer>
    </ErpPageTemplate>
  );
}
