import axios from "axios";
import type { SalesReceiptPrintPayloadDto } from "./salesService";

const DEFAULT_PRINT_AGENT_BASE_URL = "http://127.0.0.1:9817";
const DEFAULT_RECEIPT_ENDPOINT = "/print-jobs";
const PRINT_AGENT_TIMEOUT_MS = 3500;
const POLL_DELAY_MS = 450;
const POLL_ATTEMPTS = 8;

export const PRINT_AGENT_STORAGE_KEYS = {
  baseUrl: "zh.printAgent.baseUrl",
  apiKey: "zh.printAgent.apiKey",
  printerName: "zh.printAgent.printerName",
} as const;

export type PrintAgentErrorKind =
  | "not-configured"
  | "offline"
  | "unauthorized"
  | "printer-unavailable"
  | "job-failed"
  | "unknown";

export class PrintAgentError extends Error {
  public readonly kind: PrintAgentErrorKind;
  public readonly detail?: string;

  constructor(
    kind: PrintAgentErrorKind,
    message: string,
    detail?: string,
  ) {
    super(message);
    this.name = "PrintAgentError";
    this.kind = kind;
    this.detail = detail;
  }
}

export interface PrintAgentConfig {
  baseUrl: string;
  apiKey: string;
  printerName: string;
  receiptEndpoint: string;
}

export interface ReceiptDocument {
  merchantName: string;
  headerLines: string[];
  items: ReceiptLineItem[];
  totals: ReceiptTotalLine[];
  footerLines: string[];
  rawLines: string[];
}

export interface ReceiptLineItem {
  name: string;
  quantity: number;
  unitPrice: number;
  total: number;
}

export interface ReceiptTotalLine {
  label: string;
  amount: number;
}

export interface SubmitPrintJobRequest {
  jobId: string;
  printerName: string;
  copies: number;
  receipt: ReceiptDocument;
}

export type PrintJobStatus =
  | "Pending"
  | "Processing"
  | "Printed"
  | "Failed"
  | "Cancelled"
  | "NeedsReview";

export interface PrintJobResponse {
  jobId: string;
  printerName: string;
  status: PrintJobStatus | number;
  duplicate: boolean;
  attempts: number;
  lastError: string | null;
  createdAt: string;
  updatedAt: string;
  nextAttemptAt: string | null;
}

export function resolvePrintAgentConfig(): PrintAgentConfig {
  const baseUrl =
    readLocalStorage(PRINT_AGENT_STORAGE_KEYS.baseUrl) ??
    readEnv("VITE_PRINT_AGENT_BASE_URL") ??
    DEFAULT_PRINT_AGENT_BASE_URL;
  const apiKey =
    readLocalStorage(PRINT_AGENT_STORAGE_KEYS.apiKey) ??
    readEnv("VITE_PRINT_AGENT_API_KEY") ??
    "";
  const printerName =
    readLocalStorage(PRINT_AGENT_STORAGE_KEYS.printerName) ??
    readEnv("VITE_PRINT_AGENT_PRINTER_NAME") ??
    "";
  const receiptEndpoint =
    readEnv("VITE_PRINT_AGENT_RECEIPT_ENDPOINT") ?? DEFAULT_RECEIPT_ENDPOINT;

  return {
    baseUrl: trimTrailingSlash(baseUrl),
    apiKey,
    printerName,
    receiptEndpoint,
  };
}

export function buildReceiptPrintJobRequest(
  payload: SalesReceiptPrintPayloadDto,
  config: PrintAgentConfig = resolvePrintAgentConfig(),
): SubmitPrintJobRequest {
  return {
    jobId: `invoice-${payload.invoiceId}-receipt`,
    printerName: config.printerName,
    copies: 1,
    receipt: {
      merchantName: payload.tradeName ?? payload.companyName,
      headerLines: buildHeaderLines(payload),
      items: payload.lines.map((line) => ({
        name: line.sku ? `${line.sku} ${line.productName}` : line.productName,
        quantity: line.quantity,
        unitPrice: line.unitPrice,
        total: line.total,
      })),
      totals: [
        {
          label: "SUBTOTAL",
          amount: payload.totals.subtotalWithoutTaxes,
        },
        {
          label: "DESCUENTO",
          amount: payload.totals.discountTotal,
        },
        {
          label: "IVA",
          amount: payload.totals.vatTotal,
        },
        {
          label: "TOTAL",
          amount: payload.totals.total,
        },
      ],
      rawLines: buildRawLines(payload),
      footerLines: payload.footerMessage ? [payload.footerMessage] : [],
    },
  };
}

