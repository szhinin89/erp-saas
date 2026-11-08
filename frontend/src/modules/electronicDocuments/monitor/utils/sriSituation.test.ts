// @vitest-environment node
import { describe, expect, it } from "vitest";
import type { ElectronicDocumentDiagnosticDto } from "../../../../components/zh/electronicDocuments/electronicDocumentDiagnosticTypes";
import {
  incidenceVariant,
  isLongResponse,
  latestEvent,
  latestMessage,
  sriAuthorizationStatus,
  sriReceptionStatus,
  stateMeaningKey,
} from "./sriSituation";

/**
 * SRI-MONITOR-SHEET-01 — estado documental, recepción y autorización SRI se leen por separado y
 * solo desde datos reales (número de autorización, etapa del rechazo del backend, historial).
 */

function diag(
  currentState: string,
  o: Partial<ElectronicDocumentDiagnosticDto> = {},
  authorizationNumber: string | null = null,
): ElectronicDocumentDiagnosticDto {
  return {
    currentState,
    environment: "1",
    lastAttemptUtc: null,
    messages: [],
    timeline: [],
    technicalInfo: {
      accessKey: null,
      authorizationNumber,
      environment: "1",
      authorizationDate: null,
      retryCount: 0,
      lastAttemptUtc: null,
      correlationId: null,
    },
    xmlDraftAvailable: false,
    xmlSignedAvailable: false,
    xmlAuthorizedAvailable: false,
    requiresReconciliation: false,
    ...o,
  };
}

const rejected = (rejectionStage: string | null) =>
  diag("Rejected", {
    retransmission: {
      eligible: false,
      transmissionAttempt: 1,
      rejectionStage,
      rejectionCodes: [],
      blocks: [],
    },
  });

const ev = (action: string, occurredAtUtc: string) => ({
  action,
  fromState: null,
  toState: action,
  occurredAtUtc,
  userName: "Sistema",
  durationSinceLastMinutes: null,
});

describe("sriSituation — recepción y autorización", () => {
  it.each([
    ["Draft", "notSent", "none"],
    ["XmlGenerated", "notSent", "none"],
    ["Failed", "notSent", "none"],
    ["Discarded", "notSent", "none"],
    ["Signed", "unconfirmed", "pending"],
    ["Dispatching", "unconfirmed", "pending"],
    ["Sent", "unconfirmed", "pending"],
    ["Received", "received", "pending"],
    ["Authorized", "received", "authorized"],
    ["DeadLetter", "unconfirmed", "pending"],
  ])("%s → recepción %s, autorización %s", (state, reception, authorization) => {
    const d = diag(state);
    expect(sriReceptionStatus(d)).toBe(reception);
    expect(sriAuthorizationStatus(d)).toBe(authorization);
  });

  it("Rechazado distingue DEVUELTA (recepción) de NO AUTORIZADO (autorización) según el backend", () => {
    expect(sriReceptionStatus(rejected("Reception"))).toBe("returned");
    expect(sriReceptionStatus(rejected("Authorization"))).toBe("received");
    expect(sriReceptionStatus(rejected(null))).toBe("unconfirmed");
    for (const stage of ["Reception", "Authorization", null])
      expect(sriAuthorizationStatus(rejected(stage))).toBe("notAuthorized");
  });

  it("Reintentos agotados con una RECIBIDA registrada en el historial se muestra recibido", () => {
    const d = diag("DeadLetter", { timeline: [ev("Received", "2026-11-01T10:00:00Z")] });
    expect(sriReceptionStatus(d)).toBe("received");
    expect(sriAuthorizationStatus(d)).toBe("pending");
  });

  it("Anulado con número de autorización conserva recepción y autorización", () => {
    const d = diag("Cancelled", {}, "1".repeat(49));
    expect(sriReceptionStatus(d)).toBe("received");
    expect(sriAuthorizationStatus(d)).toBe("authorized");
  });

  it("significado e incidencia por estado", () => {
    expect(stateMeaningKey(rejected("Reception"))).toMatch(/RejectedReception$/);
    expect(stateMeaningKey(rejected("Authorization"))).toMatch(/RejectedAuthorization$/);
    expect(stateMeaningKey(rejected(null))).toMatch(/\.Rejected$/);
    expect(stateMeaningKey(diag("Signed"))).toMatch(/\.Signed$/);
    expect(incidenceVariant("Rejected")).toBe("error");
    expect(incidenceVariant("DeadLetter")).toBe("error");
    expect(incidenceVariant("Failed")).toBe("error");
    expect(incidenceVariant("Dispatching")).toBe("warning");
    for (const s of ["Draft", "Signed", "Sent", "Received", "Authorized", "Cancelled"])
      expect(incidenceVariant(s)).toBeNull();
  });
});

describe("sriSituation — historial y respuestas", () => {
  it("toma el evento y el mensaje más recientes sin asumir el orden", () => {
    const timeline = [
      ev("Sent", "2026-11-02T10:00:00Z"),
      ev("Sent", "2026-11-03T10:00:00Z"),
      ev("Sent", "2026-11-01T10:00:00Z"),
    ];
    expect(latestEvent(timeline, "Sent")?.occurredAtUtc).toBe("2026-11-03T10:00:00Z");
    expect(latestEvent(timeline, "Received")).toBeNull();
    const msg = (message: string, occurredAtUtc: string) => ({
      code: null,
      messageType: "ERROR",
      message,
      additionalInfo: null,
      occurredAtUtc,
    });
    expect(
      latestMessage([msg("b", "2026-11-03T00:00:00Z"), msg("a", "2026-11-01T00:00:00Z")])
        ?.message,
    ).toBe("b");
    expect(latestMessage([])).toBeNull();
    expect(isLongResponse(msg("x".repeat(241), "2026-11-01T00:00:00Z"))).toBe(true);
    expect(isLongResponse(msg("corto", "2026-11-01T00:00:00Z"))).toBe(false);
  });
});
