import { apiDelete, apiGet, apiPost, apiPut } from '../../lib/apiEnvelope';

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
  getConfig: () => apiGet<ConfiguracionContableEmpresaDto | null>('/api/finance/config'),

  upsertConfig: (data: ConfiguracionContableEmpresaDto) =>
    apiPut<ConfiguracionContableEmpresaDto>('/api/finance/config', data),

  listGastoMappings: () =>
    apiGet<ConfiguracionGastoCategoriaDto[]>('/api/finance/config/gastos').then((rows) => rows ?? []),

  createGastoMapping: (data: CreateGastoCategoriaRequest) =>
    apiPost<ConfiguracionGastoCategoriaDto>('/api/finance/config/gastos', data),

  deleteGastoMapping: (id: string) => apiDelete<unknown>(`/api/finance/config/gastos/${id}`),
};
