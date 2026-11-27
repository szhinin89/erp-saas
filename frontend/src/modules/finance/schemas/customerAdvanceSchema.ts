import { z } from "zod";
import { formatMoney, roundToDecimals } from "../../../lib/sanitizers";
import { formatDate } from "../../../lib/formatters/dateFormatters";

/**
 * Validación de interfaz de Anticipos de clientes (SSOT docs/architecture/customer-advances.md).
 * Solo anticipa rechazos evidentes: el servidor decide saldos, cuotas, fechas y elegibilidad.
 */

type T = (key: string, params?: Record<string, string | number>) => string;

const REASON_MAX = 500;
const REFERENCE_MAX = 100;

const amountField = (t: T) =>
  z.coerce
    .number({ invalid_type_error: t("finance.customerAdvances.validation.amountRequired") })
    .positive(t("finance.customerAdvances.validation.amountPositive"));

const requiredText = (t: T, key: string, max: number) =>
  z
    .string()
    .trim()
    .min(1, t(key))
    .max(max, t("finance.customerAdvances.validation.maxLength", { max }));

const optionalText = (t: T, max: number) =>
  z.string().trim().max(max, t("finance.customerAdvances.validation.maxLength", { max }));

/** Fecha de negocio "YYYY-MM-DD" dentro de [min, max]; la comparación de texto respeta el orden. */
function checkDate(
  ctx: z.RefinementCtx,
  t: T,
  path: string,
  value: string,
  min: string | null,
  max: string,
) {
  if (!value) return;
  if (value > max)
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: [path],
      message: t("finance.customerAdvances.validation.dateFuture"),
    });
  else if (min && value < min)
    ctx.addIssue({
      code: z.ZodIssueCode.custom,
      path: [path],
      message: t("finance.customerAdvances.validation.dateBefore", { date: formatDate(min) }),
    });
}

const dateField = (t: T) =>
  z.string().min(1, t("finance.customerAdvances.validation.dateRequired"));

const moneyTarget = (t: T) => ({
  paymentMethodId: z.string().min(1, t("finance.customerAdvances.validation.methodRequired")),
  /** "bank:<id>" o "cash:<id>"; el medio elegido determina cuál se ofrece. */
  destination: z.string().min(1, t("finance.customerAdvances.validation.destinationRequired")),
});

export function buildRegisterCustomerAdvanceSchema(
  t: T,
  today: string,
  requiresReference: boolean,
) {
  return z
    .object({
      customerId: z.string().min(1, t("finance.customerAdvances.validation.customerRequired")),
      amount: amountField(t),
      advanceDate: dateField(t),
      ...moneyTarget(t),
      reference: optionalText(t, REFERENCE_MAX),
      reason: optionalText(t, REASON_MAX),
    })
    .superRefine((data, ctx) => {
      checkDate(ctx, t, "advanceDate", data.advanceDate, null, today);
      if (requiresReference && !data.reference)
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["reference"],
          message: t("finance.customerAdvances.validation.referenceRequired"),
        });
    });
}

export type RegisterCustomerAdvanceFormValues = z.infer<
  ReturnType<typeof buildRegisterCustomerAdvanceSchema>
>;

export function buildRefundCustomerAdvanceSchema(
  t: T,
  options: { today: string; advanceDate: string; available: number; decimals: number },
) {
  return z
    .object({
      amount: amountField(t).max(
        options.available,
        t("finance.customerAdvances.validation.amountOverAvailable", {
          amount: formatMoney(options.available, options.decimals),
        }),
      ),
      effectiveDate: dateField(t),
      ...moneyTarget(t),
      reference: requiredText(
        t,
        "finance.customerAdvances.validation.referenceRequired",
        REFERENCE_MAX,
      ),
      reason: requiredText(t, "finance.customerAdvances.validation.reasonRequired", REASON_MAX),
    })
    .superRefine((data, ctx) =>
      checkDate(ctx, t, "effectiveDate", data.effectiveDate, options.advanceDate, options.today),
    );
}

export type RefundCustomerAdvanceFormValues = z.infer<
  ReturnType<typeof buildRefundCustomerAdvanceSchema>
>;

