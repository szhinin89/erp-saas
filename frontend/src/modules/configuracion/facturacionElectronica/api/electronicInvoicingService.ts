import { apiGet, apiPost, apiPut } from "../../../lib/apiEnvelope";

export type SriConfigurationDto = {
  companyId: string;
  hasCertificate: boolean;
  certFileName: string | null;
  certSizeBytes: number | null;
  certUploadedAtUtc: string | null;
  environment: number;
  emissionType: number;
  wsdlUrl: string;
};

export type UpsertSriConfigurationRequest = {
  certPassword: string;
  environment: number;
  emissionType: number;
  wsdlUrl: string;
};

export type SriConfigurationCheckDto = {
  code: string;
  passed: boolean;
  message: string;
};

export type SriCertificateInfoDto = {
  passwordCorrect: boolean;
  loaded: boolean;
  notAfterUtc: string | null;
  daysRemaining: number | null;
  subject: string | null;
  issuer: string | null;
  errorMessage: string | null;
};

export type SriConfigurationValidationDto = {
  isValid: boolean;
  checks: SriConfigurationCheckDto[];
  certificate: SriCertificateInfoDto | null;
};

export type SriCertificateUploadResultDto = {
  fileName: string;
  sizeBytes: number;
  uploadedAtUtc: string;
  inspection: SriCertificateInfoDto | null;
};

/**
 * Estado operativo oficial de facturación electrónica — contrato estable calculado
 * exclusivamente por el backend (`GetElectronicInvoicingStatusQueryHandler`). El frontend nunca
 * infiere `status` combinando los demás campos; solo lo renderiza (ver
 * `electronicInvoicingStatusRegistry.ts`). Los campos restantes son detalle/diagnóstico.
 */
export type ElectronicInvoicingStatus =
  | "Ready"
  | "Testing"
  | "Incomplete"
  | "NotConfigured"
  | "CertificateExpired"
  | "CertificateExpiring"
  | "SriUnavailable"
  | "Disabled"
  | "Error";

export type SriAvailability = "Unknown" | "Available" | "Unavailable";

/** Estado liviano para UI transversal (banner de ambiente) — sin datos sensibles. */
export type ElectronicInvoicingStatusDto = {
  status: ElectronicInvoicingStatus;
  configured: boolean;
  environment: "Production" | "Test" | null;
  environmentName: string | null;
  emissionType: string | null;
  certificateInstalled: boolean;
  certificateValid: boolean;
  certificateExpiresAt: string | null;
  certificateDaysRemaining: number | null;
  sriAvailability: SriAvailability;
  canIssue: boolean;
};

export const electronicInvoicingService = {
  getSriConfiguration: () =>
    apiGet<SriConfigurationDto | null>(
      "/api/v1/electronic-invoicing/sri-configuration",
    ),

  upsertSriConfiguration: (body: UpsertSriConfigurationRequest) =>
    apiPut<SriConfigurationDto>(
      "/api/v1/electronic-invoicing/sri-configuration",
      body,
    ),

  /** Solo diagnostica (certificado, contraseña, vigencia, conectividad SRI) — nunca guarda cambios. */
  validateSriConfiguration: () =>
    apiGet<SriConfigurationValidationDto>(
      "/api/v1/electronic-invoicing/sri-configuration/validate",
    ),

  uploadCertificate: (file: File, onProgress?: (percent: number) => void) => {
    const formData = new FormData();
    formData.append("file", file);
    return apiPost<SriCertificateUploadResultDto>(
      "/api/v1/electronic-invoicing/sri-configuration/certificate",
      formData,
      {
        headers: { "Content-Type": "multipart/form-data" },
        onUploadProgress: (event) => {
          if (!onProgress || !event.total) return;
          onProgress(Math.round((event.loaded / event.total) * 100));
        },
      },
    );
  },

  /** Prueba una contraseña contra el certificado ya subido sin persistirla. */
  inspectCertificate: (password: string) =>
    apiPost<SriCertificateInfoDto>(
      "/api/v1/electronic-invoicing/sri-configuration/certificate/inspect",
      { password },
    ),

  /** Estado liviano para UI transversal (banner de ambiente) — cualquier usuario autenticado. */
  getStatus: () =>
    apiGet<ElectronicInvoicingStatusDto>("/api/v1/electronic-invoicing/status"),
};
