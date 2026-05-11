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
      .get<ApiResponse<ConfiguracionContableEmpresaDto | null>>('/api/contabilidad/configuracion')
      .then((r) => r.data.responseObject),

  upsertConfig: (data: ConfiguracionContableEmpresaDto) =>
    api
      .put<ApiResponse<ConfiguracionContableEmpresaDto>>('/api/contabilidad/configuracion', data)
      .then((r) => r.data.responseObject),

  listGastoMappings: () =>
    api
      .get<ApiResponse<ConfiguracionGastoCategoriaDto[]>>('/api/contabilidad/configuracion/gastos')
      .then((r) => r.data.responseObject ?? []),

  createGastoMapping: (data: CreateGastoCategoriaRequest) =>
    api
      .post<ApiResponse<ConfiguracionGastoCategoriaDto>>('/api/contabilidad/configuracion/gastos', data)
      .then((r) => r.data.responseObject),

  deleteGastoMapping: (id: string) =>
    api
      .delete<ApiResponse<unknown>>(`/api/contabilidad/configuracion/gastos/${id}`)
      .then((r) => r.data.responseObject),
};

