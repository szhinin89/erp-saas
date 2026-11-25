import { apiGet, apiPatch, apiPost, apiPut } from "../../lib/apiEnvelope";
import { formatApiRequestError, readApiErrorCode } from "../../lib/apiError";
import type {
  CreateEmployeeBody,
  EmployeeAssignment,
  EmployeeDto,
  EmployeeListItem,
  EmployeeListParams,
  PagedResult,
  UpdateEmployeeBody,
} from "../types/employee.types";

const BASE = "/api/v1/employees";
const one = (id: string) => `${BASE}/${encodeURIComponent(id)}`;

export const employeeService = {
  list: (params: EmployeeListParams, signal?: AbortSignal) =>
    apiGet<PagedResult<EmployeeListItem>>(BASE, { params, signal }),
  getById: (id: string) => apiGet<EmployeeDto>(one(id)),
  create: (body: CreateEmployeeBody) => apiPost<EmployeeDto>(BASE, body),
  update: (id: string, body: UpdateEmployeeBody) =>
    apiPatch<EmployeeDto>(one(id), body),
  setSpecialties: (id: string, rowVersion: number, specialtyIds: string[]) =>
    apiPut<EmployeeDto>(`${one(id)}/specialties`, { rowVersion, specialtyIds }),
  terminate: (id: string, body: { rowVersion: number; endDate: string; reason?: string }) =>
    apiPost<EmployeeDto>(`${one(id)}/terminate`, body),
  rehire: (id: string, body: { rowVersion: number; startDate: string }) =>
    apiPost<EmployeeDto>(`${one(id)}/rehire`, body),
  createAssignment: (id: string, body: { branchId: string; startDate: string; endDate?: string }) =>
    apiPost<EmployeeAssignment>(`${one(id)}/branch-assignments`, body),
  /** `rowVersion` de la asignación, no de la ficha. */
  closeAssignment: (id: string, assignmentId: string, body: { rowVersion: number; endDate: string }) =>
    apiPost<EmployeeAssignment>(
      `${one(id)}/branch-assignments/${encodeURIComponent(assignmentId)}/close`,
      body,
    ),
  linkUser: (id: string, body: { identityUserId: string; rowVersion: number }) =>
    apiPost<EmployeeDto>(`${one(id)}/link-user`, body),
  unlinkUser: (id: string, rowVersion: number) =>
    apiPost<EmployeeDto>(`${one(id)}/unlink-user`, { rowVersion }),
};

/** Otro cambio ganó: hay que releer y que el usuario repita la operación (nunca reintento). */
export const isConcurrencyConflict = (err: unknown): boolean =>
  readApiErrorCode(err) === "CONCURRENCY_CONFLICT";

/** Mensaje oficial del servidor (catálogo de mensajes) o el genérico indicado. */
export const employeeErrorMessage = (err: unknown, generic: string): string =>
  formatApiRequestError(err, { generic });
