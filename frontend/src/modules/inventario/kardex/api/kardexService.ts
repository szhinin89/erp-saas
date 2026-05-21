import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';

export interface KardexFilter {
  productId: string;
  warehouseId: string;
  fechaInicio?: string;
  fechaFin?: string;
}

export interface KardexProductInfo {
  id: string;
  name: string;
  codigo: string;
}

export interface KardexWarehouseInfo {
  id: string;
  name: string;
}

export interface KardexMovementRow {
  fecha: string;
  movementType: string;
  referencia: string | null;
  inboundQuantity: number;
  inboundValue: number;
  outboundQuantity: number;
  outboundValue: number;
  balanceQuantity: number;
  balanceValue: number;
  averageUnitCost: number;
}

export interface KardexSummary {
  openingQuantity: number;
  openingValue: number;
  totalInboundQuantity: number;
  totalInboundValue: number;
  totalOutboundQuantity: number;
  totalOutboundValue: number;
  closingQuantity: number;
  closingValue: number;
  finalAverageCost: number;
}

export interface KardexResponse {
  producto: KardexProductInfo;
  warehouse: KardexWarehouseInfo;
  rows: KardexMovementRow[];
  resumen: KardexSummary;
}

interface AsyncAcceptedBody {
  jobId: string;
  estado?: string;
  mensaje?: string;
}

interface AsyncResultBody {
  data: KardexResponse;
  message?: string;
}

function buildQuery(filter: KardexFilter): URLSearchParams {
  const q = new URLSearchParams({
    productoId: filter.productId,
    bodegaId: filter.warehouseId,
  });
  if (filter.fechaInicio) q.set('fechaInicio', filter.fechaInicio);
  if (filter.fechaFin) q.set('fechaFin', filter.fechaFin);
  return q;
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function pollAsyncResult(jobId: string, onProgress?: (msg: string) => void): Promise<KardexResponse> {
  const maxAttempts = 24;

  for (let attempt = 0; attempt < maxAttempts; attempt += 1) {
    if (attempt > 0) await sleep(5000);

    const res = await api.get<AsyncResultBody | { mensaje?: string }>(
      `/api/inventory/kardex/resultado/${jobId}`,
      { validateStatus: (s) => s === 200 || s === 202 || s === 400 || s === 404 },
    );

    if (res.status === 200) {
      const body = res.data as AsyncResultBody;
      if (!body.data) throw new Error('El reporte no devolvió datos.');
      return body.data;
    }

    if (res.status === 202) {
      onProgress?.('Generando reporte… esto puede tardar unos segundos.');
      continue;
    }

    const errBody = res.data as { mensaje?: string };
    throw new Error(errBody.mensaje ?? 'No se pudo obtener el resultado del kardex.');
  }

  throw new Error('El reporte tardó demasiado. Intente un rango de fechas más corto.');
}

export const kardexService = {
  async getKardex(filter: KardexFilter, onProgress?: (msg: string) => void): Promise<KardexResponse> {
    const q = buildQuery(filter);
    const res = await api.get<ApiResponse<KardexResponse> | AsyncAcceptedBody>(
      `/api/inventory/kardex?${q.toString()}`,
      { validateStatus: (s) => s === 200 || s === 202 || s === 400 },
    );

    if (res.status === 202) {
      const body = res.data as AsyncAcceptedBody;
      if (!body.jobId) throw new Error('Respuesta asíncrona sin jobId.');
      onProgress?.(body.mensaje ?? 'Reporte en cola…');
      return pollAsyncResult(body.jobId, onProgress);
    }

    if (res.status === 400) {
      const err = res.data as { mensaje?: string; message?: string };
      throw new Error(err.mensaje ?? err.message ?? 'No se pudo consultar el kardex.');
    }

    const envelope = res.data as ApiResponse<KardexResponse>;
    if (!envelope.responseObject) throw new Error('Respuesta vacía del servidor.');
    return envelope.responseObject;
  },

  async exportExcel(filter: KardexFilter): Promise<Blob> {
    const q = buildQuery(filter);
    const res = await api.get<Blob>(`/api/inventory/kardex/exportar/excel?${q.toString()}`, {
      responseType: 'blob',
    });
    return res.data;
  },

  async exportPdf(filter: KardexFilter): Promise<Blob> {
    const q = buildQuery(filter);
    const res = await api.get<Blob>(`/api/inventory/kardex/exportar/pdf?${q.toString()}`, {
      responseType: 'blob',
    });
    return res.data;
  },
};
