import type { ConfigDeleteInput, ConfigEntry, ConfigUpsertInput } from './types';

export const configService = {

  /** Config por tenant desde Platform: deshabilitado en ERP Core independiente. */
  async loadTenantConfig(_tenantId: string): Promise<ConfigEntry[]> {
    return [];
  },

  async upsertConfig(_tenantId: string, _input: ConfigUpsertInput): Promise<ConfigEntry> {
    throw new Error('La configuración Platform está deshabilitada en ERP Core.');
  },

  async deleteConfig(_tenantId: string, _input: ConfigDeleteInput): Promise<void> {
    throw new Error('La configuración Platform está deshabilitada en ERP Core.');
  },
};
