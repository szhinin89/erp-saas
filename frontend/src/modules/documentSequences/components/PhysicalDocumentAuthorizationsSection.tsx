import { EmptyState, LoadingState, Badge } from "../../../components/PageShell";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHField, ZHGrid, ZHBtn } from "../../../components/zh/ZHForm";
import { ZhDateInput, ZhNumberInput, ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import type { PhysicalDocumentAuthorizationDto } from "../api/physicalDocumentAuthorizationsService";
import { usePhysicalDocumentAuthorizations } from "../hooks/usePhysicalDocumentAuthorizations";

function formatSequential(value: number): string {
  return String(value).padStart(9, "0");
}

/**
 * PHYSICAL-FISCAL-F1 — autorizaciones de impresión del SRI de los puntos físicos. Reutiliza las
 * mismas piezas que la lista de secuencias (pg-section, ZHDataTable, ZHField/ZHGrid/ZHBtn, Badge,
 * Zh*Input, message.*): ningún componente nuevo de Design System. Es el destino de los requisitos
 * `physicalEmission.*` de Preparación de Datos y de la apertura de caja.
 */
export function PhysicalDocumentAuthorizationsSection() {
  const ctx = usePhysicalDocumentAuthorizations();
  if (!ctx.canManage) return null;

  const columns: ZHDataTableColumn<PhysicalDocumentAuthorizationDto>[] = [
    {
      key: "docTypeCode",
      header: "Tipo de comprobante",
      render: (row) => <span className="br-list-name">{ctx.docTypeName(row.docTypeCode)}</span>,
    },
    {
      key: "authorizationNumber",
      header: "N.º de autorización",
      render: (row) => <span className="mono">{row.authorizationNumber}</span>,
    },
    {
      key: "validity",
      header: "Vigencia",
      render: (row) => `${formatDate(row.authorizedOn)} – ${formatDate(row.expiresOn)}`,
    },
    {
      key: "range",
      header: "Rango autorizado",
      render: (row) => (
        <span className="mono">
          {formatSequential(row.firstSequential)} – {formatSequential(row.lastSequential)}
        </span>
      ),
    },
    {
      key: "isActive",
      header: "Estado",
      render: (row) => (
        <Badge
          label={row.isActive ? "Activa" : "Deshabilitada"}
          variant={row.isActive ? "info" : "gray"}
          size="md"
        />
      ),
    },
    {
      key: "actions",
      header: "Acción",
      align: "right",
      render: (row) =>
        row.isActive ? (
          <ZHBtn
            type="button"
            variant="ghost"
            size="sm"
            title="Deshabilitar"
            onClick={() => void ctx.disable(row)}
          >
            <span className="material-symbols-outlined">block</span>
            Deshabilitar
          </ZHBtn>
        ) : null,
    },
  ];

  return (
    <div className="pg-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">verified</span>
          <span className="pg-section-label">Autorizaciones de impresión del SRI (puntos físicos)</span>
        </div>
        <div className="br-actions-tight">
          <ZHBtn
            variant="secondary"
            size="sm"
            type="button"
            disabled={ctx.loading}
            onClick={() => void ctx.fetchAll()}
          >
            <span className="material-symbols-outlined">refresh</span>
            Actualizar
          </ZHBtn>
        </div>
      </div>

      <div className="pg-section-body">
        <ZHPageNotice
          variant="info"
          message="Registrar una autorización no habilita por sí sola la factura física: también debe permitirla el régimen tributario de la empresa."
          detail="Hoy ningún régimen tiene habilitada la factura física ordinaria en el ERP (los preimpresos solo proceden de forma excepcional, una circunstancia que el ERP no puede verificar). Los datos se registran tal como constan en la autorización del SRI; el ERP no la consulta ni la certifica."
        />
      </div>

      {ctx.error && (
        <div className="pg-pad-40">
          <EmptyState message={ctx.error} />
        </div>
      )}

      {ctx.loading ? (
        <div className="pg-pad-40">
          <LoadingState />
        </div>
      ) : ctx.physicalPoints.length === 0 ? (
        <div className="pg-pad-40">
          <EmptyState message="No hay puntos de emisión físicos activos." />
        </div>
      ) : (
        <>
          <div className="pg-table-controls">
            <div className="pg-table-controls-left">
              <ZhSelect
                className="zh-input"
                aria-label="Punto de emisión físico"
                value={ctx.selectedPointId ?? ""}
                onChange={(e) => ctx.setSelectedPointId(e.target.value || null)}
              >
                {ctx.physicalPoints.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.establishmentCode}-{p.code} — {p.name ?? p.code}
                  </option>
                ))}
              </ZhSelect>
            </div>
          </div>

          {ctx.rows.length === 0 ? (
            <div className="pg-pad-40">
              <EmptyState message="Este punto no tiene autorizaciones de impresión registradas." />
            </div>
          ) : (
            <ZHDataTable columns={columns} rows={ctx.rows} rowKey={(row) => row.id} />
          )}

          {ctx.saveError && (
            <div className="cfg-panel-error">
              <ZHPageNotice variant="error" message="Error" detail={ctx.saveError} />
            </div>
          )}

          <form
            onSubmit={(e) => {
              e.preventDefault();
              void ctx.save();
            }}
            noValidate
          >
            <div className="pg-section-body">
              <ZHGrid cols={3}>
                <ZHField label="Tipo de comprobante" required error={ctx.errors.docTypeCode?.message}>
                  <ZhSelect className="zh-input" disabled={ctx.saving} {...ctx.register("docTypeCode")}>
                    <option value="">— seleccionar —</option>
                    {ctx.docTypes.map((d) => (
                      <option key={d.code} value={d.code}>
                        {d.code} — {d.name}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
                <ZHField
                  label="N.º de autorización del SRI"
                  required
                  error={ctx.errors.authorizationNumber?.message}
                >
                  <ZhTextInput
                    className="zh-input mono"
                    inputMode="numeric"
                    maxLength={49}
                    disabled={ctx.saving}
                    {...ctx.register("authorizationNumber")}
                  />
                </ZHField>
                <ZHField label="Fecha de autorización" required error={ctx.errors.authorizedOn?.message}>
                  <ZhDateInput disabled={ctx.saving} {...ctx.register("authorizedOn")} />
                </ZHField>
                <ZHField
                  label="Fecha de caducidad"
                  required
                  error={ctx.errors.expiresOn?.message}
                  hint="Máximo un año desde la autorización."
                >
                  <ZhDateInput disabled={ctx.saving} {...ctx.register("expiresOn")} />
                </ZHField>
                <ZHField label="Primer secuencial" required error={ctx.errors.firstSequential?.message}>
                  <ZhNumberInput
                    className="zh-input mono"
                    positiveOnly
                    maxLength={9}
                    disabled={ctx.saving}
                    {...ctx.register("firstSequential")}
                  />
                </ZHField>
                <ZHField label="Último secuencial" required error={ctx.errors.lastSequential?.message}>
                  <ZhNumberInput
                    className="zh-input mono"
                    positiveOnly
                    maxLength={9}
                    disabled={ctx.saving}
                    {...ctx.register("lastSequential")}
                  />
                </ZHField>
              </ZHGrid>
            </div>
            <div className="pg-actions-bar">
              <div className="pg-actions-buttons">
                <ZHBtn variant="primary" type="submit" disabled={ctx.saving || !ctx.selectedPoint}>
                  {ctx.saving ? "Guardando…" : "Registrar autorización"}
                </ZHBtn>
              </div>
            </div>
          </form>
        </>
      )}
    </div>
  );
}
