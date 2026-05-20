export type ConfigScope = 'global' | 'module' | 'feature';
export type ConfigDataType = 'string' | 'number' | 'boolean' | 'json';

export type ConfigEntry = {
  id: string;
  subscriberId: string;
  scope: ConfigScope;
  module: string | null;
  feature: string | null;
  key: string;
  value: string;
  dataType: string;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type ConfigUpsertInput = {
  scope: ConfigScope;
  key: string;
  value: string;
  dataType: ConfigDataType;
  module?: string | null;
  feature?: string | null;
};

export type ConfigDeleteInput = {
  scope: ConfigScope;
  key: string;
  module?: string | null;
  feature?: string | null;
};

export type ConfigResolvedValue<T = unknown> = {
  value: T;
  scope: ConfigScope;
  dataType: string;
};

export type ConfigLoadStatus = 'idle' | 'loading' | 'ready' | 'error';

