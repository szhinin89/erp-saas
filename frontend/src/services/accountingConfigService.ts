import { api } from '../modules/lib/api';
import type { ApiResponse } from '../types/api';

export interface ConfiguracionContableEmpresaDto {
  cuentaInventarioId?: string | null;
  cuentaCostoVentaId?: string | null;
  cuentaProveedoresId?: string | null;
  cuentaVentasId?: string | null;
  cuentaClientesId?: string | null;
  cuentaIvaComprasId?: string | null;
  cuentaIvaVentasId?: string | null;
  cuentaEfectivoId?: string | null;
  cuentaBancoId?: string | null;
}

export interface ConfiguracionGastoCategoriaDto {
  id: string;
  categoria: string;
  cuentaGastoId: string;
}

export interface CreateGastoCategoriaRequest {
  categoria: string;
  cuentaGastoId: string;
}

export const accountingConfigService = {
  getConfig: () =>
    api
      .get<ApiResponse<ConfiguracionContableEmpresaDto | null>>('/api/finance/config')
      .then((r) => r.data.responseObject),

  upsertConfig: (data: ConfiguracionContableEmpresaDto) =>
    api
      .put<ApiResponse<ConfiguracionContableEmpresaDto>>('/api/finance/config', data)
      .then((r) => r.data.responseObject),

  listGastoMappings: () =>
    api
      .get<ApiResponse<ConfiguracionGastoCategoriaDto[]>>('/api/finance/config/gastos')
      .then((r) => r.data.responseObject ?? []),

  createGastoMapping: (data: CreateGastoCategoriaRequest) =>
    api
      .post<ApiResponse<ConfiguracionGastoCategoriaDto>>('/api/finance/config/gastos', data)
      .then((r) => r.data.responseObject),

  deleteGastoMapping: (id: string) =>
    api
      .delete<ApiResponse<unknown>>(`/api/finance/config/gastos/${id}`)
      .then((r) => r.data.responseObject),
};

