import { useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { SupplierPicker } from "../components/SupplierPicker";
import { ProductPicker } from "../components/ProductPicker";
import type { ProductProfile } from "../components/ProductPicker";
import { ItemEditorModal } from "../../../components/items/ItemEditorModal/ItemEditorModal";
import type { ItemCreatedResult } from "../../../components/items/ItemEditorModal/types";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhNumberInput } from "../../../components/zh/inputs/ZhNumberInput";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhTextarea } from "../../../components/zh/inputs/ZhTextarea";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { formatMoney, formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import {
  ZHConfirmModal,
  ZHPromptModal,
} from "../../../components/zh/ZHConfirmModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import {
  lineNet,
  calcLineTax,
  roundToTotalAmount,
} from "../utils/purchaseCalc";
import { usePurchasesPage, type Tab } from "../hooks/usePurchasesPage";
import { useI18n } from "../../../i18n/i18n";
import {
  ItemMatchStatusBadge,
  useViewMatchedItem,
} from "../components/ItemMatchStatusBadge";
import { CreateItemFromReceptionLineModal } from "../components/CreateItemFromReceptionLineModal";
import type {
  PurchaseReceptionLineMatch,
  ItemMatchStatus,
} from "../api/purchaseReceptionService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import {
  buildPurchaseLinePresentation,
  type LineStatusTone,
} from "../utils/purchaseLinePresentation";
import "../../../styles/shared/items-catalog.css";
import "../../../styles/shared/erp-form-core.css";
import "../styles/purchases-invoice.css";