export async function submitReceiptPrintJob(
  request: SubmitPrintJobRequest,
  config: PrintAgentConfig = resolvePrintAgentConfig(),
): Promise<PrintJobResponse> {
  ensureConfigured(config);
  try {
    const submitted = await postReceiptJob(request, config);
    return await waitForPrintJobIfNeeded(submitted, config);
  } catch (err) {
    throw toPrintAgentError(err);
  }
}

export async function retryReceiptPrintJob(
  jobId: string,
  config: PrintAgentConfig = resolvePrintAgentConfig(),
): Promise<PrintJobResponse> {
  ensureConfigured(config);
  try {
    const response = await agent(config).post<PrintJobResponse>(
      `/print-jobs/${encodeURIComponent(jobId)}/retry`,
    );
    return await waitForPrintJobIfNeeded(response.data, config);
  } catch (err) {
    throw toPrintAgentError(err);
  }
}

export function printAgentUserMessage(err: unknown): string {
  if (err instanceof PrintAgentError) {
    return err.message;
  }
  return "Error de impresión; puede reintentar.";
}

function buildHeaderLines(payload: SalesReceiptPrintPayloadDto): string[] {
  return compact([
    payload.tradeName ? payload.companyName : null,
    `RUC ${payload.ruc}`,
    `Sucursal: ${payload.branchName}`,
    payload.establishmentCode || payload.emissionPointCode
      ? `Est/Pto: ${payload.establishmentCode ?? "---"}-${payload.emissionPointCode ?? "---"}`
      : null,
    payload.cashRegisterName ? `Caja: ${payload.cashRegisterName}` : null,
    `Factura: ${payload.invoiceNumber}`,
    `Fecha: ${payload.issuedAt}`,
    `Cliente: ${payload.customerName}`,
    `ID: ${payload.customerIdentification}`,
    payload.customerEmail ? `Email: ${payload.customerEmail}` : null,
  ]);
}

function buildRawLines(payload: SalesReceiptPrintPayloadDto): string[] {
  return compact([
    payload.isElectronic ? "Factura electronica" : "Factura fisica",
    payload.electronicStatus ? `Estado SRI: ${payload.electronicStatus}` : null,
    payload.accessKey ? `Clave: ${payload.accessKey}` : null,
    payload.authorizationNumber
      ? `Autorizacion: ${payload.authorizationNumber}`
      : null,
    payload.authorizationDate ? `Fecha autorizacion: ${payload.authorizationDate}` : null,
    ...payload.payments.flatMap((payment) =>
      compact([
        `Pago: ${payment.method} ${money(payment.amount)}`,
        payment.reference ? `Ref: ${payment.reference}` : null,
      ]),
    ),
    payload.cashReceived === null
      ? null
      : `Efectivo recibido: ${money(payload.cashReceived)}`,
    payload.cashChange === null ? null : `Vuelto: ${money(payload.cashChange)}`,
  ]);
}

async function postReceiptJob(
  request: SubmitPrintJobRequest,
  config: PrintAgentConfig,
): Promise<PrintJobResponse> {
  const response = await agent(config).post<PrintJobResponse>(
    config.receiptEndpoint,
    request,
  );
  return response.data;
}

async function waitForPrintJobIfNeeded(
  initial: PrintJobResponse,
  config: PrintAgentConfig,
): Promise<PrintJobResponse> {
  let current = initial;
  for (let attempt = 0; attempt < POLL_ATTEMPTS; attempt += 1) {
    const status = normalizeStatus(current.status);
    if (status === "Printed") return current;
    if (status === "Failed" || status === "Cancelled" || status === "NeedsReview") {
      throw new PrintAgentError(
        "job-failed",
        "Error de impresión; puede reintentar.",
        current.lastError ?? undefined,
      );
    }
    await delay(POLL_DELAY_MS);
    current = await getPrintJob(current.jobId, config);
  }
  return current;
}

