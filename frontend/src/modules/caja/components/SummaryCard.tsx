export function SummaryCard({
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
