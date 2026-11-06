import { formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { useNavigate } from "react-router-dom";
import { useI18n } from "../../../i18n/i18n";
import { useAuthStore } from "../../../store/authStore";
import { RuntimeModeBadge } from "../../../components/RuntimeModeBadge";
import { Badge } from "../../../components/PageShell";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ReportKpiCard } from "../../../components/ReportPageTemplate";
import { useDashboardKpis } from "../hooks/useDashboardData";
import { formatLongDate } from "../../../lib/formatters/dateFormatters";
import { brandConfig, getCopyrightText } from "../../../shared/branding/brandConfig";
import "./DashboardPage.css";

function fmt(n: number | undefined, decimals: number) {
  if (n === undefined || n === null) return "—";
  return formatMoneyWithSymbol(n, decimals); // motor único (04E), sin toFixed
}

function fmtN(n: number | undefined) {
  if (n === undefined || n === null) return "—";
  return String(n);
}

export function DashboardPage() {
  const moneyDecimals = usePrecisionDecimals("money"); // ventas / CxC / CxP (04F)
  const { t, locale } = useI18n();
  const user = useAuthStore((s) => s.user);
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);
  const navigate = useNavigate();

  const kpis = useDashboardKpis();

  const today = formatLongDate(new Date(), locale);

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
          message={t("dashboard.kpis.loadError")}
          detail={kpis.error}
        />
      )}

      {/* ── KPI cards ── vertical explícito (UIUX-GLOBAL-KPI-DEFAULT-01): badge de período,
          trend y sub condicionales; las cuatro tarjetas deben compartir la misma altura. */}
      <div className="pg-kpis">
        <ReportKpiCard
          layout="vertical"
          icon="payments"
          tone="primary"
          badge={periodLabel ? <Badge label={periodLabel} variant="info" /> : undefined}
          label={t("dashboard.kpis.salesMonth")}
          value={loading ? "…" : fmt(d?.salesMtd, moneyDecimals)}
          sub={
            <p className="subtle">
              {loading ? "" : t("dashboard.kpis.invoiceCount", { count: fmtN(d?.invoicesMtd) })}
            </p>
          }
        />

        <ReportKpiCard
          layout="vertical"
          icon="account_balance"
          tone="warning"
          label={t("dashboard.kpis.receivables")}
          value={loading ? "…" : fmt(d?.pendingArTotal, moneyDecimals)}
          trend={
            !loading && !!d?.overdueArTotal
              ? {
                  icon: "warning",
                  label: t("dashboard.kpis.overdueAmount", { amount: fmt(d.overdueArTotal, moneyDecimals) }),
                  tone: "warning",
                }
              : undefined
          }
        />

        <ReportKpiCard
          layout="vertical"
          icon="inventory_2"
          tone="error"
          label={t("dashboard.kpis.lowStock")}
          value={loading ? "…" : fmtN(d?.lowStockSkuCount)}
          sub={
            <p className="subtle">
              {loading ? "" : t("dashboard.kpis.outOfStockCount", { count: fmtN(d?.outOfStockSkuCount) })}
            </p>
          }
        />

        <ReportKpiCard
          layout="vertical"
          icon="shopping_cart"
          tone="primary"
          label={t("dashboard.kpis.payables")}
          value={loading ? "…" : fmt(d?.pendingApTotal, moneyDecimals)}
          trend={
            !loading && !!d?.overdueApTotal
              ? {
                  icon: "warning",
                  label: t("dashboard.kpis.overdueAmount", { amount: fmt(d.overdueApTotal, moneyDecimals) }),
                  tone: "warning",
                }
              : undefined
          }
        />
      </div>

      {/* ── Aging charts + quick access ── */}
      <div className="dsh-body">
        <div className="dsh-charts-col">
          {/* Antigüedad de cuentas por cobrar */}
          <div className="card card--xl">
            <div className="dsh-activity-head">
              <h2 className="dsh-section-title">
                {t("dashboard.aging.receivables")}
              </h2>
            </div>
            <div className="dsh-chart-wrap">
              <p className="dsh-chart-placeholder">
                {t("dashboard.aging.noReceivables")}
              </p>
            </div>
          </div>

          {/* Antigüedad de cuentas por pagar */}
          <div className="card card--xl">
            <div className="dsh-activity-head">
              <h2 className="dsh-section-title">
                {t("dashboard.aging.payables")}
              </h2>
            </div>
            <div className="dsh-chart-wrap">
              <p className="dsh-chart-placeholder">
                {t("dashboard.aging.noPayables")}
              </p>
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
                onClick={() => navigate("/sales")}
              >
                <span className="material-symbols-outlined dsh-quick-icon">
                  point_of_sale
                </span>
                <div>
                  <p className="dsh-quick-label">
                    {t("dashboard.quickAccess.newSale")}
                  </p>
                  <p className="dsh-quick-sub">
                    {t("dashboard.quickAccess.newSale.sub")}
                  </p>
                </div>
              </ZHBtn>

              <ZHBtn
                type="button"
                variant="secondary"
                className="dsh-quick-btn"
                onClick={() => navigate("/purchases")}
              >
                <span className="material-symbols-outlined dsh-quick-icon">
                  shopping_cart
                </span>
                <div>
                  <p className="dsh-quick-label">
                    {t("dashboard.quickAccess.newPurchase")}
                  </p>
                  <p className="dsh-quick-sub">
                    {t("dashboard.quickAccess.newPurchase.sub")}
                  </p>
                </div>
              </ZHBtn>

              <ZHBtn
                type="button"
                variant="secondary"
                className="dsh-quick-btn"
                onClick={() => navigate("/products/items")}
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

              <ZHBtn
                type="button"
                variant="secondary"
                className="dsh-quick-btn"
                onClick={() => navigate("/customers")}
              >
                <span className="material-symbols-outlined dsh-quick-icon">
                  person_add
                </span>
                <div>
                  <p className="dsh-quick-label">
                    {t("dashboard.quickAccess.newCustomer")}
                  </p>
                  <p className="dsh-quick-sub">
                    {t("dashboard.quickAccess.newCustomer.sub")}
                  </p>
                </div>
              </ZHBtn>
            </div>
          </div>

          {/* Resumen anual */}
          <div className="card card--xl">
            <div className="card-header">
              <h2 className="dsh-section-title">{t("dashboard.summary.title")}</h2>
              {d && <Badge label={d.year} variant="info" />}
            </div>
            <div className="dsh-card-body">
              <div className="dsh-summary-row">
                <span className="dsh-summary-label">{t("dashboard.summary.salesYear")}</span>
                <span className="dsh-summary-value">
                  {loading ? "…" : fmt(d?.salesYtd, moneyDecimals)}
                </span>
              </div>
              <div className="dsh-summary-row">
                <span className="dsh-summary-label">{t("dashboard.summary.invoicesMonth")}</span>
                <span className="dsh-summary-value">
                  {loading ? "…" : fmtN(d?.invoicesMtd)}
                </span>
              </div>
              <div className="dsh-summary-row">
                <span className="dsh-summary-label">
                  {t("dashboard.summary.overdueReceivables")}
                </span>
                <span className="dsh-summary-value dsh-summary-value--warn">
                  {loading ? "…" : fmt(d?.overdueArTotal, moneyDecimals)}
                </span>
              </div>
              <div className="dsh-summary-row">
                <span className="dsh-summary-label">
                  {t("dashboard.summary.overduePayables")}
                </span>
                <span className="dsh-summary-value dsh-summary-value--warn">
                  {loading ? "…" : fmt(d?.overdueApTotal, moneyDecimals)}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <footer className="dsh-footer">
        <div className="dsh-footer-left">
          <span className="dsh-footer-brand">{brandConfig.companyName}</span>
          <span className="dsh-footer-copy">
            {getCopyrightText()}. {t("dashboard.rightsReserved")}
          </span>
        </div>
      </footer>
    </ErpPageTemplate>
  );
}
