/** Mensaje final de una importación confirmada sin filas con error, con singular para 1 fila. */
export function importSuccessMessage(
  importedRows: number,
  labelPlural: string,
  labelSingular?: string,
): string {
  return importedRows === 1 && labelSingular
    ? `Se importó 1 ${labelSingular} correctamente.`
    : `Se importaron ${importedRows} ${labelPlural} correctamente.`;
}
