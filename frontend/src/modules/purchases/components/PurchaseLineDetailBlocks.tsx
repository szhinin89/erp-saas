import { PurchaseFieldError } from "../components/PurchaseFieldError";
import { Badge } from "../../../components/PageShell";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { useI18n } from "../../../i18n/i18n";
import { ItemMatchStatusBadge } from "../components/ItemMatchStatusBadge";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import { buildPurchaseLinePresentation } from "../utils/purchaseLinePresentation";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";

type PurchasesCtx = ReturnType<typeof usePurchasesPage>;

export function PurchaseLineDetailBlocks({ ctx, line: l, vm, readiness, detailId, detailErrors, packagingLevels, lineLocked, errorId, fieldMessage, onPackagingChange }: {
  ctx: PurchasesCtx; line: PurchaseLineFormValues; vm: ReturnType<typeof buildPurchaseLinePresentation>;
  readiness: PurchasesCtx["lineReadinessByKey"][number] | undefined; detailId: string;
  detailErrors: { path: string; message: string }[]; packagingLevels: NonNullable<PurchaseLineFormValues["context"]>["packagingLevels"];
  lineLocked: boolean; errorId: (field: string) => string; fieldMessage: (fields: string[]) => string | undefined;
  onPackagingChange: (packagingLevelId: string) => void;
}) {
  const { t } = useI18n();
  return (
      <div className="pdl-blocks" id={detailId}>
        <section className="pdl-block">
          <header className="pdl-block__header">
            <span
              className="material-symbols-outlined pdl-block__icon"
            >
              description
            </span>
            <h4 className="pdl-block__title zh-section-title">
              {t("purchases.lines.receivedProduct", "Producto recibido (XML)")}
              <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_XML_RECEIVED} />
            </h4>
          </header>
          {!vm.xml.hasOrigin && (
            <p className="pdl-block__hint">
              <ZHDataValue variant="muted">
                {t(
                  "purchases.lines.manualPurchaseHint",
                  "Compra manual — no proviene de recepción electrónica.",
                )}
              </ZHDataValue>
            </p>
          )}
          {vm.xml.hasOrigin && (<>
          {/* Cód. proveedor ya está en la cabecera cuando hay producto vinculado. */}
          <div className="pdl-ctx-col__badges">
            {!l.itemId && (
              <Badge
                variant="neutral"
                code
                label={`${t("purchases.lines.supplierCode", "Cód. proveedor")}: ${vm.xml.supplierCode}`}
              />
            )}
            <Badge
              variant={vm.xml.hasSupplierAuxCode ? "warning" : "neutral"}
              code
              label={`${t("purchases.lines.auxCode", "Cód. auxiliar")}: ${vm.xml.supplierAuxCode}`}
            />
          </div>
          <ZHDataValue variant="muted" className="pdl-ctx-col__desc">
            {vm.xml.description}
          </ZHDataValue>
          <div className="pdl-ctx-col__costs pdl-ctx-col__costs--grid">
            <ZHInfoRow
              label={<ZHFieldLabel size="sm">{t("purchases.lines.quantity", "Cantidad")}</ZHFieldLabel>}
              value={<ZHDataValue variant="numeric">{vm.xml.quantity}</ZHDataValue>}
            />
            <ZHInfoRow
              label={<ZHFieldLabel size="sm">{t("purchases.lines.price", "Precio")}</ZHFieldLabel>}
              value={<ZHDataValue variant="numeric">{vm.xml.unitPrice}</ZHDataValue>}
            />
            <ZHInfoRow
              label={<ZHFieldLabel size="sm">{t("purchases.lines.discount", "Descuento")}</ZHFieldLabel>}
              value={<ZHDataValue variant="numeric">{vm.xml.discount}</ZHDataValue>}
            />
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.xmlVatTaxableBase", "Base imp. IVA")}
                </ZHFieldLabel>
              }
              value={<ZHDataValue variant="numeric">{vm.xml.taxableBase}</ZHDataValue>}
            />
            <ZHInfoRow
              label={<ZHFieldLabel size="sm">IVA ({vm.xml.vatPercentage}%)</ZHFieldLabel>}
              value={<ZHDataValue variant="numeric">{vm.xml.taxValue}</ZHDataValue>}
            />
            {vm.xml.hasIce && (
              <ZHInfoRow
                label={<ZHFieldLabel size="sm">{t("purchases.lines.iceShort", "ICE")}</ZHFieldLabel>}
                value={<ZHDataValue variant="numeric">{vm.xml.iceValue}</ZHDataValue>}
              />
            )}
            {vm.xml.hasIrbpnr && (
              <ZHInfoRow
                label={<ZHFieldLabel size="sm">IRBPNR</ZHFieldLabel>}
                value={<ZHDataValue variant="numeric">{vm.xml.irbpnrValue}</ZHDataValue>}
              />
            )}
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.lineTotal", "Total línea")}
                </ZHFieldLabel>
              }
              value={<ZHDataValue variant="strong">{vm.xml.totalLine}</ZHDataValue>}
            />
          </div>
          {vm.xml.hasAdditionalFields && (
            <div className="pdl-additional-fields">
              <ZHFieldLabel size="sm">
                {t(
                  "purchases.lines.additionalFieldsTitle",
                  "Datos adicionales XML",
                )}
              </ZHFieldLabel>
              <div className="pdl-ctx-col__costs">
                {vm.xml.additionalFields.map((f, idx) => (
                  <ZHInfoRow
                    key={`${f.name}-${idx}`}
                    wide
                    label={<ZHFieldLabel size="sm">{f.name}</ZHFieldLabel>}
                    value={<ZHDataValue variant="numeric">{f.value}</ZHDataValue>}
                  />
                ))}
              </div>
            </div>
          )}
          </>)}
        </section>

        {/* 2. Con qué producto del sistema está relacionado — siempre visible, incluso sin producto aún. */}
        <section className="pdl-block">
          <header className="pdl-block__header">
            <span
              className="material-symbols-outlined pdl-block__icon"
            >
              badge
            </span>
            <h4 className="pdl-block__title zh-section-title">
              {t("purchases.lines.systemProduct", "Presentación e inventario")}
              <ZHFieldHelp helpKey={HELP_KEYS.PURCHASES_ITEM_MATCHING} />
            </h4>
            {vm.item.isLoading && (
              <span
                className="pdl-ctx-col__loading"
                title={t("purchases.lines.loadingContext", "Cargando contexto...")}
              >
                  <span
                  className="material-symbols-outlined pdl-ctx-col__loading-icon"
                >
                  hourglass_empty
                </span>
              </span>
            )}
            {vm.item.hasItem &&
              (vm.item.matchStatus || readiness?.status !== "READY") && (
                <span className="pdl-block__header-actions">
                  {vm.item.matchStatus ? (
                    <ItemMatchStatusBadge status={vm.item.matchStatus} />
                  ) : (
                    <Badge
                      variant="green"
                      label={t("purchases.lines.productLinked", "Producto vinculado")}
                    />
                  )}
                </span>
              )}
          </header>
          <div className="pdl-ctx-col__costs">
            {detailErrors.filter(issue => issue.path !== "packagingLevelId").map(issue => (
              <ZHField key={issue.path} density="compact"
                label={t(`purchases.validation.lineField.${issue.path}`, issue.path === "discountPct" ? "Descuento" : issue.path)}
                fieldError={issue.message}>{null}</ZHField>
            ))}
            {packagingLevels.length > 0 && (
              <div>
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.receivedPresentation", "Presentación recibida")}
                </ZHFieldLabel>
                <ZhSelect
                  density="compact"
                  value={l.packagingLevelId ?? ""}
                  aria-label={t("purchases.lines.receivedPresentation", "Presentación recibida")}
                  aria-invalid={!!fieldMessage(["packagingLevelId"])} aria-describedby={fieldMessage(["packagingLevelId"]) ? errorId("presentation") : undefined}
                  onChange={(e) => onPackagingChange(e.target.value)}
                  disabled={lineLocked}
                >
                  <option value="">
                    {t("purchases.lines.baseUnit", "Unidad base")}
                  </option>
                  {packagingLevels.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name}
                    </option>
                  ))}
                </ZhSelect>
              </div>
            )}
            <PurchaseFieldError id={errorId("presentation")} message={fieldMessage(["packagingLevelId"])} />
            {vm.registration.savedInBaseUnitNote && <ZHDataValue variant="muted">{vm.registration.savedInBaseUnitNote}</ZHDataValue>}
            {/* FASE 5 PURCHASE-UX — producto sin presentaciones: flujo normal en unidad base, sin
                controles de presentación innecesarios. */}
            {vm.item.hasItem && packagingLevels.length === 0 && !vm.inventory.hasPresentation && (
              <ZHDataValue variant="muted">
                {t(
                  "purchases.lines.noPresentationsBaseUnit",
                  "Producto sin presentaciones: se registra en unidad base.",
                )}
              </ZHDataValue>
            )}
            {/* La equivalencia ya se lee en la línea secundaria cuando hay presentación elegida. */}
            {vm.inventory.hasPresentation &&
              vm.inventory.conversionFactorValue !== 1 &&
              !(vm.registration.show && vm.registration.presentationDetail) && (
              <ZHInfoRow
                wide
                label={
                  <ZHFieldLabel size="sm">
                    {t("purchases.lines.conversionFactor", "Factor de conversión")}
                  </ZHFieldLabel>
                }
                value={
                  <ZHDataValue variant="numeric">
                    {vm.inventory.equivalenceDetail || vm.inventory.conversionFactorLabel}
                  </ZHDataValue>
                }
              />
            )}
            {vm.item.hasItem && vm.xml.hasOrigin && packagingLevels.length > 0 && (
              <div className="pdl-cost-wide">
                <ZHBtn
                  type="button"
                  variant="primary"
                  size="xs"
                  title={t(
                    "purchases.lines.saveSupplierPresentationHint",
                    "Recordar esta presentación para las próximas facturas de este proveedor.",
                  )}
                  disabled={
                    lineLocked ||
                    ctx.matchingKey === l._key ||
                    !l.packagingLevelId
                  }
                  onClick={() => void ctx.handleSaveSupplierPresentation(l._key)}
                >
                  {ctx.matchingKey === l._key
                    ? t(
                        "purchases.lines.savingSupplierPresentation",
                        "Guardando presentación...",
                      )
                    : t("purchases.lines.saveSupplierPresentation", "Guardar presentación")}
                </ZHBtn>
              </div>
            )}
          </div>
        </section>

        {/* 3. Impacto para el negocio — el margen % vive solo en la cabecera; aquí lo que no está
            arriba (stock, costos de referencia, precio de venta, descuento máximo). */}
        <section className="pdl-block pdl-block--muted">
          <header className="pdl-block__header">
            <span
              className="material-symbols-outlined pdl-block__icon"
            >
              insights
            </span>
            <h4 className="pdl-block__title zh-section-title">
              {t("purchases.lines.commercialInfo", "Información comercial")}
            </h4>
          </header>
          {!vm.commercial.hasContext ? (
            <p className="pdl-block__hint">
              <ZHDataValue variant="muted">
                {t("purchases.lines.noCommercialData", "Sin datos comerciales para esta línea.")}
              </ZHDataValue>
            </p>
          ) : (<>
          <div className="pdl-ctx-col__stock">
            <div className="pdl-stock-item">
              <Badge
                variant={vm.commercial.stock.isCritical ? "error" : "success"}
                size="md"
                label={vm.commercial.stock.statusLabel}
              />
              <ZHDataValue variant="numeric">{vm.commercial.stock.current}</ZHDataValue>
            </div>
            <div className="pdl-stock-item">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.availableShort", "Disp.")}
              </ZHFieldLabel>
              <ZHDataValue variant="numeric">{vm.commercial.stock.available}</ZHDataValue>
            </div>
            <div className="pdl-stock-item">
              <ZHFieldLabel size="sm">
                {t("purchases.lines.reserved", "Reserva")}
              </ZHFieldLabel>
              <ZHDataValue variant="numeric">{vm.commercial.stock.reserved}</ZHDataValue>
            </div>
          </div>
          <div className="pdl-ctx-col__costs pdl-ctx-col__costs--grid">
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.averageCost", "Costo promedio")}
                </ZHFieldLabel>
              }
              value={<ZHDataValue variant="numeric">{vm.commercial.costs.average}</ZHDataValue>}
            />
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.lastCost", "Último costo")}
                </ZHFieldLabel>
              }
              value={<ZHDataValue variant="numeric">{vm.commercial.costs.last}</ZHDataValue>}
            />
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.salePrice", "Precio de venta")}
                </ZHFieldLabel>
              }
              value={<ZHDataValue variant="numeric">{vm.commercial.profitability.pvp}</ZHDataValue>}
            />
            <ZHInfoRow
              label={
                <ZHFieldLabel size="sm">
                  {t("purchases.lines.maxDiscount", "Desc. máx.")}
                </ZHFieldLabel>
              }
              value={
                <ZHDataValue variant="numeric">
                  {vm.commercial.profitability.maxDiscountPercent}%
                </ZHDataValue>
              }
            />
          </div>
          </>)}
        </section>
      </div>
  );
}
