/**
 * Descripción oficial de un código de catálogo para mostrar al usuario: el nombre del catálogo, o
 * `null` si el catálogo aún no cargó (no se muestra el código crudo mientras tanto), o `unknown(code)`
 * si el catálogo cargó y no contiene el código (valor persistido fuera de catálogo). Los códigos
 * nunca se modifican: solo cambia su presentación.
 */
export function describeCatalogCode(
  code: string | null | undefined,
  options: ReadonlyArray<{ code: string; name: string; shortName?: string }>,
  loading: boolean,
  unknown: (code: string) => string,
  /** `shortName` solo para cabeceras/espacios compactos; por defecto la descripción completa. */
  field: "name" | "shortName" = "name",
): string | null {
  if (!code) return null;
  const option = options.find((o) => o.code === code);
  const found = (field === "shortName" ? option?.shortName : undefined) || option?.name;
  if (found) return found;
  return loading || options.length === 0 ? null : unknown(code);
}
