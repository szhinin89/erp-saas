import { useEffect, useState } from 'react';

export type SriIdTypeOption = {
  code: string;
  name: string;
};

const CACHE: SriIdTypeOption[] = [];

export function useSriIdTypes() {
  const [options, setOptions] = useState<SriIdTypeOption[]>(CACHE.length ? [...CACHE] : []);
  const [loading, setLoading] = useState(CACHE.length === 0);

  useEffect(() => {
    if (CACHE.length) return;
    setLoading(true);
    fetch('/api/catalogs/sri/id-types')
      .then((r) => r.json())
      .then((data: SriIdTypeOption[]) => {
        CACHE.push(...data);
        setOptions([...data]);
      })
      .catch(() => {
        // fallback con los 6 tipos estándar SRI
        const fallback: SriIdTypeOption[] = [
          { code: '04', name: 'RUC' },
          { code: '05', name: 'Cédula de ciudadanía' },
          { code: '06', name: 'Pasaporte' },
          { code: '07', name: 'Consumidor Final' },
          { code: '08', name: 'Identificación del exterior' },
          { code: '09', name: 'Placa' },
        ];
        CACHE.push(...fallback);
        setOptions(fallback);
      })
      .finally(() => setLoading(false));
  }, []);

  return { options, loading };
}

/** Devuelve el nombre legible dado un código SRI de identificación. */
export function getSriIdTypeName(code: string, options: SriIdTypeOption[]): string {
  return options.find((o) => o.code === code)?.name ?? code;
}
