import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhNumberInput } from "../../../components/zh/inputs/ZhNumberInput";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import {
  Badge,
  PageShell,
  type BadgeVariant,
} from "../../../components/PageShell";
import { formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import { useCajaPage } from "../hooks/useCajaPage";
import "../../../styles/shared/erp-form-core.css";
import "../../../styles/shared/items-catalog.css";
import "./CajaPage.css";

export function CajaPage() {
  const ctx = useCajaPage();

  const statusLabel = (s: string) => (s === "Open" ? "Abierta" : "Cerrada");
  const statusBadge = (s: string): BadgeVariant =>
    s === "Open" ? "success" : "neutral";

  const movementTypeLabel = (t: string) => {
    const map: Record<string, string> = {
      Opening: "Apertura",
      SaleIncome: "Venta",
      ManualIncome: "Ingreso",
      ManualExpense: "Egreso",
      Withdrawal: "Retiro",
    };
    return map[t] ?? t;
  };

  return (
    <PageShell title="Caja" kicker="Gestión de efectivo">
      <div className="cj-content">
        {ctx.mySession && ctx.tab === "listado" && (
          <ZHPageNotice
            variant="info"
            message={`Caja abierta — Saldo: ${formatMoneyWithSymbol(ctx.mySession.currentBalance)}`}
          />
        )}

        {ctx.saveError && (
          <ZHPageNotice variant="error" message={ctx.saveError} />
        )}

        {/* ═══════════════════════ LISTADO ═══════════════════════════ */}
        {ctx.tab === "listado" && (
          <div className="prd-section">
            <div className="cj-toolbar">
              {!ctx.mySession && (
                <ZHBtn
                  type="button"
                  variant="primary"
                  onClick={() => ctx.setTab("abrir")}
                >
                  <span className="material-symbols-outlined zh-icon-md">
                    add
                  </span>
                  Abrir Caja
                </ZHBtn>
              )}
              <div className="cj-toolbar-spacer" />
              <select
                value={ctx.statusFilter}
                onChange={(e) => ctx.setStatusFilter(e.target.value)}
                className="cj-filter-select"
              >
                <option value="">Todos</option>
                <option value="Open">Abiertas</option>
                <option value="Closed">Cerradas</option>
              </select>
              <ZHBtn
                variant="secondary"
                onClick={ctx.fetchList}
                disabled={ctx.listLoading}
              >
                <span className="material-symbols-outlined zh-icon-md">
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
                    <th>Apertura</th>
                    <th className="zh-text-align-right">Monto Apertura</th>
                    <th className="zh-text-align-right">Saldo</th>
                    <th className="zh-text-align-center">Movimientos</th>
                    <th>Estado</th>
                    <th>Cierre</th>
                    <th className="zh-text-align-right">Diferencia</th>
                    <th className="zh-text-align-center">Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {ctx.listItems.map((s) => (
                    <tr key={s.id}>
                      <td>{formatDateTime(s.openedAt)}</td>
                      <td className="zh-table-cell--num">
                        {formatMoneyWithSymbol(s.openingAmount)}
                      </td>
                      <td className="zh-table-cell--num">
                        {formatMoneyWithSymbol(s.currentBalance)}
                      </td>
                      <td className="zh-text-align-center">{s.movementCount}</td>
                      <td>
                        <Badge
                          variant={statusBadge(s.status)}
                          label={statusLabel(s.status)}
                        />
                      </td>
                      <td>{s.closedAt ? formatDateTime(s.closedAt) : "—"}</td>
                      <td className="zh-table-cell--num">
                        {s.difference != null
                          ? formatMoneyWithSymbol(s.difference)
                          : "—"}
                      </td>
                      <td className="zh-text-align-center">
                        <ZHIconButton
                          icon="visibility"
                          variant="ghost"
                          title="Ver detalle"
                          onClick={() => ctx.loadDetail(s.id)}
                        />
                      </td>
                    </tr>
                  ))}
                  {ctx.listItems.length === 0 && (
                    <tr>
                      <td colSpan={8} className="zh-table-empty">
                        Sin sesiones de caja.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            )}
          </div>
        )}

        {/* ═══════════════════════ ABRIR CAJA ════════════════════════ */}
        {ctx.tab === "abrir" && (
          <div className="prd-section cj-form-narrow">
            <h3 className="cj-section-title">Abrir Caja</h3>
            {ctx.cashRegisters.length === 0 && (
              <ZHPageNotice
                variant="warning"
                message="No hay cajas disponibles en la sucursal activa. Contacte al administrador para configurar una."
              />
            )}
            <form onSubmit={ctx.handleOpen}>
              <ZHField
                density="compact"
                className="zh-mb-12"
                label="Sucursal activa"
              >
                <input
                  type="text"
                  value={ctx.branchName ?? "—"}
                  readOnly
                  disabled
                />
              </ZHField>

              <ZHField
                density="compact"
                className="zh-mb-12"
                label="Caja"
                required
                fieldError={
                  ctx.openForm.formState.errors.cashRegisterId?.message
                }
              >
                <select
                  {...ctx.openForm.register("cashRegisterId")}
                  disabled={ctx.cashRegisters.length === 0}
                >
                  <option value="">Seleccione...</option>
                  {ctx.cashRegisters.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.code} — {r.name}
                    </option>
                  ))}
                </select>
              </ZHField>

              {/* Confirmación visual antes de abrir: Sucursal/Establecimiento/Punto de emisión
                  son solo informativos, provienen del mismo CashRegisterDto que llena el <select>
                  de arriba — no se puede editar ni se hace una petición adicional. */}
              {ctx.selectedRegister && (
                <div className="cj-summary-grid zh-mb-12">
                  <SummaryCard
                    label="Sucursal"
                    value={ctx.selectedRegister.branchName}
                  />
                  <SummaryCard
                    label="Establecimiento"
                    value={ctx.selectedRegister.establishmentCode ?? "—"}
                  />
                  <SummaryCard
                    label="Punto de emisión"
                    value={ctx.selectedRegister.emissionPointCode ?? "—"}
                  />
                </div>
              )}

              <ZHField
                density="compact"
                className="zh-mb-12"
                label="Monto de apertura"
                required
                fieldError={
                  ctx.openForm.formState.errors.openingAmount?.message
                }
              >
                <ZhDecimalInput
                  {...ctx.openForm.register("openingAmount")}
                  decimals={2}
                  positiveOnly
                />
              </ZHField>

              <ZHField density="compact" className="zh-mb-12" label="Notas">
                <textarea {...ctx.openForm.register("notes")} rows={2} />
              </ZHField>

              <div className="cj-actions">
                <ZHBtn
                  variant="primary"
                  type="submit"
                  disabled={ctx.saving || ctx.cashRegisters.length === 0}
                >
                  {ctx.saving ? "Abriendo..." : "Abrir Caja"}
                </ZHBtn>
                <ZHBtn
                  variant="secondary"
                  onClick={() => ctx.setTab("listado")}
                >
                  Cancelar
                </ZHBtn>
              </div>
            </form>
          </div>
        )}

        {/* ═══════════════════════ DETALLE ═══════════════════════════ */}
        {ctx.tab === "detalle" && ctx.viewing && (
          <div className="prd-section">
            <div className="cj-detail-header">
              <ZHBtn
                variant="secondary"
                size="sm"
                onClick={() => {
                  ctx.setTab("listado");
                }}
              >
                <span className="material-symbols-outlined zh-icon-md">
                  arrow_back
                </span>{" "}
                Volver
              </ZHBtn>
              <h3>Sesión de Caja</h3>
              <Badge
                variant={statusBadge(ctx.viewing.status)}
                label={statusLabel(ctx.viewing.status)}
              />
              <div className="cj-detail-header-spacer" />
              {ctx.viewing.status === "Open" && (
                <ZHBtn variant="destructive" onClick={ctx.startClose}>
                  Cerrar Caja
                </ZHBtn>
              )}
            </div>

            {/* Sucursal / Caja / Punto de emisión: solo informativos. El punto de emisión viene
                exclusivamente de CashSessionDto (emissionPointCodeSnapshot) — nunca de un lookup
                manual a EmissionPoint. */}
            <div className="cj-summary-grid">
              <SummaryCard label="Sucursal" value={ctx.branchName ?? "—"} />
              <SummaryCard
                label="Caja"
                value={`${ctx.viewing.cashRegisterCodeSnapshot} — ${ctx.viewing.cashRegisterNameSnapshot}`}
              />
              <SummaryCard
                label="Punto de emisión"
                value={ctx.viewing.emissionPointCodeSnapshot}
              />
              <SummaryCard
                label="Estado"
                value={statusLabel(ctx.viewing.status)}
              />
            </div>

            <div className="cj-summary-grid">
              <SummaryCard
                label="Apertura"
                value={formatMoneyWithSymbol(ctx.viewing.openingAmount)}
              />
              <SummaryCard
                label="Ingresos"
                value={formatMoneyWithSymbol(ctx.viewing.totalIncome)}
              />
              <SummaryCard
                label="Egresos"
                value={formatMoneyWithSymbol(ctx.viewing.totalExpense)}
              />
              <SummaryCard
                label="Saldo actual"
                value={formatMoneyWithSymbol(ctx.viewing.currentBalance)}
                highlight
              />
              {ctx.viewing.status === "Closed" && (
                <>
                  <SummaryCard
                    label="Esperado"
                    value={formatMoneyWithSymbol(
                      ctx.viewing.expectedAmount ?? 0,
                    )}
                  />
                  <SummaryCard
                    label="Contado"
                    value={formatMoneyWithSymbol(
                      ctx.viewing.countedAmount ?? 0,
                    )}
                  />
                  <SummaryCard
                    label="Diferencia"
                    value={formatMoneyWithSymbol(ctx.viewing.difference ?? 0)}
                    highlight={(ctx.viewing.difference ?? 0) !== 0}
                  />
                </>
              )}
            </div>

            {ctx.viewing.status === "Open" && (
              <div className="cj-movement-form-wrap">
                <h4>Registrar movimiento</h4>
                <form
                  onSubmit={ctx.handleRecordMovement}
                  className="cj-movement-form"
                >
                  <ZHField
                    density="compact"
                    className="cj-movement-field--type"
                    label="Tipo"
                    required
                    fieldError={
                      ctx.movementForm.formState.errors.movementType?.message
                    }
                  >
                    <select {...ctx.movementForm.register("movementType")}>
                      <option value="">Seleccione...</option>
                      {ctx.movementTypes.map((mt) => (
                        <option key={mt.value} value={mt.value}>
                          {mt.label}
                        </option>
                      ))}
                    </select>
                  </ZHField>
                  <ZHField
                    density="compact"
                    className="cj-movement-field--amount"
                    label="Monto"
                    required
                    fieldError={
                      ctx.movementForm.formState.errors.amount?.message
                    }
                  >
                    <ZhDecimalInput
                      {...ctx.movementForm.register("amount")}
                      decimals={2}
                      positiveOnly
                    />
                  </ZHField>
                  <ZHField
                    density="compact"
                    className="cj-movement-field--desc"
                    label="Descripción"
                    required
                    fieldError={
                      ctx.movementForm.formState.errors.description?.message
                    }
                  >
                    <input
                      type="text"
                      {...ctx.movementForm.register("description")}
                    />
                  </ZHField>
                  <ZHBtn variant="primary" type="submit" disabled={ctx.saving}>
                    Registrar
                  </ZHBtn>
                </form>
              </div>
            )}

            <h4 className="cj-section-title">Movimientos</h4>
            <table className="table table--compact table--neutral">
              <thead>
                <tr>
                  <th>Fecha</th>
                  <th>Tipo</th>
                  <th>Descripción</th>
                  <th className="zh-text-align-right">Monto</th>
                  <th>Referencia</th>
                </tr>
              </thead>
              <tbody>
                {ctx.viewing.movements.map((m) => (
                  <tr key={m.id}>
                    <td>{formatDateTime(m.createdAt)}</td>
                    <td>{movementTypeLabel(m.movementType)}</td>
                    <td>{m.description}</td>
                    <td className="zh-table-cell--num">
                      {formatMoneyWithSymbol(m.amount)}
                    </td>
                    <td>{m.referenceNumber ?? "—"}</td>
                  </tr>
                ))}
                {ctx.viewing.movements.length === 0 && (
                  <tr>
                    <td colSpan={5} className="zh-table-empty">
                      Sin movimientos.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>

            {ctx.viewing.status === "Closed" &&
              ctx.viewing.closingCounts.length > 0 && (
                <>
                  <h4 className="cj-section-title cj-section-title--spaced">
                    Arqueo
                  </h4>
                  <table className="table table--compact table--neutral cj-arqueo-table">
                    <thead>
                      <tr>
                        <th>Denominación</th>
                        <th className="zh-text-align-center">Cantidad</th>
                        <th className="zh-text-align-right">Total</th>
                      </tr>
                    </thead>
                    <tbody>
                      {ctx.viewing.closingCounts.map((c) => (
                        <tr key={c.id}>
                          <td>{c.denominationLabel}</td>
                          <td className="zh-text-align-center">{c.quantity}</td>
                          <td className="zh-table-cell--num">
                            {formatMoneyWithSymbol(c.total)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  {ctx.viewing.closeNotes && (
                    <p className="cj-close-notes">
                      <strong>Notas:</strong> {ctx.viewing.closeNotes}
                    </p>
                  )}
                </>
              )}
          </div>
        )}

        {/* ═══════════════════════ CERRAR CAJA ═══════════════════════ */}
        {ctx.tab === "cerrar" && ctx.viewing && (
          <div className="prd-section cj-close-narrow">
            <div className="cj-detail-header">
              <ZHBtn
                variant="secondary"
                size="sm"
                onClick={() => ctx.setTab("detalle")}
              >
                <span className="material-symbols-outlined zh-icon-md">
                  arrow_back
                </span>{" "}
                Volver
              </ZHBtn>
              <h3>Cerrar Caja — Arqueo</h3>
            </div>

            <div className="cj-close-summary">
              <div>
                <strong>Saldo esperado:</strong>{" "}
                {formatMoneyWithSymbol(ctx.viewing.currentBalance)}
              </div>
              <div>
                <strong>Contado:</strong>{" "}
                {formatMoneyWithSymbol(ctx.countedTotal)}
              </div>
              <div
                className={
                  ctx.countedTotal - ctx.viewing.currentBalance !== 0
                    ? "cj-close-diff--mismatch"
                    : "cj-close-diff--ok"
                }
              >
                <strong>Diferencia:</strong>{" "}
                {formatMoneyWithSymbol(
                  ctx.countedTotal - ctx.viewing.currentBalance,
                )}
              </div>
            </div>

            <form onSubmit={ctx.handleClose}>
              <table className="pf-table zh-mb-16">
                <thead>
                  <tr>
                    <th>Denominación</th>
                    <th className="zh-text-align-center">Cantidad</th>
                    <th className="zh-text-align-right">Total</th>
                  </tr>
                </thead>
                <tbody>
                  {ctx.closeForm.watch("closingCounts").map((c, i) => (
                    <tr key={c._key}>
                      <td>{c.denominationLabel}</td>
                      <td className="zh-text-align-center">
                        <ZhNumberInput
                          positiveOnly
                          className="cj-arqueo-input"
                          {...ctx.closeForm.register(
                            `closingCounts.${i}.quantity`,
                            { valueAsNumber: true },
                          )}
                        />
                      </td>
                      <td className="zh-table-cell--num">
                        {formatMoneyWithSymbol(
                          c.denominationValue * c.quantity,
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr>
                    <td colSpan={2} className="cj-arqueo-total">
                      Total contado
                    </td>
                    <td className="zh-table-cell--num cj-arqueo-total">
                      {formatMoneyWithSymbol(ctx.countedTotal)}
                    </td>
                  </tr>
                </tfoot>
              </table>

              <ZHField
                density="compact"
                className="zh-mb-12"
                label="Notas de cierre"
              >
                <textarea {...ctx.closeForm.register("closeNotes")} rows={2} />
              </ZHField>

              <div className="cj-actions">
                <ZHBtn
                  variant="destructive"
                  type="submit"
                  disabled={ctx.saving}
                >
                  {ctx.saving ? "Cerrando..." : "Confirmar Cierre"}
                </ZHBtn>
                <ZHBtn
                  variant="secondary"
                  onClick={() => ctx.setTab("detalle")}
                >
                  Cancelar
                </ZHBtn>
              </div>
            </form>
          </div>
        )}
      </div>
    </PageShell>
  );
}

function SummaryCard({
  label,
  value,
  highlight,
}: {
  label: string;
  value: string;
  highlight?: boolean;
}) {
  return (
    <div
      className={`cj-summary-card${highlight ? " cj-summary-card--highlight" : ""}`}
    >
      <div className="cj-summary-card__label">{label}</div>
      <div className="cj-summary-card__value">{value}</div>
    </div>
  );
}
