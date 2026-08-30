import { describe, expect, it } from 'vitest';

import en from './locales/en.json';
import es from './locales/es.json';
import qu from './locales/qu.json';

/**
 * ZH-MENU-TAXONOMY-STANDARD-01: regresión puntual — un ítem de menú (Nivel 3) no debe repetir
 * literalmente el nombre de su grupo contenedor (Nivel 2) cuando ese grupo tiene más de una
 * pantalla real, porque el nombre deja de identificar la pantalla concreta (ejemplo del ticket:
 * "Proveedores > Compras > Compras"). No es una regla genérica — grupos con un único hijo real
 * (p. ej. "Cuentas por pagar > Cuentas por pagar") sí pueden repetir el nombre a propósito.
 */
describe('nav menu labels do not repeat their multi-screen parent group name', () => {
  const dictionaries: Record<string, Record<string, string>> = { es, en, qu };

  const ambiguousPairs = [
    {
      group: 'app.nav.item.purchases.operation',
      item: 'app.nav.item.purchases.invoices',
      description: 'Compras (grupo) vs. Facturas de compra (pantalla)',
    },
    {
      group: 'app.nav.item.accounting.reportsGroup',
      item: 'app.nav.item.accounting.reports',
      description: 'Reportes (grupo) vs. Reportes contables (pantalla)',
    },
  ];

  for (const [locale, dict] of Object.entries(dictionaries)) {
    for (const pair of ambiguousPairs) {
      it(`[${locale}] ${pair.description}`, () => {
        const groupLabel = dict[pair.group];
        const itemLabel = dict[pair.item];

        expect(groupLabel, `missing key ${pair.group}`).toBeTruthy();
        expect(itemLabel, `missing key ${pair.item}`).toBeTruthy();
        expect(itemLabel).not.toBe(groupLabel);
      });
    }
  }
});
