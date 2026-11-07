import type { ReactNode } from "react";
import { ZHMoneyValue } from "../../../../components/zh/ZHMoneyValue";

/** Fila de un estado financiero: cuenta con importe (columna Detalle) o encabezado de grupo sin importe. */
export type StatementRow =
  | { kind: "account"; id: string; account: ReactNode; amount: number }
  | { kind: "group"; id: string; label: ReactNode };

/**
 * ACCOUNTING-REPORTS-PRO-15 — sección (`<tbody>`) de un estado financiero en formato de dos columnas
 * de importe dentro de una `<table className="table">` nativa (excepción documentada en frontend.md:
 * bandas de sección, filas de total y `<tfoot>` que `ZHDataTable` no expone):
 *  - banda de título (`th` oficial) a todo el ancho;
 *  - cuentas con su importe en la columna **Detalle**;
 *  - encabezados de grupo (cuenta padre del Plan de Cuentas) SIN importe: nunca se presenta el saldo
 *    propio de una agrupadora como si fuera consolidado y no se calculan subtotales por grupo (D1);
 *  - total de la sección en la columna **Total** (cada cifra aparece una sola vez).
 * Importes tal cual los entrega el backend: sin recálculo ni cambio de signo.
 */
export function FinancialStatementSection({
  title,
  rows,
  emptyMessage,
  totalLabel,
  total,
}: Readonly<{
  title: string;
  rows: StatementRow[];
  emptyMessage: string;
  totalLabel: string;
  total: number;
}>) {
  return (
    <tbody>
      <tr>
        <th colSpan={3} scope="colgroup">{title}</th>
      </tr>
      {rows.map((r) =>
        r.kind === "group" ? (
          <tr key={r.id}>
            <td colSpan={3} className="zh-text-muted">{r.label}</td>
          </tr>
        ) : (
          <tr key={r.id}>
            <td>{r.account}</td>
            <td className="zh-table-cell--num">
              <ZHMoneyValue precision="accounting" value={r.amount} />
            </td>
            <td />
          </tr>
        ),
      )}
      {rows.length === 0 && (
        <tr>
          <td colSpan={3} className="zh-text-muted">{emptyMessage}</td>
        </tr>
      )}
      <tr>
        <td><strong>{totalLabel}</strong></td>
        <td />
        <td className="zh-table-cell--num">
          <ZHMoneyValue precision="accounting" value={total} emphasis="total" />
        </td>
      </tr>
    </tbody>
  );
}

/** Encabezado de tabla de un estado financiero: Concepto/Cuenta | Detalle | Total. */
export function StatementHead({ first, detail, total }: Readonly<{ first: string; detail: string; total: string }>) {
  return (
    <thead>
      <tr>
        <th>{first}</th>
        <th className="zh-table-cell--num">{detail}</th>
        <th className="zh-table-cell--num">{total}</th>
      </tr>
    </thead>
  );
}

/** Celda de cuenta: código (chip oficial de código) + nombre. */
export function AccountLabel({ code, name }: Readonly<{ code: string; name: string }>) {
  return (
    <>
      <span className="zh-code-value zh-code-value--document">{code}</span> {name}
    </>
  );
}