export function PurchasesPage() {
  const ctx = usePurchasesPage();
  const navigate = useNavigate();
  const { t } = useI18n();
  const [searchParams] = useSearchParams();
  const openedFromParam = useRef(false);

  // Entrada cruzada desde el Kardex ("Ver documento origen"): abre la factura referida.
  // Entrada cruzada desde Recepción Electrónica ("Crear compra"): precarga un borrador desde el XML.
  useEffect(() => {
    const invoiceId = searchParams.get("invoiceId");
    const fromReceptionId = searchParams.get("fromReceptionId");
    if (invoiceId && !openedFromParam.current) {
      openedFromParam.current = true;
      void ctx.loadForEdit(invoiceId);
    } else if (fromReceptionId && !openedFromParam.current) {
      openedFromParam.current = true;
      void ctx.loadFromReception(fromReceptionId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams]);

  const tabs: { id: Tab; label: string; icon: string }[] = [
    { id: "listado", label: "Listado", icon: "view_list" },
    {
      id: "nuevo",
      label: ctx.editing ? "Editar Compra" : "Nueva Compra",
      icon: ctx.editing ? "edit" : "add_box",
    },
  ];

  return (
    <ErpPageTemplate
      title="Facturas de Compra"
      subtitle="Gestión de compras en borrador."
    >
      <div className="prd-tabs">
        {tabs.map((t) => (
          <button
            key={t.id}
            className={`prd-tab-btn ${ctx.tab === t.id ? "prd-tab-btn--active" : ""}`}
            onClick={() => {
              if (t.id === "listado") ctx.resetForm();
              ctx.setTab(t.id);
            }}
          >
            <span
              className="material-symbols-outlined zh-icon-lg"
            >
              {t.icon}
            </span>
            {t.label}
          </button>
        ))}
      </div>

      {/* ══════════════════════════════════════════════════════════ LISTADO */}
      {ctx.tab === "listado" && (
        <div className="prd-section">
          <div className="pg-table-controls zh-mb-16">
            <ZhTextInput
              placeholder="Buscar por número de factura..."
              value={ctx.listSearchInput}
              onChange={(e) => ctx.setListSearchInput(e.target.value)}
            />
            <ZhSelect
              value={ctx.listStatus}
              onChange={(e) => ctx.setListStatus(e.target.value)}
            >
              <option value="">Todos los estados</option>
              <option value="Draft">Borrador</option>
              <option value="Confirmed">Confirmada</option>
              <option value="Cancelled">Anulada</option>
            </ZhSelect>
            <ZHBtn
              variant="secondary"
              onClick={ctx.fetchList}
              disabled={ctx.listLoading}
            >
              <span
                className="material-symbols-outlined zh-icon-lg"
              >
                refresh
              </span>
            </ZHBtn>
          </div>
          {ctx.listLoading ? (
            <p>Cargando...</p>
          ) : (
            <table className="table table--compact table--neutral">
              <thead>
                <tr>
                  <th>Nro. Factura</th>
                  <th>Fecha</th>
                  <th className="zh-text-align-center">Líneas</th>
                  <th>Estado</th>
                  <th className="zh-text-align-center">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {ctx.listItems.map((inv) => (
                  <tr key={inv.id}>
                    <td className="pf-invoice-number">
                      {inv.invoiceNumber}
                    </td>
                    <td>{formatDate(inv.issueDate)}</td>
                    <td className="zh-text-align-center">{inv.lineCount}</td>
                    <td>
                      <Badge
                        variant={
                          inv.status === "Draft"
                            ? "warning"
                            : inv.status === "Confirmed"
                              ? "success"
                              : "error"
                        }
                        label={inv.status}
                      />
                    </td>
                    <td className="zh-text-align-center">
                      <ZHIconButton
                        icon="edit"
                        variant="ghost"
                        title="Editar"
                        onClick={() => void ctx.loadForEdit(inv.id)}
                      />
                      {inv.status === "Confirmed" && (
                        <ZHIconButton
                          icon="history"
                          variant="ghost"
                          title="Ver Movimiento de Inventario"
                          onClick={() =>
                            navigate(
                              `/inventory/kardex?docId=${inv.id}&docType=PurchaseInvoice`,
                            )
                          }
                        />
                      )}
                    </td>
                  </tr>
                ))}
                {ctx.listItems.length === 0 && (
                  <tr>
                    <td colSpan={5} className="zh-table-empty">
                      Sin compras registradas.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          )}
          {!ctx.listLoading && ctx.listTotal > ctx.listPageSize && (
            <div className="pf-pagination">
              <span className="pf-pagination__summary">
                Página {ctx.listPage} de{" "}
                {Math.max(1, Math.ceil(ctx.listTotal / ctx.listPageSize))} —{" "}
                {ctx.listTotal} compra(s)
              </span>
              <div className="pf-pagination__actions">
                <ZHBtn
                  type="button"
                  variant="secondary"
                  disabled={ctx.listPage <= 1}
                  onClick={() => ctx.setListPage((p) => p - 1)}
                >
                  Anterior
                </ZHBtn>
                <ZHBtn
                  type="button"
                  variant="secondary"
                  disabled={
                    ctx.listPage >= Math.ceil(ctx.listTotal / ctx.listPageSize)
                  }
                  onClick={() => ctx.setListPage((p) => p + 1)}
                >
                  Siguiente
                </ZHBtn>
              </div>
            </div>
          )}
        </div>
      )}

      {/* ══════════════════════════════════════════════════════════ NUEVO / EDITAR */}
      {ctx.tab === "nuevo" && (
        <div>
          {/* Error/Validation Banners */}
          {ctx.saveError && (
            <ZHPageNotice variant="error" message={ctx.saveError} />
          )}
          {ctx.receptionProcessingNotice && (
            <ZHPageNotice
              variant="warning"
              message={ctx.receptionProcessingNotice}
            />
          )}
          {ctx.errors.supplierId && (
            <ZHPageNotice
              variant="error"
              message={ctx.errors.supplierId.message ?? ""}
            />
          )}
          {ctx.errors.lines && (
            <ZHPageNotice
              variant="error"
              message={
                ctx.errors.lines.message ?? ctx.errors.lines.root?.message ?? ""
              }
            />
          )}

          {ctx.readOnly && (
            <ZHPageNotice
              variant={
                ctx.editing?.status === "Cancelled" ? "error" : "success"
              }
              message={
                ctx.editing?.status === "Cancelled"
                  ? `Compra anulada el ${formatDate(ctx.editing.cancelledAt)}. Motivo: ${ctx.editing.cancelReason ?? "Sin motivo"}`
                  : "Compra confirmada — solo lectura."
              }
            />
          )}

          {/* ── SECTION 1: Header Cards ── */}
          <div className="pf-header-cards-row">
            {/* Datos del Proveedor */}
            <div className="pf-mini-card">
              <h4 className="pf-mini-card__title">
                <span className="material-symbols-outlined pf-mini-card__icon">
                  storefront
                </span>
                Datos del Proveedor
              </h4>
              <div className="pf-mini-card__body">
                <ZHField density="compact" label="RUC o Nombre" required>
                  <SupplierPicker
                    value={ctx.formWatch.supplierId || null}
                    onChange={ctx.handleSupplierChange}
                    disabled={ctx.fieldDisabled}
                  />
                </ZHField>
                {ctx.supplierProfile && (
                  <ZHField density="compact" label="Razón Social">
                    <div className="pf-mini-card__readonly">
                      {ctx.supplierProfile.name}
                    </div>
                  </ZHField>
                )}
                {ctx.supplierProfile && (
                  <div className="pf-mini-card__badges">
                    {ctx.supplierProfile.isRequiredToKeepAccounting ? (
                      <Badge
                        variant="success"
                        label={
                          <>
                            <span className="material-symbols-outlined pf-badge__icon">
                              check_circle
                            </span>
                            Obligado Contabilidad
                          </>
                        }
                      />
                    ) : (
                      <Badge
                        variant="error"
                        label={
                          <>
                            <span className="material-symbols-outlined pf-badge__icon">
                              cancel
                            </span>
                            No Obligado
                          </>
                        }
                      />
                    )}
                    {ctx.supplierProfile.config?.isRetentionExempt && (
                      <Badge
                        variant="warning"
                        label={
                          <>
                            <span className="material-symbols-outlined pf-badge__icon">
                              shield
                            </span>
                            Exento Retención
                          </>
                        }
                      />
                    )}
                  </div>
                )}
              </div>
            </div>

            {/* Datos del Documento */}
            <div className="pf-mini-card">
              <h4 className="pf-mini-card__title">
                <span className="material-symbols-outlined pf-mini-card__icon">
                  description
                </span>
                Datos del Documento
              </h4>
              <div className="pf-mini-card__body">
                <ZHField
                  density="compact"
                  label="Tipo de Documento"
                  required
                  fieldError={ctx.errors.docTypeCode?.message}
                >
                  <ZhSelect
                    value={ctx.formWatch.docTypeCode}
                    onChange={(e) =>
                      ctx.setValue("docTypeCode", e.target.value)
                    }
                    disabled={ctx.fieldDisabled}
                  >
                    {ctx.sriDocTypes.map((d) => (
                      <option key={d.code} value={d.code}>
                        {d.code} — {d.name}
                      </option>
                    ))}
                    {ctx.sriDocTypes.length === 0 && (
                      <option value="01">01 — FACTURA</option>
                    )}
                  </ZhSelect>
                </ZHField>
                <div className="pf-mini-card__row">
                  <ZHField
                    density="compact"
                    label="Nro. Documento"
                    required
                    fieldError={ctx.errors.invoiceNumber?.message}
                  >
                    <ZhTextInput
                      {...ctx.register("invoiceNumber")}
                      placeholder="001-001..."
                      maxLength={30}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                  <ZHField
                    density="compact"
                    label="Fecha Emisión"
                    required
                    fieldError={ctx.errors.issueDate?.message}
                  >
                    <ZhDateInput
                      {...ctx.register("issueDate")}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                </div>
              </div>
            </div>

            {/* Condiciones */}
            <div className="pf-mini-card">
              <h4 className="pf-mini-card__title">
                <span className="material-symbols-outlined pf-mini-card__icon">
                  handshake
                </span>
                Condiciones
              </h4>
              <div className="pf-mini-card__body">
                <ZHField density="compact" label="Forma de Pago SRI">
                  <ZhSelect
                    value={ctx.formWatch.sriPaymentMethodCode}
                    onChange={(e) =>
                      ctx.setValue("sriPaymentMethodCode", e.target.value)
                    }
                    disabled={ctx.fieldDisabled}
                  >
                    <option value="">— Seleccionar —</option>
                    {ctx.sriPaymentMethods.map((m) => (
                      <option key={m.code} value={m.code}>
                        {m.code} — {m.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
                {/* taxSupportCode: código de metadato documental SRI (Sustento
                    Tributario) — no participa en cálculo de impuestos/retenciones,
                    solo se persiste como dato del comprobante. Migración visual
                    únicamente, ver reporte 14B-2. */}
                <ZHField density="compact" label="Sustento Tributario">
                  <ZhSelect
                    value={ctx.formWatch.taxSupportCode}
                    onChange={(e) =>
                      ctx.setValue("taxSupportCode", e.target.value)
                    }
                    disabled={ctx.fieldDisabled}
                  >
                    <option value="">— Seleccionar —</option>
                    {ctx.sriTaxSupports.map((s) => (
                      <option key={s.code} value={s.code}>
                        {s.code} — {s.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
                <ZHField density="compact" label="Condición de Pago">
                  {/* paymentTermId: NO migrado — ver reporte 14B-2. onChange
                      dispara ctx.handlePaymentTermChange(), que regenera el
                      cronograma de cuotas — dominio excluido explícitamente
                      de este bloque. */}
                  <select
                    value={ctx.formWatch.paymentTermId}
                    onChange={(e) =>
                      ctx.handlePaymentTermChange(e.target.value)
                    }
                    disabled={ctx.fieldDisabled}
                  >
                    <option value="">— Del proveedor —</option>
                    {ctx.paymentTermsList
                      .filter((p) => p.isActive)
                      .map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name} ({p.summary})
                        </option>
                      ))}
                  </select>
                </ZHField>
              </div>
            </div>
          </div>

          {/* ── Información Electrónica (colapsable) ── */}
          <div className="pf-collapsible">
            <button
              type="button"
              className="pf-collapsible__toggle"
              onClick={() => ctx.setShowElectronic((v) => !v)}
            >
              <span
                className="material-symbols-outlined zh-icon-lg"
              >
                qr_code_2
              </span>
              <span>Información Electrónica</span>
              <span
                className={`material-symbols-outlined pf-collapsible__chevron ${ctx.showElectronic ? "pf-collapsible__chevron--open" : ""}`}
              >
                expand_more
              </span>
              {!ctx.showElectronic &&
                (ctx.formWatch.accessKey ||
                  ctx.formWatch.authorizationNumber) && (
                  <Badge
                    variant="info"
                    label="Con datos"
                    className="pf-collapsible__status"
                  />
                )}
            </button>
            {ctx.showElectronic && (
              <div className="pf-collapsible__body">
                <div className="pf-mini-card__row pf-mini-card__row--3col">
                  <ZHField density="compact" label="Clave de Acceso">
                    <ZhTextInput
                      {...ctx.register("accessKey")}
                      placeholder="49 dígitos"
                      maxLength={49}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                  <ZHField density="compact" label="Nro. Autorización">
                    <ZhTextInput
                      {...ctx.register("authorizationNumber")}
                      maxLength={49}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                  {/* authorizationDate: type="datetime-local" (fecha + hora) —
                      ZhDateInput fuerza type="date" (sin hora); no es
                      equivalente, se excluye de este bloque. Ver reporte 14B-2. */}
                  <ZHField density="compact" label="Fecha y Hora Autorización">
                    <input
                      type="datetime-local"
                      {...ctx.register("authorizationDate")}
                      disabled={ctx.fieldDisabled}
                    />
                  </ZHField>
                </div>
              </div>
            )}
          </div>

          <div className="pf-header-cards-row pf-header-cards-row--mid">
            <div className="pf-mini-card">
              <h4 className="pf-mini-card__title">
                <span className="material-symbols-outlined pf-mini-card__icon">
                  inventory
                </span>
                Logística &amp; Totales
              </h4>
              <div className="pf-mini-card__body">
                {/* globalWarehouseId: NO migrado — ver reporte 14B-2. onChange
                    dispara ctx.applyGlobalWarehouse(), que propaga la bodega a
                    todas las líneas — operación de stock/logística, dominio
                    excluido explícitamente de este bloque. */}
                <ZHField density="compact" label="Bodega Destino">
                  <select
                    value={ctx.formWatch.globalWarehouseId}
                    onChange={(e) => ctx.applyGlobalWarehouse(e.target.value)}
                    disabled={ctx.fieldDisabled}
                  >
                    <option value="">— Seleccionar bodega —</option>
                    {ctx.warehouses.map((w) => (
                      <option key={w.id} value={w.id}>
                        {w.code ? `${w.code} — ${w.name}` : w.name}
                      </option>
                    ))}
                  </select>
                </ZHField>
              </div>
            </div>
          </div>

          {/* ── Observaciones ── */}
          <div className="pf-collapsible">
            <button
              type="button"
              className="pf-collapsible__toggle"
              onClick={() => ctx.setShowNotes((v) => !v)}
            >
              <span
                className="material-symbols-outlined zh-icon-lg"
              >
                notes
              </span>
              <span>Observaciones</span>
              <span
                className={`material-symbols-outlined pf-collapsible__chevron ${ctx.showNotes ? "pf-collapsible__chevron--open" : ""}`}
              >
                expand_more
              </span>
              {!ctx.showNotes && ctx.formWatch.notes && (
                <Badge
                  variant="info"
                  label="Con notas"
                  className="pf-collapsible__status"
                />
              )}
            </button>
            {ctx.showNotes && (
              <div className="pf-collapsible__body">
                <ZHField density="compact">
                  <ZhTextarea
                    {...ctx.register("notes")}
                    rows={3}
                    placeholder="Ingrese notas adicionales o descripciones del servicio/producto..."
                    disabled={ctx.fieldDisabled}
                  />
                </ZHField>
              </div>
            )}
          </div>

          {/* ── MAIN LAYOUT ── */}
          <div className="pf-main-layout">
            <div>
              {/* ── Product Detail ── */}
              <div className="pdl-container">
                <div className="pdl-topbar">
                  <h4 className="pdl-topbar__title">
                    <span
                      className="material-symbols-outlined pf-icon--22"
                    >
                      inventory_2
                    </span>
                    Detalle de Productos
                  </h4>
                  {/* pdl-search__input: NO migrado — ver reporte 14B-2. Buscador
                      especializado con navegación por teclado (onKeyDown) y
                      dropdown de resultados — Categoría C, no equivalente a
                      ZhTextInput. */}
                  <div className="pdl-search__wrap" ref={ctx.globalSearchRef}>
                    <div className="pdl-search__input-wrap">
                      <span className="material-symbols-outlined pdl-search__icon">
                        search
                      </span>
                      <input
                        className="pdl-search__input"
                        type="text"
                        placeholder="Buscar producto por nombre o SKU..."
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
                        {ctx.globalResults.length === 0 ? (
                          <div className="pdl-search__empty">
                            Sin resultados para &ldquo;{ctx.globalQuery}&rdquo;
                          </div>
                        ) : (
                          ctx.globalResults.map((item, i) => (
                            <button
                              key={item.id}
                              type="button"
                              className={`pdl-search__result ${i === ctx.globalFocusIdx ? "pdl-search__result--focused" : ""}`}
                              onClick={() => void ctx.addLineWithItem(item)}
                              onMouseEnter={() => ctx.setGlobalFocusIdx(i)}
                            >
                              <span className="pdl-search__result-sku">
                                {item.sku}
                              </span>
                              <span className="pdl-search__result-name">
                                {item.shortName}
                              </span>
                              <span className="pdl-search__result-type">
                                {item.itemTypeName}
                              </span>
                            </button>
                          ))
                        )}
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
                    <option value="all">Todo</option>
                    {ctx.itemTypeOptions.map((it) => (
                      <option key={it.id} value={it.id}>
                        {it.name}
                      </option>
                    ))}
                  </ZhSelect>
                  <CostsDropdown ctx={ctx} />
                </div>

                {/* Column Headers */}
                <div className="pdl-header">
                  <span className="pdl-header__col pdl-header__col--num">
                    #
                  </span>
                  <span className="pdl-header__col pdl-header__col--product">
                    Producto / Código
                  </span>
                  <span className="pdl-header__col pdl-header__col--qty">
                    Cantidad
                  </span>
                  <span className="pdl-header__col pdl-header__col--finance">
                    Financiero (Costo/Desc)
                  </span>
                  <span className="pdl-header__col pdl-header__col--tax">
                    Impuestos
                  </span>
                  <span className="pdl-header__col pdl-header__col--total">
                    Total Neto
                  </span>
                  <span className="pdl-header__col pdl-header__col--actions">
                    Acciones
                  </span>
                </div>

                {/* Line Cards */}
                <div className="pdl-lines">
                  {ctx.lines.map((l, idx) => (
                    <PurchaseLineCard
                      key={l._key}
                      line={l}
                      idx={idx}
                      ctx={ctx}
                    />
                  ))}
                  {ctx.lines.length === 0 && (
                    <div className="pdl-empty">
                      <span
                        className="material-symbols-outlined pdl-empty__icon"
                      >
                        add_shopping_cart
                      </span>
                      <p>Sin productos agregados</p>
                    </div>
                  )}
                </div>

                <div className="pdl-add-wrap">
                  <button
                    type="button"
                    className="pdl-add"
                    onClick={ctx.addLine}
                    disabled={ctx.fieldDisabled}
                  >
                    <span
                      className="material-symbols-outlined pf-icon--28"
                    >
                      add_circle
                    </span>
                    <div>
                      <div className="pdl-add__title">
                        Agregar Nuevo Producto o Servicio
                      </div>
                      <div className="pdl-add__hint">
                        Presione para desplegar el catálogo o búsqueda rápida
                      </div>
                    </div>
                  </button>
                </div>
              </div>
            </div>

            {/* ── Schedule + Summary ── */}
            <div className="pf-plazos-resumen-grid">
              <div>
                <PaymentScheduleSection ctx={ctx} />
                {ctx.editing && ctx.editing.status === "Confirmed" && (
                  <RetentionSection ctx={ctx} />
                )}
              </div>
              <SummaryPanel ctx={ctx} />
            </div>
          </div>

          {ctx.editing &&
            ctx.editing.status === "Draft" &&
            ctx.pendingReceptionItems.length > 0 && (
              <ZHPageNotice
                variant="warning"
                message="No se puede confirmar la compra: productos pendientes de vinculación"
                detail={ctx.pendingReceptionItems.join(", ")}
              />
            )}

          {/* Footer Actions */}
          <div className="pf-footer">
            <ZHBtn
              type="button"
              variant="secondary"
              onClick={() => {
                ctx.resetForm();
                ctx.setTab("listado");
              }}
            >
              <span className="material-symbols-outlined zh-icon-md">
                arrow_back
              </span>
              Cancelar
            </ZHBtn>
            <ZHBtn
              type="button"
              variant="primary"
              onClick={ctx.handleSave}
              disabled={
                ctx.saving ||
                !ctx.formWatch.invoiceNumber.trim() ||
                !ctx.formWatch.supplierId.trim() ||
                !ctx.formWatch.issueDate ||
                ctx.lines.length === 0
              }
            >
              <span className="material-symbols-outlined zh-icon-md">
                save
              </span>
              {ctx.saving
                ? "Guardando..."
                : ctx.editing
                  ? "Actualizar Borrador"
                  : "Guardar Borrador"}
            </ZHBtn>
            {ctx.editing && ctx.editing.status === "Draft" && (
              <ZHBtn
                type="button"
                variant="primary"
                onClick={() => ctx.setModalConfirm(true)}
                disabled={
                  ctx.fieldDisabled || ctx.pendingReceptionItems.length > 0
                }
                title={
                  ctx.pendingReceptionItems.length > 0
                    ? "Resuelva los productos pendientes de vinculación antes de confirmar."
                    : undefined
                }
              >
                <span className="material-symbols-outlined zh-icon-md">
                  check_circle
                </span>
                Confirmar Compra
              </ZHBtn>
            )}
            {ctx.editing && ctx.editing.status === "Confirmed" && (
              <ZHBtn
                type="button"
                variant="secondary"
                onClick={() =>
                  navigate(`/purchases/returns/new?invoiceId=${ctx.editing!.id}`)
                }
              >
                <span className="material-symbols-outlined zh-icon-md">
                  assignment_return
                </span>
                Devolución
              </ZHBtn>
            )}
            {ctx.editing && ctx.editing.status === "Confirmed" && (
              // Acción complementaria (FLOW-READY-02C.4) — nunca reemplaza "Devolución": la
              // pantalla centralizada de Nota de Crédito (FLOW-READY-02C.3) delega ahí mismo a
              // /purchases/returns/new cuando el usuario elige Devolución. Mismo patrón de
              // navegación que el botón "Devolución" de arriba (navigate en la misma pestaña,
              // no window.open): esta pantalla de edición de compra ya navega in-place para sus
              // acciones documentales (Devolución, Anular Compra), a diferencia de la bandeja de
              // Recepción que sí abre pestañas nuevas para mantenerse abierta como lista.
              <ZHBtn
                type="button"
                variant="secondary"
                title={t("purchases.creditNote.actions.openCreditNote", "Abrir nota de crédito")}
                onClick={() =>
                  navigate(`/purchases/credit-notes/new?invoiceId=${ctx.editing!.id}`)
                }
              >
                <span className="material-symbols-outlined zh-icon-md">
                  request_quote
                </span>
                {t("purchases.creditNote.actions.creditNoteButton", "Nota de crédito")}
              </ZHBtn>
            )}
            {ctx.editing && ctx.editing.status === "Confirmed" && (
              <ZHBtn
                type="button"
                variant="destructive"
                onClick={() => ctx.setModalCancelReason(true)}
                disabled={ctx.fieldDisabled}
              >
                <span className="material-symbols-outlined zh-icon-md">
                  block
                </span>
                Anular Compra
              </ZHBtn>
            )}
          </div>
        </div>
      )}

      {/* ══════════════════════════════════════════════════════════ MODALS */}
      <ZHPromptModal
        open={ctx.modalDiscount}
        title="Descuento Global"
        label="Porcentaje (%)"
        type="decimal"
        decimals={getDecimalConfig().percentage}
        positiveOnly
        placeholder={formatMoney(0, getDecimalConfig().percentage)}
        variant="default"
        confirmLabel="Aplicar"
        onCancel={() => ctx.setModalDiscount(false)}
        onConfirm={ctx.handleApplyDiscount}
      />

      <ZHConfirmModal
        open={ctx.modalConfirm}
        variant="warning"
        title="Confirmar Compra"
        message="Esta acción generará movimientos de inventario y cuentas por pagar. No se puede revertir."
        confirmLabel="Confirmar"
        onCancel={() => ctx.setModalConfirm(false)}
        onConfirm={ctx.handleConfirm}
      />

      <ZHPromptModal
        open={ctx.modalCancelReason}
        variant="danger"
        title="Anular Compra"
        message={
          ctx.editing
            ? `¿ANULAR compra ${ctx.editing.invoiceNumber}? Esto revertirá stock, cuentas por pagar y retenciones. Esta acción NO se puede deshacer.`
            : ""
        }
        label="Motivo de anulación"
        placeholder="Ingrese el motivo..."
        confirmLabel="Anular"
        onCancel={() => ctx.setModalCancelReason(false)}
        onConfirm={ctx.handleCancel}
      />

      <ZHPromptModal
        open={ctx.modalWhIssue}
        title="Emitir Retención"
        variant="default"
        label="ID del Punto de Emisión"
        placeholder="ID del punto de emisión"
        confirmLabel="Emitir"
        onCancel={() => ctx.setModalWhIssue(false)}
        onConfirm={ctx.handleIssueWithholding}
      />

      <ZHPromptModal
        open={ctx.modalWhCancel}
        variant="danger"
        title="Anular Retención"
        label="Motivo de anulación"
        placeholder="Ingrese el motivo..."
        confirmLabel="Anular"
        onCancel={() => ctx.setModalWhCancel(false)}
        onConfirm={ctx.handleCancelWithholding}
      />
    </ErpPageTemplate>
  );
}

// ── Internal Components ────────────────────────────────────────────────

const STATUS_TONE_VARIANT: Record<LineStatusTone, BadgeVariant> = {
  success: "green",
  warning: "orange",
  danger: "red",
};

type PurchaseLineDraft = PurchaseLineFormValues;

function PurchaseLineCard({
  line: l,
  idx,
  ctx,
}: {
  line: PurchaseLineDraft;
  idx: number;
  ctx: ReturnType<typeof usePurchasesPage>;
}) {
  const viewMatchedItem = useViewMatchedItem();
  const sub = lineNet(l);
  const editLine = ctx.editing?.lines?.[idx];
  const localTax = calcLineTax(l, ctx.vatRatesMap, ctx.iceRatesMap);
  const vatAmt = editLine?.vatAmount ?? localTax.vat;
  const iceAmt = editLine?.iceAmount ?? localTax.ice;
  // FLOW-READY-02F.1 — IRBPNR solo existe una vez persistido desde el XML (no hay cálculo local
  // por tarifas, a diferencia de IVA/ICE); nunca se suma al total de línea (ver ConfirmPurchase:
  // no forma parte de TaxInclusiveTotal mientras no exista soporte contable verificado).
  const irbpnrAmt = editLine?.irbpnrAmount ?? 0;
  const total = editLine
    ? (editLine.totalLineCost ?? sub) + editLine.vatAmount + editLine.iceAmount
    : sub + localTax.vat + localTax.ice;
  const ctxData = l.context;
  const vatPct = ctxData?.vatPercent ?? ctx.vatRatesMap[l.vatCode] ?? 0;
  const vm = buildPurchaseLinePresentation(l);

  return (
    <div className="pdl-line">
      <div className="pdl-line__main">
        <div className="pdl-line__num">{String(idx + 1).padStart(2, "0")}</div>
        <div className="pdl-line__product">
          {!l.itemId ? (
            <>
              <ProductPicker
                disabled={ctx.fieldDisabled || ctx.matchingKey === l._key}
                vatRates={ctx.vatRatesMap}
                onSelect={(p: ProductProfile) => {
                  if (!l.vatCode)
                    ctx.updateLine(l._key, "vatCode", p.purchaseVatCode ?? "");
                  if (p.appliesExciseTax && p.exciseTaxCode)
                    ctx.updateLine(l._key, "iceCode", p.exciseTaxCode);
                  if (l.purchaseReceptionLineId) {
                    void ctx.handleMatchItem(
                      l._key,
                      p.id,
                      `${p.sku} — ${p.name}`,
                    );
                    return;
                  }
                  ctx.updateLine(l._key, "itemId", p.id);
                  ctx.updateLine(l._key, "description", `${p.sku} — ${p.name}`);
                  const wh = l.warehouseId || ctx.formWatch.globalWarehouseId;
                  if (wh) void ctx.fetchItemContext(l._key, p.id, wh);
                }}
              />
              {l.purchaseReceptionLineId ? (
                <ReceptionCreateItemAction
                  line={l}
                  ctx={ctx}
                  disabled={ctx.fieldDisabled}
                />
              ) : (
                <CreateItemLineAction
                  line={l}
                  ctx={ctx}
                  disabled={ctx.fieldDisabled}
                />
              )}
              {l.itemMatchStatus && (
                <ItemMatchStatusBadge status={l.itemMatchStatus} />
              )}
            </>
          ) : (
            <>
              <div className="pdl-line__product-name">
                {ctxData?.shortName ||
                  l.description?.split(" — ")[1] ||
                  l.description}
                {l.itemMatchStatus && (
                  <ItemMatchStatusBadge status={l.itemMatchStatus} />
                )}
                <ZHBtn
                  variant="ghost"
                  size="xs"
                  type="button"
                  onClick={() => viewMatchedItem(l.itemId as string)}
                >
                  Ver Item
                </ZHBtn>
                {!ctx.fieldDisabled && <UpdateItemAction line={l} ctx={ctx} />}
                {!ctx.fieldDisabled && l.purchaseReceptionLineId && (
                  <ZHBtn
                    variant="ghost"
                    size="xs"
                    type="button"
                    disabled={ctx.matchingKey === l._key}
                    onClick={() => void ctx.handleUnmatchItem(l._key)}
                  >
                    {ctx.matchingKey === l._key
                      ? "Desvinculando..."
                      : "Desvincular Item"}
                  </ZHBtn>
                )}
                {!ctx.fieldDisabled && !l.purchaseReceptionLineId && (
                  <button
                    type="button"
                    className="pdl-line__clear"
                    title="Cambiar producto"
                    onClick={() => {
                      ctx.updateLine(l._key, "itemId", undefined);
                      ctx.updateLine(l._key, "description", "");
                      ctx.updateLine(l._key, "context", undefined);
                    }}
                  >
                    <span
                      className="material-symbols-outlined zh-icon-sm"
                    >
                      close
                    </span>
                  </button>
                )}
              </div>
              {/* pdl-line__wh-select: NO migrado — ver reporte 14B-2. Control
                  de bodega por línea del detalle de productos (tabla) —
                  "no migrar tablas", dominio excluido explícitamente. */}
              <select
                className="pdl-line__wh-select"
                value={l.warehouseId ?? ctx.formWatch.globalWarehouseId ?? ""}
                onChange={(e) =>
                  ctx.updateLineWarehouse(l._key, e.target.value || null)
                }
                disabled={ctx.fieldDisabled}
              >
                <option value="">— Seleccionar bodega —</option>
                {ctx.warehouses.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.code ? `${w.code} — ${w.name}` : w.name}
                  </option>
                ))}
              </select>
            </>
          )}
        </div>
        <div className="pdl-line__qty">
          <ZhDecimalInput
            className="pdl-input pdl-input--qty"
            decimals={getDecimalConfig().quantity}
            positiveOnly
            defaultValue={l.quantity}
            onBlur={(e) =>
              ctx.updateLine(l._key, "quantity", Number(e.target.value) || 1)
            }
            disabled={ctx.fieldDisabled}
          />
        </div>
        <div className="pdl-line__finance">
          <div className="pdl-line__cost-row">
            <span className="pdl-line__cost-label">COSTO: $</span>
            <ZhDecimalInput
              className="pdl-input pdl-input--cost"
              decimals={getDecimalConfig().purchaseUnitPrice}
              positiveOnly
              defaultValue={l.unitPrice}
              onBlur={(e) =>
                ctx.updateLine(l._key, "unitPrice", Number(e.target.value) || 0)
              }
              disabled={ctx.fieldDisabled}
            />
          </div>
          <div className="pdl-line__cost-row">
            <span className="pdl-line__cost-label">DESC: %</span>
            <ZhDecimalInput
              className="pdl-input pdl-input--disc"
              decimals={getDecimalConfig().percentage}
              positiveOnly
              defaultValue={l.discountPct ?? 0}
              onBlur={(e) =>
                ctx.updateLine(
                  l._key,
                  "discountPct",
                  Math.min(100, Math.max(0, Number(e.target.value) || 0)),
                )
              }
              disabled={ctx.fieldDisabled}
            />
          </div>
        </div>
        <div className="pdl-line__tax">
          <div className="pdl-line__tax-head">IVA {vatPct}%: $</div>
          <div className="pdl-line__tax-amount">
            {formatMoney(vatAmt, getDecimalConfig().totalAmount)}
          </div>
          <div className="pdl-line__tax-ice">
            ICE: $ {formatMoney(iceAmt, getDecimalConfig().totalAmount)}
          </div>
          {irbpnrAmt > 0 && (
            <div className="pdl-line__tax-ice">
              IRBPNR: $ {formatMoney(irbpnrAmt, getDecimalConfig().totalAmount)}
            </div>
          )}
        </div>
        <div className="pdl-line__total">
          $ {formatMoney(total, getDecimalConfig().totalAmount)}
        </div>
        <div className="pdl-line__actions">
          <ZHIconButton
            icon="content_copy"
            variant="ghost"
            title="Duplicar"
            onClick={() => ctx.duplicateLine(l._key)}
          />
          <ZHIconButton
            icon="delete"
            variant="danger"
            title="Eliminar"
            onClick={() => ctx.removeLine(l._key)}
          />
        </div>
      </div>

      {/* Resumen de estado — interpreta visualmente estados ya existentes, ninguno nuevo. */}
      <div className="pdl-line__status">
        <Badge
          variant={STATUS_TONE_VARIANT[vm.status.tone]}
          label={`${vm.status.icon} ${vm.status.label}`}
        />
      </div>

      {/* 1. Qué llegó del proveedor — siempre visible, con nota cuando la línea no viene de XML. */}
      <div className="pdl-blocks">
        <section className="pdl-block">
          <header className="pdl-block__header">
            <span
              className="material-symbols-outlined pdl-block__icon"
            >
              description
            </span>
            <h4 className="pdl-block__title">Producto recibido (XML)</h4>
          </header>
          {!vm.xml.hasOrigin && (
            <p className="pdl-block__hint">
              Compra manual — no proviene de Recepción Electrónica.
            </p>
          )}
          <div className="pdl-ctx-col__badges">
            <span className="pdl-tag">
              Cód. proveedor: {vm.xml.supplierCode}
            </span>
            <span
              className={`pdl-tag ${vm.xml.hasSupplierAuxCode ? "pdl-tag--accent" : ""}`}
            >
              Cód. auxiliar: {vm.xml.supplierAuxCode}
            </span>
          </div>
          <div className="pdl-ctx-col__desc">{vm.xml.description}</div>
          <div className="pdl-ctx-col__costs">
            <div>
              <span className="pdl-cost-label">Cantidad</span>
              <span className="pdl-cost-val">{vm.xml.quantity}</span>
            </div>
            <div>
              <span className="pdl-cost-label">Precio</span>
              <span className="pdl-cost-val">{vm.xml.unitPrice}</span>
            </div>
            <div>
              <span className="pdl-cost-label">Descuento</span>
              <span className="pdl-cost-val">{vm.xml.discount}</span>
            </div>
            <div>
              <span className="pdl-cost-label">
                IVA ({vm.xml.vatPercentage}%)
              </span>
              <span className="pdl-cost-val">{vm.xml.taxValue}</span>
            </div>
            <div>
              <span className="pdl-cost-label pdl-cost-label--strong">
                Total línea
              </span>
              <span className="pdl-cost-val pdl-cost-val--strong">
                {vm.xml.totalLine}
              </span>
            </div>
          </div>
        </section>

        {/* 2. Con qué Item del ERP está relacionado — siempre visible, incluso sin Item aún. */}
        <section className="pdl-block">
          <header className="pdl-block__header">
            <span
              className="material-symbols-outlined pdl-block__icon"
            >
              badge
            </span>
            <h4 className="pdl-block__title">Item ERP</h4>
            {vm.item.isLoading && (
              <span
                className="pdl-ctx-col__loading"
                title="Cargando contexto..."
              >
                  <span
                  className="material-symbols-outlined pdl-ctx-col__loading-icon"
                >
                  hourglass_empty
                </span>
              </span>
            )}
          </header>
          {vm.item.matchStatus ? (
            <ItemMatchStatusBadge status={vm.item.matchStatus} />
          ) : (
            <Badge
              variant={vm.item.hasItem ? "green" : "orange"}
              label={
                vm.item.hasItem
                  ? "🟢 Item seleccionado"
                  : "🟡 Pendiente de vincular Item"
              }
            />
          )}
          <div className="pdl-ctx-col__costs">
            <div>
              <span className="pdl-cost-label">SKU</span>
              <span className="pdl-cost-val">{vm.item.sku}</span>
            </div>
            <div>
              <span className="pdl-cost-label">Nombre</span>
              <span className="pdl-cost-val">{vm.item.name}</span>
            </div>
            <div>
              <span className="pdl-cost-label">Unidad</span>
              <span className="pdl-cost-val">{vm.item.uom}</span>
            </div>
          </div>
        </section>

        {/* 3. Impacto para el negocio — información de análisis, menor peso visual que los bloques anteriores. */}
        <section className="pdl-block pdl-block--muted">
          <header className="pdl-block__header">
            <span
              className="material-symbols-outlined pdl-block__icon"
            >
              insights
            </span>
            <h4 className="pdl-block__title">Información Comercial</h4>
          </header>
          <div className="pdl-ctx-col__stock">
            <div className="pdl-stock-item">
              <span
                className={`pdl-stock-label ${vm.commercial.hasContext ? (vm.commercial.stock.isCritical ? "pdl-stock-label--danger" : "pdl-stock-label--ok") : ""}`}
              >
                {vm.commercial.stock.statusLabel}
              </span>
              <span className="pdl-stock-val">
                {vm.commercial.stock.current}
              </span>
            </div>
            <div className="pdl-stock-item">
              <span className="pdl-stock-head">Disp.</span>
              <span className="pdl-stock-val">
                {vm.commercial.stock.available}
              </span>
            </div>
            <div className="pdl-stock-item">
              <span className="pdl-stock-head">Reserva</span>
              <span className="pdl-stock-val">
                {vm.commercial.stock.reserved}
              </span>
            </div>
          </div>
          <div className="pdl-ctx-col__costs">
            <div>
              <span className="pdl-cost-label">Costo promedio</span>
              <span className="pdl-cost-val">
                {vm.commercial.costs.average}
              </span>
            </div>
            <div>
              <span className="pdl-cost-label">Último costo</span>
              <span className="pdl-cost-val">{vm.commercial.costs.last}</span>
            </div>
          </div>
          {vm.commercial.costs.showDeviationAlert && (
            <div className="pdl-cost-alert">
              <span
                className="material-symbols-outlined pdl-cost-alert__icon"
              >
                warning
              </span>
              {vm.commercial.costs.deviationLabel}
            </div>
          )}
          <div className="pdl-ctx-col__margin">
            <div className="pdl-margin-row">
              <span className="pdl-margin-label">
                Rentabilidad (margen neto)
              </span>
              <span
                className={`pdl-margin-pct ${vm.commercial.profitability.marginPctValue >= 0 ? "" : "pdl-margin-pct--neg"}`}
              >
                {vm.commercial.profitability.marginPct}%
              </span>
            </div>
            <progress
              className="pdl-margin-bar pdl-margin-bar--progress"
              value={Math.min(100, Math.max(0, vm.commercial.profitability.marginPctValue))}
              max={100}
              aria-label="Rentabilidad margen neto"
            />
            <div className="pdl-margin-meta">
              <span>Precio de venta: {vm.commercial.profitability.pvp}</span>
              <span>
                Desc Máx: {vm.commercial.profitability.maxDiscountPercent}%
              </span>
            </div>
          </div>
        </section>
      </div>
    </div>
  );
}

/**
 * "Crear Item" para una línea de compra manual sin producto asignado — usa el ItemEditorModal
 * genérico directamente (sin wrapper), en modo Crear (sin `itemId`): esta línea no tiene
 * PurchaseReceptionLineId, por lo que no hay nada que vincular en el backend de Compras más allá
 * de seleccionar el ítem en el formulario, igual que hace ProductPicker.onSelect.
 */
function CreateItemLineAction({
  line: l,
  ctx,
  disabled,
}: {
  line: PurchaseLineDraft;
  ctx: ReturnType<typeof usePurchasesPage>;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);

  const handleSaved = (item: ItemCreatedResult) => {
    setOpen(false);
    ctx.updateLine(l._key, "itemId", item.id);
    ctx.updateLine(l._key, "description", `${item.sku} — ${item.shortName}`);
    // El Item recién creado desde Compras trae su propio IVA de compra (ahora obligatorio en
    // ItemEditorModal) — se refleja de inmediato en la línea, sin depender de fetchItemContext
    // (que solo llena `context` para mostrar, no el campo `vatCode` del formulario).
    ctx.updateLine(l._key, "vatCode", item.purchaseVatCode ?? "");
    const wh = l.warehouseId || ctx.formWatch.globalWarehouseId;
    if (wh) void ctx.fetchItemContext(l._key, item.id, wh);
  };

  return (
    <>
      <ZHBtn
        variant="ghost"
        size="xs"
        type="button"
        disabled={disabled}
        onClick={() => setOpen(true)}
      >
        Crear Item
      </ZHBtn>
      <ItemEditorModal
        open={open}
        initialData={{
          name: l.description || undefined,
          barcode: l.xmlSupplierAuxCode ?? l.xmlSupplierCode ?? undefined,
          supplierCode: l.xmlSupplierCode ?? undefined,
          supplierId: ctx.formWatch.supplierId || undefined,
          source: l.xmlSupplierCode ? "PurchaseReception" : "Manual",
          purchaseContext:
            l.unitPrice > 0
              ? {
                  unitCost: l.unitPrice,
                  quantity: l.quantity,
                  discountPct: l.discountPct ?? undefined,
                  vatCode: l.vatCode || undefined,
                }
              : undefined,
        }}
        onClose={() => setOpen(false)}
        onSaved={handleSaved}
      />
    </>
  );
}

/**
 * "Actualizar Item" — visible en cualquier línea que ya tenga Item vinculado (manual o de
 * Recepción, da igual el origen). Reutiliza el mismo ItemEditorModal en modo Actualizar (pasando
 * `itemId`) y, al guardar, refresca el contexto de la línea con el mismo flujo ya existente
 * (`fetchItemContext`) — nunca un flujo paralelo de actualización visual.
 */
function UpdateItemAction({
  line: l,
  ctx,
}: {
  line: PurchaseLineFormValues;
  ctx: ReturnType<typeof usePurchasesPage>;
}) {
  const [open, setOpen] = useState(false);
  if (!l.itemId) return null;

  const handleSaved = (item: ItemCreatedResult) => {
    setOpen(false);
    ctx.updateLine(l._key, "description", `${item.sku} — ${item.shortName}`);
    // Si el usuario cambió el IVA de compra al editar el Item desde esta línea, la línea debe
    // reflejar el valor vigente del Item de inmediato.
    ctx.updateLine(l._key, "vatCode", item.purchaseVatCode ?? "");
    const wh = l.warehouseId || ctx.formWatch.globalWarehouseId;
    if (wh) void ctx.fetchItemContext(l._key, item.id, wh);
  };

  return (
    <>
      <ZHBtn
        variant="ghost"
        size="xs"
        type="button"
        onClick={() => setOpen(true)}
      >
        Actualizar Item
      </ZHBtn>
      <ItemEditorModal
        open={open}
        itemId={l.itemId}
        initialData={{
          purchaseContext:
            l.unitPrice > 0
              ? {
                  unitCost: l.unitPrice,
                  quantity: l.quantity,
                  discountPct: l.discountPct ?? undefined,
                  vatCode: l.vatCode || undefined,
                }
              : undefined,
        }}
        onClose={() => setOpen(false)}
        onSaved={handleSaved}
      />
    </>
  );
}

/**
 * "Crear Item" para una línea que sí tiene PurchaseReceptionLineId — reutiliza el mismo wrapper
 * que usa la pantalla de Recepción (`CreateItemFromReceptionLineModal`), que crea el Item y lo
 * vincula server-side con `matchItem` en un solo paso, sin reimplementar esa lógica aquí.
 */
function ReceptionCreateItemAction({
  line: l,
  ctx,
  disabled,
}: {
  line: PurchaseLineDraft;
  ctx: ReturnType<typeof usePurchasesPage>;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);

  const receptionLine: PurchaseReceptionLineMatch = {
    lineId: l.purchaseReceptionLineId as string,
    supplierId: ctx.formWatch.supplierId || null,
    supplierCode: l.xmlSupplierCode ?? null,
    supplierAuxCode: l.xmlSupplierAuxCode ?? null,
    description: l.description,
    quantity: l.quantity,
    unitPrice: l.unitPrice,
    itemId: l.itemId ?? null,
    matchStatus: l.itemMatchStatus ?? "PENDING",
    matchedAt: null,
    suggestions: [],
  };

  const applyLinked = (
    itemId: string,
    matchStatus: ItemMatchStatus,
    label: string,
    vatCode?: string | null,
  ) => {
    const current = ctx.getValues("lines");
    ctx.setValue(
      "lines",
      current.map((x: PurchaseLineFormValues) =>
        x._key === l._key
          ? {
              ...x,
              itemId,
              itemMatchStatus: matchStatus,
              description: label,
              // Solo se pisa si viene un IVA de compra real — el matching manual (sin `item`
              // recién creado) no debe borrar un vatCode ya cargado en la línea.
              ...(vatCode !== undefined ? { vatCode: vatCode ?? "" } : {}),
            }
          : x,
      ),
    );
    const wh = l.warehouseId || ctx.formWatch.globalWarehouseId;
    if (wh) void ctx.fetchItemContext(l._key, itemId, wh);
  };

  return (
    <>
      <ZHBtn
        variant="ghost"
        size="xs"
        type="button"
        disabled={disabled}
        onClick={() => setOpen(true)}
      >
        Crear Item
      </ZHBtn>
      <CreateItemFromReceptionLineModal
        open={open}
        line={receptionLine}
        supplierName={ctx.supplierProfile?.name ?? ""}
        purchaseContext={
          l.unitPrice > 0
            ? {
                unitCost: l.unitPrice,
                quantity: l.quantity,
                discountPct: l.discountPct ?? undefined,
                vatCode: l.vatCode || undefined,
              }
            : undefined
        }
        onClose={() => setOpen(false)}
        onCreated={(updatedLine, item) => {
          setOpen(false);
          if (updatedLine.itemId)
            applyLinked(
              updatedLine.itemId,
              updatedLine.matchStatus,
              `${item.sku} — ${item.shortName}`,
              item.purchaseVatCode,
            );
        }}
        onCreatedButNotLinked={() => setOpen(false)}
      />
    </>
  );
}

function CostsDropdown({ ctx }: { ctx: ReturnType<typeof usePurchasesPage> }) {
  return (
    <div className="pdl-topbar__right" ref={ctx.costsMenuRef}>
      <button
        type="button"
        className="pdl-costs-toggle"
        onClick={() => ctx.setShowCostsMenu((v) => !v)}
      >
        <span className="material-symbols-outlined pdl-costs-toggle__icon">
          tune
        </span>
        Gastos y Acciones
        <span
          className={`material-symbols-outlined pdl-costs-toggle__chevron${ctx.showCostsMenu ? " pdl-costs-toggle__chevron--open" : ""}`}
        >
          expand_more
        </span>
      </button>
      {ctx.showCostsMenu && (
        <div className="pdl-costs-dropdown">
          <div className="pdl-costs-dropdown__row">
            <ZHField density="compact" label="Flete ($)">
              <ZhDecimalInput
                decimals={getDecimalConfig().totalAmount}
                positiveOnly
                defaultValue={ctx.formWatch.freightCost}
                onBlur={(e) =>
                  ctx.setValue("freightCost", Number(e.target.value) || 0)
                }
                disabled={ctx.fieldDisabled}
              />
            </ZHField>
            <ZHField density="compact" label="Otros Gastos ($)">
              <ZhDecimalInput
                decimals={getDecimalConfig().totalAmount}
                positiveOnly
                defaultValue={ctx.formWatch.otherCosts}
                onBlur={(e) =>
                  ctx.setValue("otherCosts", Number(e.target.value) || 0)
                }
                disabled={ctx.fieldDisabled}
              />
            </ZHField>
          </div>
          <ZHBtn
            type="button"
            variant="secondary"
            className="pdl-costs-dropdown__btn"
            onClick={() => {
              ctx.setModalDiscount(true);
              ctx.setShowCostsMenu(false);
            }}
            disabled={ctx.fieldDisabled || !ctx.editing}
          >
            <span
              className="material-symbols-outlined pdl-costs-dropdown__icon"
            >
              percent
            </span>
            Desc. Global
          </ZHBtn>
          <ZHBtn
            type="button"
            variant="secondary"
            className="pdl-costs-dropdown__btn"
            onClick={ctx.handleAllocateFreight}
            disabled={ctx.fieldDisabled || !ctx.editing}
          >
            <span
              className="material-symbols-outlined pdl-costs-dropdown__icon"
            >
              local_shipping
            </span>
            Calc. Flete
          </ZHBtn>
          <ZHBtn
            type="button"
            variant="primary"
            className="pdl-costs-dropdown__btn"
            onClick={ctx.handleRecalculate}
            disabled={ctx.fieldDisabled || !ctx.editing}
          >
            <span
              className="material-symbols-outlined pdl-costs-dropdown__icon"
            >
              sync
            </span>
            Recalcular
          </ZHBtn>
        </div>
      )}
    </div>
  );
}

function PaymentScheduleSection({
  ctx,
}: {
  ctx: ReturnType<typeof usePurchasesPage>;
}) {
  return (
    <div className="pf-card">
      <div className="pf-card__header">
        <h4 className="pf-card__title">
          <span className="material-symbols-outlined pf-card__title-icon">
            calendar_month
          </span>
          Plazos de Pago
          {ctx.hasPersistedSchedule && (
            <Badge
              variant="success"
              className="pf-badge--offset"
              label={
                <>
                  <span className="material-symbols-outlined pf-badge__icon">
                    lock
                  </span>{" "}
                  Confirmado
                </>
              }
            />
          )}
          {!ctx.hasPersistedSchedule && ctx.ptLoaded && (
            <Badge
              variant="warning"
              className="pf-badge--offset"
              label="Preview"
            />
          )}
        </h4>
        {ctx.isDraft && !ctx.hasPersistedSchedule && (
          <div className="pf-schedule-actions">
            <ZHBtn
              type="button"
              variant="primary"
              size="xs"
              onClick={ctx.regenerateSchedule}
              disabled={ctx.saving || !ctx.formWatch.issueDate}
            >
              <span
                className="material-symbols-outlined pf-schedule-action-icon"
              >
                autorenew
              </span>
              Regenerar
            </ZHBtn>
            <ZHBtn
              type="button"
              variant="secondary"
              size="xs"
              onClick={ctx.addInstallment}
              disabled={ctx.saving || !ctx.formWatch.issueDate}
            >
              <span
                className="material-symbols-outlined pf-schedule-action-icon"
              >
                add
              </span>
              Cuota
            </ZHBtn>
          </div>
        )}
      </div>
      <div className="pf-card__body">
        {ctx.isDraft && !ctx.hasPersistedSchedule && (
          <div className="pf-schedule-setup">
            <ZHField
              density="compact"
              label="Nro. Cuotas"
              className="pf-schedule-field--installments"
            >
              <ZhNumberInput
                positiveOnly
                value={String(ctx.ptInstallments)}
                onChange={(e) =>
                  ctx.setPtInstallments(
                    Math.min(60, Math.max(1, Number(e.target.value) || 1)),
                  )
                }
                disabled={ctx.fieldDisabled}
              />
            </ZHField>
            <ZHField
              density="compact"
              label="Días entre cuotas"
              className="pf-schedule-field--days-between"
            >
              <ZhNumberInput
                positiveOnly
                value={String(ctx.ptDaysBetween)}
                onChange={(e) =>
                  ctx.setPtDaysBetween(Math.max(0, Number(e.target.value) || 0))
                }
                disabled={ctx.fieldDisabled}
              />
            </ZHField>
            <div className="pf-schedule-total">
              Total plazo:{" "}
              <strong>{ctx.ptInstallments * ctx.ptDaysBetween} días</strong>
            </div>
          </div>
        )}

        {ctx.ptMismatch && ctx.ptRows.length > 0 && (
          <ZHPageNotice
            variant="warning"
            message={`Las cuotas no cuadran con el total. Diferencia: $${formatMoney(roundToTotalAmount(ctx.localTotal - ctx.ptRowsSum), getDecimalConfig().totalAmount)}`}
          />
        )}

        {ctx.hasPersistedSchedule && (
          <table className="table table--compact table--neutral">
            <thead>
              <tr>
                <th className="pf-schedule-col--number">#</th>
                <th>Vencimiento</th>
                <th className="zh-text-align-right">Monto</th>
                <th>Notas</th>
              </tr>
            </thead>
            <tbody>
              {ctx.editing!.paymentSchedules.map((s) => (
                <tr key={s.id}>
                  <td className="zh-text-align-center pf-schedule-installment-number">
                    {s.installmentNumber}
                  </td>
                  <td>{formatDate(s.dueDate)}</td>
                  <td className="zh-table-cell--num">
                    {formatMoneyWithSymbol(
                      s.amount,
                      getDecimalConfig().totalAmount,
                    )}
                  </td>
                  <td className="pf-schedule-notes">
                    {s.notes || "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {!ctx.hasPersistedSchedule && ctx.ptRows.length > 0 && (
          <>
            <table className="pf-table">
              <thead>
                <tr>
                  <th className="pf-schedule-col--number">#</th>
                  <th>Vencimiento</th>
                  <th className="zh-text-align-right pf-schedule-col--amount">
                    Monto ($)
                  </th>
                  <th>Notas</th>
                  <th>Estado</th>
                  <th className="zh-text-align-center pf-schedule-col--actions"></th>
                </tr>
              </thead>
              <tbody>
                {ctx.ptRows.map((r, idx) => (
                  <tr key={idx}>
                    <td className="zh-text-align-center pf-schedule-installment-number pf-schedule-installment-number--muted">
                      {r.number}
                    </td>
                    <td>
                      <ZhDateInput
                        density="compact"
                        className="pf-schedule-input--date"
                        value={r.dueDate}
                        onChange={(e) =>
                          ctx.updateScheduleRow(idx, "dueDate", e.target.value)
                        }
                        disabled={ctx.saving || !ctx.isDraft}
                      />
                    </td>
                    <td>
                      <ZhDecimalInput
                        decimals={getDecimalConfig().totalAmount}
                        positiveOnly
                        density="compact"
                        className="pf-schedule-input--amount"
                        defaultValue={r.amount}
                        onBlur={(e) =>
                          ctx.updateScheduleRow(
                            idx,
                            "amount",
                            Number(e.target.value) || 0,
                          )
                        }
                        disabled={ctx.saving || !ctx.isDraft}
                      />
                    </td>
                    <td>
                      <ZhTextInput
                        density="compact"
                        className="pf-schedule-input--notes"
                        placeholder="Observación..."
                        value={r.notes}
                        onChange={(e) =>
                          ctx.updateScheduleRow(idx, "notes", e.target.value)
                        }
                        maxLength={500}
                        disabled={ctx.saving || !ctx.isDraft}
                      />
                    </td>
                    <td>
                      <Badge variant="warning" label="Pendiente" />
                    </td>
                    <td className="zh-text-align-center">
                      {ctx.isDraft && ctx.ptRows.length > 1 && (
                        <ZHIconButton
                          icon="close"
                          variant="danger"
                          title="Eliminar cuota"
                          onClick={() => ctx.removeInstallment(idx)}
                          disabled={ctx.fieldDisabled}
                        />
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="pf-schedule-footer">
              <span className="pf-schedule-footer__count">
                {ctx.ptRows.length} cuota(s)
              </span>
              <span>
                Total cuotas:{" "}
                <strong
                  className={`pf-schedule-footer__amount ${ctx.ptMismatch ? "pf-schedule-footer__amount--error" : "pf-schedule-footer__amount--default"}`}
                >
                  {formatMoneyWithSymbol(
                    ctx.ptRowsSum,
                    getDecimalConfig().totalAmount,
                  )}
                </strong>
                {" / "}Total compra:{" "}
                <strong className="pf-schedule-footer__purchase-total">
                  {formatMoneyWithSymbol(
                    ctx.localTotal,
                    getDecimalConfig().totalAmount,
                  )}
                </strong>
              </span>
            </div>
          </>
        )}

        {!ctx.hasPersistedSchedule && ctx.ptRows.length === 0 && (
          <div className="pf-schedule-empty">
            <span
              className="material-symbols-outlined pf-schedule-empty__icon"
            >
              event_busy
            </span>
            {!ctx.ptLoaded
              ? "Seleccione un proveedor para cargar la condición de pago"
              : !ctx.formWatch.issueDate
                ? "Ingrese la fecha de emisión para calcular vencimientos"
                : "Configure cuotas arriba para generar el cronograma"}
          </div>
        )}
      </div>
    </div>
  );
}

function RetentionSection({
  ctx,
}: {
  ctx: ReturnType<typeof usePurchasesPage>;
}) {
  return (
    <div className="pf-card pf-retention">
      <div className="pf-card__header">
        <h4 className="pf-card__title">
          <span className="material-symbols-outlined pf-card__title-icon">
            receipt
          </span>{" "}
          Retenciones
        </h4>
        <div className="pf-retention-actions">
          {!ctx.withholding && (
            <>
              <ZHBtn
                type="button"
                variant="primary"
                size="sm"
                onClick={ctx.handleCalcRetention}
                disabled={ctx.whLoading}
              >
                <span
                  className="material-symbols-outlined pf-retention-action-icon"
                >
                  calculate
                </span>
                Calcular
              </ZHBtn>
              {ctx.whPreview && ctx.whPreview.lines.length > 0 && (
                <ZHBtn
                  type="button"
                  variant="primary"
                  size="sm"
                  onClick={() => ctx.setModalWhIssue(true)}
                  disabled={ctx.whLoading}
                >
                  <span
                    className="material-symbols-outlined pf-retention-action-icon"
                  >
                    receipt_long
                  </span>
                  Emitir
                </ZHBtn>
              )}
            </>
          )}
          {ctx.withholding && ctx.withholding.status === "Issued" && (
            <ZHBtn
              type="button"
              variant="destructive"
              size="sm"
              onClick={() => ctx.setModalWhCancel(true)}
              disabled={ctx.whLoading}
            >
              <span
                className="material-symbols-outlined pf-retention-action-icon"
              >
                cancel
              </span>
              Anular
            </ZHBtn>
          )}
        </div>
      </div>
      <div className="pf-card__body">
        {ctx.whPreview && !ctx.withholding && (
          <>
            {ctx.whPreview.skipReason && (
              <div className="pf-retention__skip">
                {ctx.whPreview.skipReason}
              </div>
            )}
            {ctx.whPreview.lines.length > 0 && (
              <table className="table table--compact table--neutral">
                <thead>
                  <tr>
                    <th>Tipo</th>
                    <th>Código</th>
                    <th>Descripción</th>
                    <th className="zh-text-align-right">Base</th>
                    <th className="zh-text-align-right">%</th>
                    <th className="zh-text-align-right">Retenido</th>
                  </tr>
                </thead>
                <tbody>
                  {ctx.whPreview.lines.map((l, i) => (
                    <tr key={i}>
                      <td>
                        <Badge
                          variant={l.taxType === "IVA" ? "info" : "warning"}
                          label={l.taxType}
                        />
                      </td>
                      <td className="pf-retention-code">
                        {l.retentionCode}
                      </td>
                      <td>{l.retentionCodeName}</td>
                      <td className="zh-table-cell--num">
                        {formatMoneyWithSymbol(
                          l.taxableBase,
                          getDecimalConfig().totalAmount,
                        )}
                      </td>
                      <td className="zh-table-cell--num">{l.retentionPct}%</td>
                      <td className="zh-table-cell--num pf-retention-amount">
                        {formatMoneyWithSymbol(
                          l.amountRetained,
                          getDecimalConfig().totalAmount,
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            {ctx.whPreview.totalRetained > 0 && (
              <div className="pf-retention__total">
                Total a retener:{" "}
                {formatMoneyWithSymbol(
                  ctx.whPreview.totalRetained,
                  getDecimalConfig().totalAmount,
                )}
              </div>
            )}
          </>
        )}
        {ctx.withholding && (
          <>
            <div className="pf-retention__issued-meta">
              <span className="pf-retention__issued-item">
                Número: <strong>{ctx.withholding.withholdingNumber}</strong>
              </span>
              <span className="pf-retention__issued-item">
                Estado:{" "}
                <Badge
                  variant={
                    ctx.withholding.status === "Issued" ? "success" : "error"
                  }
                  label={ctx.withholding.status}
                />
              </span>
              <span className="pf-retention__issued-item">
                Fecha: <strong>{formatDate(ctx.withholding.issueDate)}</strong>
              </span>
            </div>
            <table className="table table--compact table--neutral">
              <thead>
                <tr>
                  <th>Tipo</th>
                  <th>Código</th>
                  <th>Descripción</th>
                  <th className="zh-text-align-right">Base</th>
                  <th className="zh-text-align-right">%</th>
                  <th className="zh-text-align-right">Retenido</th>
                </tr>
              </thead>
              <tbody>
                {ctx.withholding.details.map((d) => (
                  <tr key={d.id}>
                    <td>
                      <Badge
                        variant={d.taxType === "IVA" ? "info" : "warning"}
                        label={d.taxType}
                      />
                    </td>
                    <td className="pf-retention-code">
                      {d.retentionCode}
                    </td>
                    <td>{d.retentionCodeDescription}</td>
                    <td className="zh-table-cell--num">
                      {formatMoneyWithSymbol(
                        d.taxableBase,
                        getDecimalConfig().totalAmount,
                      )}
                    </td>
                    <td className="zh-table-cell--num">{d.retentionPct}%</td>
                    <td className="zh-table-cell--num pf-retention-amount">
                      {formatMoneyWithSymbol(
                        d.amountRetained,
                        getDecimalConfig().totalAmount,
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="pf-retention__issued-totals">
              <TotalMiniCard
                label="Ret. IVA"
                value={ctx.withholding.totalRetainedVat}
                color="var(--color-primary)"
              />
              <TotalMiniCard
                label="Ret. Renta"
                value={ctx.withholding.totalRetainedIncome}
                color="var(--color-warning)"
              />
              <TotalMiniCard
                label="TOTAL RETENIDO"
                value={ctx.withholding.totalRetained}
                color="var(--color-error)"
                highlight
              />
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function SummaryPanel({ ctx }: { ctx: ReturnType<typeof usePurchasesPage> }) {
  return (
    <div className="pf-totals">
      <div className="pf-totals__header">
        <h4>
          <span
            className="material-symbols-outlined pf-totals__title-icon--primary"
          >
            summarize
          </span>{" "}
          Resumen
        </h4>
      </div>
      <div className="pf-totals__body">
        <div className="pf-totals__row">
          <span className="pf-totals__label">Subtotal</span>
          <span className="pf-totals__value">
            {formatMoneyWithSymbol(
              ctx.editing ? ctx.editing.subtotal : ctx.localSummary.subtotal,
              getDecimalConfig().totalAmount,
            )}
          </span>
        </div>
        <div className="pf-totals__row">
          <span className="pf-totals__label">Descuento</span>
          <span
            className="pf-totals__value pf-totals__value--warning"
          >
            -
            {formatMoneyWithSymbol(
              ctx.editing
                ? ctx.editing.totalDiscount
                : ctx.localSummary.discount,
              getDecimalConfig().totalAmount,
            )}
          </span>
        </div>
        <div className="pf-totals__row">
          <span className="pf-totals__label">ICE</span>
          <span className="pf-totals__value">
            {formatMoneyWithSymbol(
              ctx.editing ? ctx.editing.totalIce : ctx.localSummary.ice,
              getDecimalConfig().totalAmount,
            )}
          </span>
        </div>
        <div className="pf-totals__row">
          <span className="pf-totals__label">IVA</span>
          <span className="pf-totals__value">
            {formatMoneyWithSymbol(
              ctx.editing ? ctx.editing.totalVat : ctx.localSummary.vat,
              getDecimalConfig().totalAmount,
            )}
          </span>
        </div>
        {!!ctx.editing?.totalIrbpnr && (
          <div className="pf-totals__row">
            <span className="pf-totals__label">
              IRBPNR{" "}
              <span className="pf-totals__value--warning" title="Informativo — no incluido en el Gran Total mientras no exista soporte contable.">
                (i)
              </span>
            </span>
            <span className="pf-totals__value">
              {formatMoneyWithSymbol(
                ctx.editing.totalIrbpnr,
                getDecimalConfig().totalAmount,
              )}
            </span>
          </div>
        )}
        <div className="pf-totals__row">
          <span className="pf-totals__label">Flete</span>
          <span className="pf-totals__value">
            {formatMoneyWithSymbol(
              ctx.editing
                ? ctx.editing.totalFreight
                : ctx.formWatch.freightCost,
              getDecimalConfig().totalAmount,
            )}
          </span>
        </div>
        <div className="pf-totals__row">
          <span className="pf-totals__label">Otros Costos</span>
          <span className="pf-totals__value">
            {formatMoneyWithSymbol(
              ctx.editing
                ? ctx.editing.totalOtherCosts
                : ctx.formWatch.otherCosts,
              getDecimalConfig().totalAmount,
            )}
          </span>
        </div>
        <div className="pf-totals__divider" />
        <div className="pf-totals__row">
          <span className="pf-totals__label pf-totals__label--strong">
            Líneas
          </span>
          <span className="pf-totals__value">{ctx.lines.length}</span>
        </div>
      </div>
      <div className="pf-totals__grand">
        <span className="pf-totals__grand-label">TOTAL</span>
        <span className="pf-totals__grand-value">
          {formatMoneyWithSymbol(
            ctx.editing ? ctx.editing.grandTotal : ctx.localTotal,
            getDecimalConfig().totalAmount,
          )}
        </span>
      </div>
    </div>
  );
}

function TotalMiniCard({
  label,
  value,
  color,
  highlight,
}: {
  label: string;
  value: number;
  color?: string;
  highlight?: boolean;
}) {
  return (
    <div className={`pf-total-mini-card ${highlight ? "pf-total-mini-card--highlight" : "pf-total-mini-card--default"}`}>
      <div className="pf-total-mini-card__label">
        {label}
      </div>
      <div
        className={`pf-total-mini-card__value ${highlight ? "pf-total-mini-card__value--highlight" : "pf-total-mini-card__value--default"}`}
        data-tone={color === "var(--color-error)" ? "error" : "default"}
      >
        {formatMoneyWithSymbol(value, getDecimalConfig().totalAmount)}
      </div>
    </div>
  );
}