async function getPrintJob(
  jobId: string,
  config: PrintAgentConfig,
): Promise<PrintJobResponse> {
  const response = await agent(config).get<PrintJobResponse>(
    `/print-jobs/${encodeURIComponent(jobId)}`,
  );
  return response.data;
}

function agent(config: PrintAgentConfig) {
  return axios.create({
    baseURL: config.baseUrl,
    timeout: PRINT_AGENT_TIMEOUT_MS,
    headers: {
      "Content-Type": "application/json",
      "X-ZH-PrintAgent-Key": config.apiKey,
    },
  });
}

function ensureConfigured(config: PrintAgentConfig): void {
  if (!config.apiKey.trim()) {
    throw new PrintAgentError(
      "not-configured",
      "API key del agente no configurada.",
    );
  }
  if (!config.printerName.trim()) {
    throw new PrintAgentError(
      "printer-unavailable",
      "Impresora no disponible.",
      "Configure VITE_PRINT_AGENT_PRINTER_NAME o zh.printAgent.printerName.",
    );
  }
}

function toPrintAgentError(err: unknown): PrintAgentError {
  if (err instanceof PrintAgentError) return err;

  if (axios.isAxiosError(err)) {
    if (!err.response) {
      return new PrintAgentError(
        "offline",
        "No se pudo conectar con el agente de impresión.",
        err.message,
      );
    }
    if (err.response.status === 401) {
      return new PrintAgentError(
        "unauthorized",
        "API key del agente inválida.",
      );
    }

    const detail = readAgentErrorDetail(err.response.data);
    if (
      err.response.status === 400 &&
      (detail.toLowerCase().includes("printer") ||
        detail.toLowerCase().includes("printername") ||
        detail.toLowerCase().includes("impresora"))
    ) {
      return new PrintAgentError("printer-unavailable", "Impresora no disponible.", detail);
    }
    return new PrintAgentError("unknown", "Error de impresión; puede reintentar.", detail);
  }

  return new PrintAgentError("unknown", "Error de impresión; puede reintentar.");
}

function normalizeStatus(status: PrintJobResponse["status"]): PrintJobStatus {
  if (typeof status === "string") return status as PrintJobStatus;
  const byNumber: Record<number, PrintJobStatus> = {
    0: "Pending",
    1: "Processing",
    2: "Printed",
    3: "Failed",
    4: "Cancelled",
    5: "NeedsReview",
  };
  return byNumber[status] ?? "Failed";
}

function readAgentErrorDetail(data: unknown): string {
  if (typeof data === "string") return data;
  if (!data || typeof data !== "object" || Array.isArray(data)) return "";
  const record = data as Record<string, unknown>;
  if (typeof record.error === "string") return record.error;
  if (typeof record.title === "string") return record.title;
  const errors = record.errors;
  if (errors && typeof errors === "object" && !Array.isArray(errors)) {
    return Object.values(errors as Record<string, unknown>)
      .flatMap((value) => (Array.isArray(value) ? value : [value]))
      .filter((value): value is string => typeof value === "string")
      .join(" ");
  }
  return "";
}

function readEnv(key: string): string | null {
  const value = (import.meta.env[key] as string | undefined)?.trim();
  return value ? value : null;
}

function readLocalStorage(key: string): string | null {
  try {
    const value = window.localStorage.getItem(key)?.trim();
    return value ? value : null;
  } catch {
    return null;
  }
}

function trimTrailingSlash(value: string): string {
  return value.replace(/\/+$/, "");
}

function compact(values: Array<string | null | undefined>): string[] {
  return values.filter((value): value is string => !!value?.trim());
}

function money(value: number): string {
  return value.toFixed(2);
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => window.setTimeout(resolve, ms));
}