export interface ApplicationTarget {
  /** Saldo pendiente de la CxC. */
  balanceDue: number;
  /** Emisión de la factura; null en un saldo inicial (su fecha de origen la valida el servidor). */
  originDate: string | null;
  installments: { id: string; pending: number }[];
}

export function buildApplyCustomerAdvanceSchema(
  t: T,
  options: {
    today: string;
    advanceDate: string | null;
    available: number;
    decimals: number;
    targets: Map<string, ApplicationTarget>;
  },
) {
  const money = (value: number) => formatMoney(value, options.decimals);
  return z
    .object({
      advanceId: z.string().min(1, t("finance.customerAdvances.validation.advanceRequired")),
      effectiveDate: dateField(t),
      lines: z
        .array(
          z.object({
            receivableId: z
              .string()
              .min(1, t("finance.customerAdvances.validation.receivableRequired")),
            installmentId: z.string(),
            amount: amountField(t),
          }),
        )
        .min(1, t("finance.customerAdvances.validation.linesRequired")),
    })
    .superRefine((data, ctx) => {
      const origins = data.lines
        .map((line) => options.targets.get(line.receivableId)?.originDate)
        .filter((date): date is string => !!date);
      const min = [options.advanceDate, ...origins]
        .filter((date): date is string => !!date)
        .sort()
        .pop();
      checkDate(ctx, t, "effectiveDate", data.effectiveDate, min ?? null, options.today);

      // Acumulado por CxC y por cuota: varias líneas no pueden exceder juntas el saldo.
      const used = new Map<string, number>();
      let total = 0;
      data.lines.forEach((line, index) => {
        const target = options.targets.get(line.receivableId);
        if (!target || !Number.isFinite(line.amount)) return;
        total = roundToDecimals(total + line.amount, 2);
        const check = (key: string, limit: number, message: string) => {
          const next = roundToDecimals((used.get(key) ?? 0) + line.amount, 2);
          used.set(key, next);
          if (next > limit)
            ctx.addIssue({
              code: z.ZodIssueCode.custom,
              path: ["lines", index, "amount"],
              message: t(message, { amount: money(limit) }),
            });
        };
        check(
          line.receivableId,
          target.balanceDue,
          "finance.customerAdvances.validation.amountOverReceivable",
        );
        const installment = target.installments.find((i) => i.id === line.installmentId);
        if (installment)
          check(
            installment.id,
            installment.pending,
            "finance.customerAdvances.validation.amountOverInstallment",
          );
      });
      if (total > options.available)
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["advanceId"],
          message: t("finance.customerAdvances.validation.amountOverAvailable", {
            amount: money(options.available),
          }),
        });
    });
}

export type ApplyCustomerAdvanceFormValues = z.infer<
  ReturnType<typeof buildApplyCustomerAdvanceSchema>
>;

export type CompensationKind = "reverseApplication" | "reverseRefund" | "reverseIncome" | "cancel";

export const requiresEvidenceDate = (kind: CompensationKind) =>
  kind === "reverseRefund" || kind === "reverseIncome";

export function buildCustomerAdvanceCompensationSchema(
  t: T,
  options: { kind: CompensationKind; today: string; originalDate: string },
) {
  return z
    .object({
      reason: requiredText(t, "finance.customerAdvances.validation.reasonRequired", REASON_MAX),
      reference: requiredText(
        t,
        "finance.customerAdvances.validation.referenceRequired",
        REFERENCE_MAX,
      ),
      evidenceDate: z.string(),
      moneyNotReceived: z.boolean(),
    })
    .superRefine((data, ctx) => {
      if (requiresEvidenceDate(options.kind)) {
        if (!data.evidenceDate)
          ctx.addIssue({
            code: z.ZodIssueCode.custom,
            path: ["evidenceDate"],
            message: t("finance.customerAdvances.validation.dateRequired"),
          });
        checkDate(ctx, t, "evidenceDate", data.evidenceDate, options.originalDate, options.today);
      }
      if (options.kind === "cancel" && !data.moneyNotReceived)
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["moneyNotReceived"],
          message: t("finance.customerAdvances.validation.declarationRequired"),
        });
    });
}

export type CustomerAdvanceCompensationFormValues = z.infer<
  ReturnType<typeof buildCustomerAdvanceCompensationSchema>
>;
