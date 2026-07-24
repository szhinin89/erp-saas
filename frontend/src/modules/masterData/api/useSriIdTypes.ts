import { useAsync } from '../../../hooks/useAsync';
import { apiGet } from '../../lib/apiEnvelope';

export type SriIdTypeOption = {
  code: string;
  name: string;
  digits?: number | null;
};

const FALLBACK: SriIdTypeOption[] = [
  { code: '04', name: 'RUC' },
  { code: '05', name: 'Cédula de ciudadanía' },
  { code: '06', name: 'Pasaporte' },
  { code: '07', name: 'Consumidor Final' },
  { code: '08', name: 'Identificación del exterior' },
  { code: '09', name: 'Placa' },
];

export function useSriIdTypes() {
  const state = useAsync(() =>
    apiGet<SriIdTypeOption[]>('/api/v1/catalog/sri-id-types').catch(() => FALLBACK)
  );
  return { options: state.data ?? FALLBACK, loading: state.loading };
}

export function useSriIdTypesByUsage(usage: string) {
  const state = useAsync(() =>
    apiGet<SriIdTypeOption[]>(`/api/v1/catalog/sri-id-types/by-usage/${usage}`).catch(() => FALLBACK)
  , true, [usage]);
  return { options: state.data ?? FALLBACK, loading: state.loading };
}

export function getSriIdTypeName(code: string, options: SriIdTypeOption[] = FALLBACK): string {
  return options.find((o) => o.code === code)?.name ?? code;
}
