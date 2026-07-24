import { useAsync } from '../../../hooks/useAsync';
import { apiGet } from '../../lib/apiEnvelope';

export type PersonTypeOption = {
  code: number;
  name: string;
};

const FALLBACK: PersonTypeOption[] = [
  { code: 1, name: 'Natural (persona física)' },
  { code: 2, name: 'Jurídica (empresa/sociedad)' },
  { code: 3, name: 'Gubernamental' },
  { code: 4, name: 'Organización / ONG' },
];

export function usePersonTypes() {
  const state = useAsync(() =>
    apiGet<PersonTypeOption[]>('/api/v1/catalog/person-types').catch(() => FALLBACK)
  );
  return { options: state.data ?? FALLBACK, loading: state.loading };
}
