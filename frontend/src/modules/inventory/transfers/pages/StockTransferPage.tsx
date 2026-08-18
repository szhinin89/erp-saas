import { Link } from "react-router-dom";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHGrid, ZHField, ZHBtn } from "../../../../components/zh/ZHForm";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHFieldLabel } from "../../../../components/zh/ZHFieldLabel";
import { ZHInfoRow } from "../../../../components/zh/ZHInfoRow";
import { ZHDataValue } from "../../../../components/zh/ZHDataValue";
import { ZHLineCard } from "../../../../components/zh/ZHLineCard";
import { ZHRowDeleteAction } from "../../../../components/zh/ZHRowDeleteAction";
import { ZhWarehouseSelector } from "../../../../components/zh/inputs/ZhWarehouseSelector";
import { ZhDecimalInput } from "../../../../components/zh/inputs/ZhDecimalInput";
import { ZhTextInput } from "../../../../components/zh/inputs/ZhTextInput";
import { Badge, EmptyState } from "../../../../components/PageShell";
import { TransferProductPicker } from "../components/TransferProductPicker";
import { useStockTransferPage } from "../hooks/useStockTransferPage";
import "./StockTransferPage.css";

function statusBadge(status: string): { label: string; variant: "orange" | "green" | "gray" } {
  if (status === "Confirmed") return { label: "Confirmada", variant: "green" };
  if (status === "Cancelled") return { label: "Anulada", variant: "gray" };
  return { label: "Borrador", variant: "orange" };
}

function availabilityBadge(
  available: number,
  quantity: number,
): { label: string; variant: "red" | "orange" | "green" } {
  if (quantity > available) return { label: `Disp. ${available}`, variant: "red" };
  if (available <= 5) return { label: `Disp. ${available}`, variant: "orange" };
  return { label: `Disp. ${available}`, variant: "green" };
}

/**
 * P1-INVENTORY-WAREHOUSE-TRANSFER-UI-01 / -POLISH-01 — pantalla independiente de
 * transferencias entre bodegas, consumiendo el backend existente (POST
 * /inventory/stock/transfers + /transfers/{id}/confirm). No es un modal de Kardex ni se
 * mezcla con ajustes de inventario. Backend es la autoridad: esta UI solo previene
 * combinaciones inválidas antes de enviar (origen=destino, líneas vacías, cantidad<=0,
 * cantidad>stock conocido) — el backend vuelve a validar todo al confirmar.
 * Ancho máximo (`.itf-page`) y tarjetas ZHCard son puro ajuste visual — sin cambios de
 * lógica ni de contrato respecto a la versión anterior.
 */
