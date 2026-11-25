import { z } from "zod";

/**
 * Capa Zod de Empleados (contrato §6). El backend sigue siendo la autoridad: aquí solo se
 * anticipan obligatoriedad, longitudes y orden de fechas. Fechas de negocio "YYYY-MM-DD",
 * comparables como texto. Los mensajes son claves i18n (`employees.validation.*`).
 */
export type Translate = (key: string, fallback: string) => string;

const businessDate = (t: Translate) =>
  z
    .string()
    .regex(/^\d{4}-\d{2}-\d{2}$/, t("employees.validation.dateRequired", "Indique una fecha válida."));

export const employeeCreateSchema = (t: Translate) =>
  z.object({
    businessPartnerId: z
      .string()
      .min(1, t("employees.validation.partnerRequired", "Seleccione un socio de negocio.")),
    startDate: businessDate(t),
    jobPositionId: z.string(),
    professionId: z.string(),
    specialtyIds: z.array(z.string()),
  });
export type EmployeeCreateValues = z.infer<ReturnType<typeof employeeCreateSchema>>;

export const employeeProfileSchema = z.object({
  jobPositionId: z.string(),
  professionId: z.string(),
  specialtyIds: z.array(z.string()),
});
export type EmployeeProfileValues = z.infer<typeof employeeProfileSchema>;

export const REASON_MAX_LENGTH = 500;

/** `periodStart`: inicio del período abierto; el fin no puede ser anterior. */
export const terminateSchema = (t: Translate, periodStart: string) =>
  z.object({
    endDate: businessDate(t).refine(
      (v) => v >= periodStart,
      t("employees.validation.endBeforeStart", "La fecha de fin no puede ser anterior al inicio."),
    ),
    reason: z
      .string()
      .max(
        REASON_MAX_LENGTH,
        t("employees.validation.reasonTooLong", "El motivo no puede superar 500 caracteres."),
      ),
  });
export type TerminateValues = z.infer<ReturnType<typeof terminateSchema>>;

/** `lastEnd`: fin del último período; el reingreso es estrictamente posterior. */
export const rehireSchema = (t: Translate, lastEnd: string) =>
  z.object({
    startDate: businessDate(t).refine(
      (v) => v > lastEnd,
      t("employees.validation.rehireAfterEnd", "La reincorporación debe ser posterior a la última baja."),
    ),
  });
export type RehireValues = z.infer<ReturnType<typeof rehireSchema>>;

export const assignmentSchema = (t: Translate) =>
  z
    .object({
      branchId: z.string().min(1, t("employees.validation.branchRequired", "Seleccione una sucursal.")),
      startDate: businessDate(t),
      endDate: z.union([z.literal(""), businessDate(t)]),
    })
    .refine((v) => v.endDate === "" || v.endDate >= v.startDate, {
      path: ["endDate"],
      message: t("employees.validation.endBeforeStart", "La fecha de fin no puede ser anterior al inicio."),
    });
export type AssignmentValues = z.infer<ReturnType<typeof assignmentSchema>>;

export const closeAssignmentSchema = (t: Translate, start: string) =>
  z.object({
    endDate: businessDate(t).refine(
      (v) => v >= start,
      t("employees.validation.endBeforeStart", "La fecha de fin no puede ser anterior al inicio."),
    ),
  });
export type CloseAssignmentValues = z.infer<ReturnType<typeof closeAssignmentSchema>>;

export const CATALOG_NAME_MAX_LENGTH = 150;

export const catalogEntrySchema = (t: Translate) =>
  z.object({
    name: z
      .string()
      .trim()
      .min(1, t("employees.validation.nameRequired", "El nombre es obligatorio."))
      .max(
        CATALOG_NAME_MAX_LENGTH,
        t("employees.validation.nameTooLong", "El nombre no puede superar 150 caracteres."),
      ),
  });
export type CatalogEntryValues = z.infer<ReturnType<typeof catalogEntrySchema>>;
