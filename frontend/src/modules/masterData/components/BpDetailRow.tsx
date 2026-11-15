/** Fila etiqueta/valor de solo lectura de la ficha de Business Partner. */
export function BpDetailRow({
  label,
  value,
}: {
  label: string;
  value?: string | null;
}) {
  return (
    <div className="md-detail-row">
      <span className="md-detail-label">{label}</span>
      <span className="md-detail-value">
        {value ?? <em className="md-detail-empty">—</em>}
      </span>
    </div>
  );
}
