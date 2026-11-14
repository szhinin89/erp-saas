import type {
  ExpenseDocumentHeaderErrors,
  ExpenseDocumentHeaderState,
} from "../components/ExpenseDocumentHeader";
import type {
  ExpenseDraftLineState,
  ExpenseLineFieldErrors,
} from "../components/ExpenseDocumentLinesEditor";

/**
 * EXPENSE-UX-01 (B2) — asocia el mapa `campo → mensajes` de un 422 del backend con los campos de la
 * cabecera y de cada línea del formulario de gastos.
 *
 * Contrato real del backend (ExceptionMiddleware + FluentValidation): la clave es el `PropertyName`
 * con SOLO la primera letra en minúscula, p. ej. `supplierId`, `lines[2].Quantity`, `lines[1]`
 * (regla a nivel de línea sin propiedad) o `lines` (colección). Los errores de negocio de los
 * handlers (subcategoría inactiva, código IVA inexistente…) llegan como lista plana (`_`) sin
 * información de línea: no se pueden asociar y quedan en `unmapped` para el mensaje general.
 */
export interface ExpenseBackendErrorMap {
  header: ExpenseDocumentHeaderErrors;
  lines: ExpenseLineFieldErrors;
  /** Mensajes sin campo asociable; los de una línea conocida ya llevan su prefijo de línea. */
  unmapped: string[];
}

const HEADER_FIELDS: ReadonlySet<keyof ExpenseDocumentHeaderState> = new Set([
  "supplierId",
  "issueDate",
  "accountingDate",
  "documentType",
  "documentNumber",
  "paymentTermId",
  "dueDate",
  "authorizationNumber",
  "authorizationDate",
  "notes",
  "taxSupportCode",
]);

const LINE_FIELDS: ReadonlySet<keyof ExpenseDraftLineState> = new Set([
  "expenseSubcategoryId",
  "description",
  "quantity",
  "unitPrice",
  "discountValue",
  "vatCode",
  "notes",
]);

const LINE_KEY = /^lines\[(\d+)\](?:\.(\w+))?$/i;

function lowerFirst(value: string): string {
  return value.length === 0 ? value : value[0].toLowerCase() + value.slice(1);
}

function firstMessage(messages: readonly string[] | undefined): string | undefined {
  return messages?.map((m) => m?.trim()).find((m): m is string => !!m);
}

/**
 * @param validation mapa devuelto por `parseValidationErrors`.
 * @param lineKeys   `key` de cada línea, en el MISMO orden en que se enviaron al backend.
 * @param formatLineMessage compone "Línea N: mensaje" para errores de una línea sin campo asociable.
 */
export function mapExpenseBackendErrors(
  validation: Record<string, string[]>,
  lineKeys: readonly string[],
  formatLineMessage: (lineNumber: number, message: string) => string,
): ExpenseBackendErrorMap {
  const result: ExpenseBackendErrorMap = { header: {}, lines: {}, unmapped: [] };

  for (const [rawKey, messages] of Object.entries(validation)) {
    const message = firstMessage(messages);
    if (!message) continue;

    const lineMatch = LINE_KEY.exec(rawKey);
    if (lineMatch) {
      const index = Number(lineMatch[1]);
      const lineKey = lineKeys[index];
      if (lineKey === undefined) {
        result.unmapped.push(message);
        continue;
      }
      const field = lineMatch[2] ? lowerFirst(lineMatch[2]) : null;
      if (field && LINE_FIELDS.has(field as keyof ExpenseDraftLineState)) {
        const current = result.lines[lineKey] ?? {};
        current[field as keyof ExpenseDraftLineState] ??= message;
        result.lines[lineKey] = current;
      } else {
        result.unmapped.push(formatLineMessage(index + 1, message));
      }
      continue;
    }

    const headerField = lowerFirst(rawKey);
    if (HEADER_FIELDS.has(headerField as keyof ExpenseDocumentHeaderState)) {
      result.header[headerField as keyof ExpenseDocumentHeaderState] ??= message;
    } else {
      result.unmapped.push(message);
    }
  }

  return result;
}

export function hasMappedFieldErrors(map: ExpenseBackendErrorMap): boolean {
  return Object.keys(map.header).length > 0 || Object.keys(map.lines).length > 0;
}