export function StockTransferPage() {
  const ctx = useStockTransferPage();

  const badge = ctx.transfer ? statusBadge(ctx.transfer.status) : null;

  return (
    <ErpPageTemplate
      title="Transferencias entre bodegas"
      subtitle="Mueve stock entre bodegas de la misma empresa manteniendo trazabilidad en Kardex."
    >
      <div className="itf-page">
        {ctx.error && (
          <ZHPageNotice variant="error" message="Error" detail={ctx.error} />
        )}
        {ctx.successMessage && (
          <ZHPageNotice variant="success" message={ctx.successMessage} />
        )}

        <ZHCard title="Origen y destino">
          <ZHGrid cols={2}>
            <ZHField
              label="Bodega origen"
              required
              hint={
                ctx.sourceWarehouseId
                  ? (ctx.branchNameForWarehouse(ctx.sourceWarehouseId) ??
                    "Sucursal no disponible")
                  : undefined
              }
            >
              <ZhWarehouseSelector
                value={ctx.sourceWarehouseId || null}
                onChange={(id) => ctx.setSourceWarehouseId(id)}
                fallbackWarehouses={ctx.warehouses}
                disabled={ctx.formLocked}
                placeholder="Seleccione bodega origen"
              />
            </ZHField>
            <ZHField
              label="Bodega destino"
              required
              hint={
                ctx.targetWarehouseId
                  ? (ctx.branchNameForWarehouse(ctx.targetWarehouseId) ??
                    "Sucursal no disponible")
                  : undefined
              }
            >
              <ZhWarehouseSelector
                value={ctx.targetWarehouseId || null}
                onChange={(id) => ctx.setTargetWarehouseId(id)}
                fallbackWarehouses={ctx.warehouses}
                disabled={ctx.formLocked}
                placeholder="Seleccione bodega destino"
              />
            </ZHField>
          </ZHGrid>
          {ctx.sameWarehouse && (
            <ZHPageNotice
              variant="warning"
              message="La bodega origen y destino no pueden ser la misma."
            />
          )}
        </ZHCard>

        <ZHCard title="Productos">
          {!ctx.formLocked && (
            <div className="itf-picker-wrap">
              <TransferProductPicker
                onSelect={(p) => void ctx.addLine(p)}
                disabled={ctx.formLocked}
              />
            </div>
          )}

          {ctx.lines.length === 0 ? (
            <EmptyState message="Agregue al menos un producto para transferir." />
          ) : (
            <div className="itf-lines">
              {ctx.lines.map((line, index) => (
                <ZHLineCard
                  key={line._key}
                  className="itf-line"
                  rail={
                    <>
                      <span className="itf-line__index zh-text-muted zh-text-xs">
                        {String(index + 1).padStart(2, "0")}
                      </span>
                      {!ctx.formLocked && (
                        <ZHRowDeleteAction
                          compact
                          showText={false}
                          title="Quitar línea"
                          ariaLabel={`Quitar ${line.name}`}
                          onClick={() => ctx.removeLine(line._key)}
                        />
                      )}
                    </>
                  }
                >
                  <div className="itf-line__main">
                    <div className="itf-line__info">
                      <div className="itf-line__title-row">
                        {line.sku && (
                          <span className="itf-line__code zh-code-value">{line.sku}</span>
                        )}
                      </div>
                      <div className="itf-line__name zh-row-title" title={line.name}>
                        {line.name}
                      </div>
                    </div>

                    <div className="itf-line__qty">
                      <ZHFieldLabel size="sm">Cantidad</ZHFieldLabel>
                      <ZhDecimalInput
                        decimals={2}
                        positiveOnly
                        density="compact"
                        defaultValue={line.quantity}
                        disabled={ctx.formLocked}
                        onBlur={(e) =>
                          ctx.updateLineQuantity(
                            line._key,
                            Number(e.target.value) || 0,
                          )
                        }
                      />
                    </div>

                    {line.availableAtSource !== null && (
                      <div className="itf-line__stock">
                        <ZHFieldLabel size="sm">En origen</ZHFieldLabel>
                        <Badge
                          {...availabilityBadge(line.availableAtSource, line.quantity)}
                        />
                      </div>
                    )}
                  </div>
                </ZHLineCard>
              ))}
            </div>
          )}
        </ZHCard>

        {!ctx.formLocked && (
          <ZHCard title="Detalles de la transferencia" className="itf-details">
            <ZHGrid cols={2}>
              <ZHField label="Motivo">
                <ZhTextInput
                  value={ctx.reason}
                  onChange={(e) => ctx.setReason(e.target.value)}
                  maxLength={200}
                  placeholder="Ej. Reabastecimiento sucursal"
                />
              </ZHField>
              <ZHField label="Notas">
                <ZhTextInput
                  value={ctx.notes}
                  onChange={(e) => ctx.setNotes(e.target.value)}
                  maxLength={500}
                />
              </ZHField>
            </ZHGrid>
          </ZHCard>
        )}

        <ZHCard title="Resumen" className="itf-summary">
          <ZHInfoRow
            label={<ZHFieldLabel size="sm">Líneas</ZHFieldLabel>}
            value={<ZHDataValue variant="numeric">{ctx.lines.length}</ZHDataValue>}
          />
          <ZHInfoRow
            label={<ZHFieldLabel size="sm">Total unidades</ZHFieldLabel>}
            value={<ZHDataValue variant="numeric">{ctx.totalUnits}</ZHDataValue>}
          />
          <ZHInfoRow
            label={<ZHFieldLabel size="sm">Origen → Destino</ZHFieldLabel>}
            value={
              <ZHDataValue>
                {ctx.sourceWarehouse?.name ?? "—"} → {ctx.targetWarehouse?.name ?? "—"}
              </ZHDataValue>
            }
          />
          {ctx.transfer && badge && (
            <ZHInfoRow
              label={<ZHFieldLabel size="sm">Estado</ZHFieldLabel>}
              value={<Badge label={badge.label} variant={badge.variant} />}
            />
          )}
          {ctx.transfer && (
            <ZHInfoRow
              label={<ZHFieldLabel size="sm">N.º transferencia</ZHFieldLabel>}
              value={<ZHDataValue variant="code">{ctx.transfer.transferNumber}</ZHDataValue>}
            />
          )}
        </ZHCard>

        <div className="zh-form-actions-row zh-form-actions-row--end itf-actions">
          {!ctx.transfer ? (
            <>
              <ZHBtn
                variant="ghost"
                size="sm"
                type="button"
                onClick={ctx.resetForm}
                disabled={ctx.creating}
              >
                Limpiar
              </ZHBtn>
              <ZHBtn
                variant="primary"
                size="sm"
                type="button"
                onClick={() => void ctx.createTransfer()}
                disabled={ctx.creating}
              >
                {ctx.creating ? "Creando..." : "Crear transferencia"}
              </ZHBtn>
            </>
          ) : ctx.isDraft ? (
            <>
              <ZHBtn
                variant="ghost"
                size="sm"
                type="button"
                onClick={ctx.resetForm}
                disabled={ctx.confirming}
              >
                Nueva transferencia
              </ZHBtn>
              <ZHBtn
                variant="primary"
                size="sm"
                type="button"
                onClick={() => void ctx.confirmTransfer()}
                disabled={ctx.confirming}
              >
                {ctx.confirming ? "Confirmando..." : "Confirmar transferencia"}
              </ZHBtn>
            </>
          ) : (
            <>
              <Link
                to={`/inventory/kardex?warehouseId=${ctx.sourceWarehouseId}`}
                className="zh-link"
              >
                Ver Kardex — bodega origen
              </Link>
              <Link
                to={`/inventory/kardex?warehouseId=${ctx.targetWarehouseId}`}
                className="zh-link"
              >
                Ver Kardex — bodega destino
              </Link>
              <ZHBtn variant="primary" size="sm" type="button" onClick={ctx.resetForm}>
                Nueva transferencia
              </ZHBtn>
            </>
          )}
        </div>
      </div>
    </ErpPageTemplate>
  );
}
