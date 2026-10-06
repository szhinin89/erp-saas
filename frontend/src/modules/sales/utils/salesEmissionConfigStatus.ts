export type SalesConfigStatusLevel = "ready" | "review" | "incomplete";

export type SalesConfigIssueSeverity = "error" | "warning" | "info";

export interface SalesConfigIssue {
  /** error = bloquea emitir (integrado a `canEmit`); warning = conviene revisar, no bloquea;
   * info = dato informativo, nunca se muestra como alerta en la tarjeta. */
  severity: SalesConfigIssueSeverity;
  message: string;
}

export interface SalesConfigStatus {
  level: SalesConfigStatusLevel;
  /** Solo los issues bloqueantes (severity=error) — mismos textos que gobiernan `canEmit`. */
  missing: string[];
  /** Issues no bloqueantes (severity=warning). */
  warnings: string[];
  issues: SalesConfigIssue[];
}

export interface SalesConfigStatusInput {
  /** La caja abierta ya se valida como bloqueante propio (CashSessionNotice) — aquí solo se usa
   * para no reportar "tipo de emisión" mientras no hay caja que lo determine. */
  hasCashSession: boolean | null;
  /** Tipo de emisión EFECTIVO de la venta en pantalla (snapshot de la factura o caja abierta). */
  emissionType: string | null | undefined;
  docTypeCode: string | null | undefined;
  lines: readonly { _participatesInInventory?: boolean; warehouseId?: string | null }[];
  /** Forma de pago SRI por defecto (cabecera) — solo relevante para emisión electrónica. */
  defaultSriPaymentCode: string | null | undefined;
  payments: readonly { paymentMethodId: string; amount: number }[];
  paymentMethods: readonly {
    id: string;
    isCreditAllowed: boolean;
    sriPaymentMethodCode?: string | null;
  }[];
}

/**
 * POS-CONFIG-STATUS-SEVERITY-01 — única fuente de verdad del estado de "Configuración de venta".
 * Cada condición tiene una severidad explícita y SOLO las bloqueantes (`error`) se integran a
 * `canEmit` (useSalesPage) — una tarjeta roja con Emitir habilitado ya no es posible.
 *
 * Reglas (todas verificadas contra el backend real):
 * - Tipo de emisión sin resolver con caja abierta → error: la creación del borrador es fail-closed
 *   si el punto de emisión no existe (CreateSalesDraftHandler, POS-EMISSION-TYPE-SNAPSHOT-01).
 * - Línea de stock sin bodega → error: mismo criterio que `salesInvoiceSchema` (bloquea al emitir).
 * - Tipo de documento vacío → info: el backend usa "01" (Factura) por defecto.
 * - Forma de pago SRI → SOLO electrónica: el XML exige `SriPaymentMethodCode` de cabecera
 *   (SalesInvoiceElectronicDocumentDataProvider). La cabecera se sincroniza al autorizar cuando
 *   toda la venta es un único método no-crédito con mapeo propio (AuthorizeSalesInvoiceHandler);
 *   en cualquier otro caso hace falta el default de empresa → error si no existe. Una factura
 *   física nunca se vuelve electrónica (snapshot inmutable), así que en física no aplica.
 * Cliente / caja abierta / cobro / stock no viven aquí: tienen su propio bloqueante en el hook.
 */
export function computeSalesConfigStatus(
  input: SalesConfigStatusInput,
): SalesConfigStatus {
  const issues: SalesConfigIssue[] = [];

  if (input.hasCashSession === true && !input.emissionType)
    issues.push({
      severity: "error",
      message: "Tipo de emisión (punto de emisión de la caja sin configurar)",
    });

  if (input.lines.some((l) => l._participatesInInventory && !l.warehouseId))
    issues.push({ severity: "error", message: "Bodega" });

  if (!input.docTypeCode)
    issues.push({
      severity: "info",
      message: "Sin tipo de documento seleccionado — se emitirá como Factura.",
    });

  if (input.emissionType === "Electronic") {
    const sriIssue = electronicSriPaymentIssue(input);
    if (sriIssue) issues.push(sriIssue);
  }

  const missing = issues.filter((i) => i.severity === "error").map((i) => i.message);
  const warnings = issues.filter((i) => i.severity === "warning").map((i) => i.message);
  let level: SalesConfigStatusLevel = "ready";
  if (missing.length > 0) level = "incomplete";
  else if (warnings.length > 0) level = "review";

  return { level, missing, warnings, issues };
}

/** Regla de Forma de pago SRI de cabecera — solo emisión electrónica (ver doc arriba). */
function electronicSriPaymentIssue(input: SalesConfigStatusInput): SalesConfigIssue | null {
  const defaultCode = input.defaultSriPaymentCode?.trim() || null;
  const inUse = input.payments.filter((p) => p.amount > 0);
  const methodsInUse = [...new Set(inUse.map((p) => p.paymentMethodId))].map((id) =>
    input.paymentMethods.find((m) => m.id === id),
  );

  if (defaultCode) {
    const anyUnmapped = methodsInUse.some((m) => !m?.sriPaymentMethodCode);
    return anyUnmapped
      ? {
          severity: "info",
          message:
            "Alguna forma de cobro no tiene mapeo SRI propio y usa la Forma Pago SRI por defecto.",
        }
      : null;
  }
  if (inUse.length === 0)
    return { severity: "warning", message: "No hay Forma Pago SRI por defecto configurada." };

  const single = methodsInUse.length === 1 ? methodsInUse[0] : undefined;
  const headerSyncedFromSingleMethod =
    !!single && !single.isCreditAllowed && !!single.sriPaymentMethodCode;
  return headerSyncedFromSingleMethod
    ? null
    : { severity: "error", message: "Forma de pago SRI por defecto" };
}
