import { useEffect, useRef } from "react";
import { useSearchParams } from "react-router-dom";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHTabBar } from "../../../components/zh/ZHTabBar";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { useI18n } from "../../../i18n/i18n";
import { usePurchasesPage, type Tab } from "../hooks/usePurchasesPage";
import { PurchaseFormFeedback } from "../components/PurchaseFormFeedback";
import { PurchaseSummaryPanel } from "../components/PurchaseSummaryPanel";
import { PurchaseListTab } from "../components/PurchaseListTab";
import { PurchaseHeaderCards } from "../components/PurchaseHeaderCards";
import { PurchaseExtrasPanel } from "../components/PurchaseExtrasPanel";
import { PurchaseProductsSection } from "../components/PurchaseProductsSection";
import { PaymentScheduleSection } from "../components/PaymentScheduleSection";
import { RetentionSection } from "../components/RetentionSection";
import { PurchaseFooterActions } from "../components/PurchaseFooterActions";
import { PurchaseDialogs } from "../components/PurchaseDialogs";
import "../../../styles/shared/items-catalog.css";
import "../../../styles/shared/erp-form-core.css";
import "../styles/purchases-invoice.css";

export function PurchasesPage() {
  const ctx = usePurchasesPage();
  const { t } = useI18n();
  const [searchParams] = useSearchParams();
  const openedFromParam = useRef(false);

  // Entrada cruzada desde el Kardex ("Ver documento origen"): abre la factura referida.
  // Entrada cruzada desde Recepción Electrónica ("Crear compra"): precarga un borrador desde el XML.
  useEffect(() => {
    const invoiceId = searchParams.get("invoiceId");
    const fromReceptionId = searchParams.get("fromReceptionId");
    const accessKey = searchParams.get("accessKey");
    if (invoiceId && !openedFromParam.current) {
      openedFromParam.current = true;
      void ctx.loadForEdit(invoiceId);
    } else if (fromReceptionId && !openedFromParam.current) {
      openedFromParam.current = true;
      void ctx.loadFromReception(fromReceptionId, accessKey);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams]);

  const tabs: { id: Tab; label: string; icon: string }[] = [
    { id: "listado", label: t("purchases.tabs.list", "Listado"), icon: "view_list" },
    {
      id: "nuevo",
      label: ctx.editing
        ? ctx.readOnly
          ? t("purchases.tabs.view", "Ver compra")
          : t("purchases.tabs.edit", "Editar compra")
        : t("purchases.tabs.new", "Nueva compra"),
      icon: ctx.editing ? (ctx.readOnly ? "visibility" : "edit") : "add_box",
    },
  ];

  return (
    <ErpPageTemplate
      title={ctx.tab === "nuevo" ? tabs[1].label : t("purchases.page.title", "Facturas de compra")}
      subtitle={t("purchases.page.subtitle", "Registro de facturas de compra de proveedores.")}
    >
      {ctx.tab === "listado" && <ZHTabBar
        tabs={tabs}
        activeTab={ctx.tab}
        onChange={(id) => {
          if (id !== "listado") {
            ctx.setTab(id);
            return;
          }
          // PURCHASE-DRAFT-RECOVERY D3 — nunca descartar cambios sin guardar en silencio.
          ctx.requestLeave(() => {
            ctx.resetForm();
            ctx.setTab(id);
          });
        }}
      />}

      {/* ══════════════════════════════════════════════════════════ LISTADO */}
      {ctx.tab !== "nuevo" && ctx.saveError && <ZHPageNotice variant="error" message={ctx.saveError} detail={ctx.saveErrorDetails.join("\n")} />}
      {ctx.tab === "listado" && <PurchaseListTab ctx={ctx} />}

      {ctx.tab === "nuevo" && (
        <div className="pf-purchase-form">
          <PurchaseFormFeedback ctx={ctx} leading={
            <ZHIconButton icon="arrow_back" variant="ghost"
              title={t("purchases.actions.backToList", "Volver al listado")}
              ariaLabel={t("purchases.actions.backToList", "Volver al listado")}
              onClick={() => ctx.requestLeave(() => {
                ctx.resetForm();
                ctx.setTab("listado");
              })} />
          } />
          <PurchaseHeaderCards ctx={ctx} />
          <PurchaseExtrasPanel ctx={ctx} />
          <div className="pf-main-layout">
            <div>
              <PurchaseProductsSection key={ctx.editing?.id ?? "new"} ctx={ctx} />
            </div>
            <div className="pf-plazos-resumen-grid">
              <div>
                <PaymentScheduleSection ctx={ctx} />
                {/* ZH-PURCHASE-RETENTION-CONFIRM-01 — en borrador se define la retención (se emite
                    al confirmar); confirmada, se consulta/gestiona la ya emitida. */}
                {ctx.editing &&
                  (ctx.editing.status === "Draft" || ctx.editing.status === "Confirmed") && (
                    <RetentionSection ctx={ctx} />
                  )}
              </div>
              <PurchaseSummaryPanel lines={ctx.lines} vatRatesMap={ctx.vatRatesMap}
                iceRatesMap={ctx.iceRatesMap} totals={ctx.summaryTotals} />
            </div>
          </div>
          <PurchaseFooterActions ctx={ctx} />
        </div>
      )}

      <PurchaseDialogs ctx={ctx} />
    </ErpPageTemplate>
  );
}
