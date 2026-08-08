/**
 * electronicDocumentAccessFacade — superficie pública de acceso a
 * documentos electrónicos para consumidores externos (sales).
 *
 * Expone la lectura del XML ya generado y el registro/backfill real de un
 * documento electrónico — no es una facade de solo lectura, `register` es
 * una acción de negocio (POST) que crea el documento electrónico de un
 * documento de origen ya autorizado. Los módulos externos deben importar
 * desde aquí, nunca directamente de
 * electronicDocuments/monitor/api/electronicDocumentsMonitorService.
 */

import { electronicDocumentsMonitorService } from "../monitor/api/electronicDocumentsMonitorService";
import type { ElectronicDocumentXmlVariant } from "../monitor/api/electronicDocumentsMonitorService";

export type { ElectronicDocumentXmlVariant };

export const electronicDocumentAccessFacade = {
  getXml: electronicDocumentsMonitorService.getXml,
  register: electronicDocumentsMonitorService.register,
};
