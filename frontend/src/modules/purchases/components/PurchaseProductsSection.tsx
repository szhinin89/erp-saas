import { PurchaseActionReason } from "../components/PurchaseActionReason";
import { PurchaseSearchFeedback } from "../components/PurchaseSearchFeedback";
import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPickerResultItem } from "../../../components/zh/ZHPickerResultItem";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { useI18n } from "../../../i18n/i18n";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";
import { PurchaseDetailLines, PurchaseExpandAllButton, usePurchaseLinesExpansion } from "./PurchaseLineCard";

type PurchasesCtx = ReturnType<typeof usePurchasesPage>;

export function PurchaseProductsSection({ ctx }: { ctx: PurchasesCtx }) {
  const { t } = useI18n();
  const expansion = usePurchaseLinesExpansion(ctx.lines);
  return (

              <div className="pdl-container">
                <div className="pdl-topbar pdl-topbar--split">
                  <h4 className="pdl-topbar__title">
                    <span
                      className="material-symbols-outlined pf-icon--22"
                    >
                      inventory_2
                    </span>
                    {t("purchases.lines.title", "Detalle de productos")}
                    {ctx.xmlLinesSummary.total > 0 && <ZHDataValue variant="muted">{t("purchases.pendingProducts.summary.total", { count: ctx.xmlLinesSummary.total })}</ZHDataValue>}
                    {ctx.xmlLinesSummary.warnings > 0 && <Badge variant="warning" label={t("purchases.pendingProducts.summary.warnings", { count: ctx.xmlLinesSummary.warnings })} />}
                  </h4>
                  <PurchaseActionReason reason={ctx.distributeCostDisabledReason}
                    action={t("purchases.distributeCost.trigger", "Distribuir flete/gasto")} />
                  <div className="pdl-toolbar">
                    <div className="pdl-toolbar__actions">
                  <ZHBtn
                    type="button"
                    variant="secondary"
                    onClick={() => ctx.setModalDistributeCost(true)}
                    disabled={!!ctx.distributeCostDisabledReason}
                  >
                    <span className="material-symbols-outlined zh-icon-md">
                      local_shipping
                    </span>
                    {t("purchases.distributeCost.trigger", "Distribuir flete/gasto")}
                  </ZHBtn>
                  {ctx.pendingProductSources.length > 0 && !ctx.fieldDisabled && (
                    <ZHBtn
                      type="button"
                      variant="primary"
                      onClick={() => ctx.openPendingProducts()}
                    >
                      <span className="material-symbols-outlined zh-icon-md">
                        playlist_add_check
                      </span>
                      {t("purchases.pendingProducts.trigger", {
                        count: ctx.pendingProductSources.length,
                      })}
                    </ZHBtn>
                  )}
                    </div>
                  <div className="pdl-search__wrap" ref={ctx.globalSearchRef}>
                    <div className="pdl-search__input-wrap">
                      <span className="material-symbols-outlined pdl-search__icon">
                        search
                      </span>
                      <ZhTextInput
                        className="pdl-search__input"
                        placeholder={t(
                          "purchases.lines.globalSearchPlaceholder",
                          "Buscar producto por nombre o SKU...",
                        )}
                        value={ctx.globalQuery}
                        onChange={(e) => {
                          ctx.setGlobalQuery(e.target.value);
                          ctx.setGlobalOpen(true);
                        }}
                        onFocus={() => {
                          if (ctx.globalQuery.length >= 2)
                            ctx.setGlobalOpen(true);
                        }}
                        onKeyDown={ctx.handleGlobalKeyDown}
                        disabled={ctx.fieldDisabled}
                      />
                    </div>
                    {ctx.globalOpen && ctx.globalQuery.length >= 2 && (
                      <div className="pdl-search__dropdown">
                        <PurchaseSearchFeedback status={ctx.globalSearchStatus} empty={ctx.globalResults.length === 0}
                          query={ctx.globalQuery} onRetry={ctx.retryGlobalSearch} />
                        {ctx.globalResults.map((item, i) => (
                            <ZHPickerResultItem
                              key={item.id}
                              selected={i === ctx.globalFocusIdx}
                              onClick={() => void ctx.addLineWithItem(item)}
                              onMouseEnter={() => ctx.setGlobalFocusIdx(i)}
                              title={
                                <div className="pdl-search__result-row">
                                  <span className="pdl-search__result-name">
                                    {item.shortName}
                                  </span>
                                  {item.baseSalePrice != null && (
                                    <span className="pdl-search__result-price">
                                      <ZHMoneyValue
                                        value={item.baseSalePrice}
                                        precision="salesUnitPrice"
                                      />
                                    </span>
                                  )}
                                </div>
                              }
                              subtitle={
                                <div className="pdl-search__result-row pdl-search__result-row--meta">
                                  <span className="pdl-search__result-sku">
                                    {item.sku}
                                  </span>
                                  <Badge
                                    variant="neutral"
                                    size="md"
                                    upper
                                    label={item.itemTypeName}
                                    className="pdl-search__result-type"
                                  />
                                </div>
                              }
                            />
                          ))}
                      </div>
                    )}
                  </div>
                  <ZhSelect
                    className="pdl-type-filter-select"
                    value={ctx.globalFilter}
                    onChange={(e) => {
                      ctx.setGlobalFilter(e.target.value);
                      if (ctx.globalQuery.length >= 2) ctx.setGlobalOpen(true);
                    }}
                  >
                    <option value="all">{t("common.all", "Todo")}</option>
                    {ctx.itemTypeOptions.map((it) => (
                      <option key={it.id} value={it.id}>
                        {it.name}
                      </option>
                    ))}
                  </ZhSelect>
                  <PurchaseExpandAllButton expansion={expansion} />
                  <ZHBtn
                    type="button"
                    variant="primary"
                    className="pdl-toolbar__add"
                    icon="add_circle"
                    onClick={ctx.addLine}
                    disabled={ctx.fieldDisabled}
                  >
                    {t("purchases.lines.addTitle", "Agregar producto o servicio")}
                  </ZHBtn>
                  </div>
                </div>

                {/* Line Cards */}
                <div className="pdl-lines" id="purchase-lines" tabIndex={-1}>
                  {(ctx.errors.lines?.message || ctx.errors.lines?.root?.message) && <ZHPageNotice variant="error" message={ctx.errors.lines.message ?? ctx.errors.lines.root?.message ?? ""} />}
                  <PurchaseDetailLines ctx={ctx} expansion={expansion} />
                  {ctx.lines.length === 0 && (
                    <div className="pdl-empty">
                      <span className="pdl-empty__icon-wrap">
                        <span className="material-symbols-outlined pdl-empty__icon">
                          add_shopping_cart
                        </span>
                      </span>
                      <div className="pdl-empty__body">
                        <p className="pdl-empty__title">
                          {t("purchases.lines.empty", "Sin productos agregados")}
                        </p>
                        <p className="pdl-empty__desc">
                          {t(
                            "purchases.lines.emptyDetail",
                            "Comience añadiendo ítems a esta transacción.",
                          )}
                        </p>
                      </div>
                    </div>
                  )}
                </div>
              </div>

  );
}
