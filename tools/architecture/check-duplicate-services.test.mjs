import { test } from 'node:test';
import assert from 'node:assert/strict';
import { findDuplicateRegistrations } from './check-duplicate-services.mjs';

/**
 * ZH-ARCH-DUPLICATE-SERVICES-SCANNER-01 — el checker cuenta por nombre simple de interfaz,
 * tanto si el registro usa el nombre corto como el namespace completo.
 */
const NONE = new Set();

test('detecta duplicado con nombre simple', () => {
  const source = `
    services.AddScoped<IFoo, FooA>();
    services.AddScoped<IFoo, FooB>();
  `;
  assert.deepEqual(findDuplicateRegistrations(source, NONE), [{ iface: 'IFoo', count: 2 }]);
});

test('detecta duplicado con namespace completo (multilínea) y lo cuenta junto al simple', () => {
  const source = `
    services.AddScoped<
        ERP.Application.Modules.Foo.IFoo,
        ERP.Application.Modules.Foo.FooA
    >();
    services.AddSingleton<ERP.Application.Modules.Foo.IFoo>(sp => new FooB());
    services.AddTransient<IFoo, FooC>();
  `;
  assert.deepEqual(findDuplicateRegistrations(source, NONE), [{ iface: 'IFoo', count: 3 }]);
});

test('registro único no es violación', () => {
  const source = `
    services.AddScoped<ERP.Application.IFoo, ERP.Application.Foo>();
    services.AddScoped<IBar, Bar>();
  `;
  assert.deepEqual(findDuplicateRegistrations(source, NONE), []);
});

test('multi-registro en allowlist (por nombre simple) no falla, aunque use namespace completo', () => {
  const source = `
    services.AddScoped<ERP.Application.Modules.Ride.Parsers.IRideXmlParser, InvoiceRideXmlParser>();
    services.AddScoped<ERP.Application.Modules.Ride.Parsers.IRideXmlParser, CreditNoteRideXmlParser>();
  `;
  assert.deepEqual(findDuplicateRegistrations(source, new Set(['IRideXmlParser'])), []);
});

test('misma interfaz registrada simple + FQ se cuenta como duplicado', () => {
  const source = `
    services.AddScoped<ERP.Application.Common.IFoo, FooA>();
    services.AddScoped<IFoo, FooB>();
  `;
  assert.deepEqual(findDuplicateRegistrations(source, NONE), [{ iface: 'IFoo', count: 2 }]);
});

test('dos namespaces distintos con el mismo nombre simple, 1 registro cada uno → no es duplicado', () => {
  const source = `
    services.AddScoped<ERP.Application.Common.Services.ISriTaxResolver>(sp => sp.GetRequiredService<SriTaxResolver>());
    services.AddScoped<ERP.Application.Modules.Purchases.Services.ISriTaxResolver>(sp => sp.GetRequiredService<SriTaxResolver>());
  `;
  assert.deepEqual(findDuplicateRegistrations(source, NONE), []);
});

test('dos namespaces distintos: cada FQ se cuenta por separado', () => {
  const source = `
    services.AddScoped<A.IFoo, Foo1>();
    services.AddScoped<A.IFoo, Foo2>();
    services.AddScoped<B.IFoo, Foo3>();
  `;
  assert.deepEqual(findDuplicateRegistrations(source, NONE), [{ iface: 'A.IFoo', count: 2 }]);
});

test('dos namespaces distintos + registro simple → falla cerrado por ambigüedad (aun en allowlist)', () => {
  const source = `
    services.AddScoped<A.IFoo, Foo1>();
    services.AddScoped<B.IFoo, Foo2>();
    services.AddScoped<IFoo, Foo3>();
  `;
  const expected = [{ iface: 'IFoo', count: 1, ambiguous: ['A.IFoo', 'B.IFoo'] }];
  assert.deepEqual(findDuplicateRegistrations(source, NONE), expected);
  assert.deepEqual(findDuplicateRegistrations(source, new Set(['IFoo'])), expected);
});

test('duplicado real fuera de allowlist sí falla aunque otra interfaz esté permitida', () => {
  const source = `
    services.AddScoped<IRideTemplate, A>();
    services.AddScoped<IRideTemplate, B>();
    services.AddScoped<ERP.Application.IFoo, Foo>();
    services.AddScoped<ERP.Application.IFoo, Foo>();
  `;
  assert.deepEqual(findDuplicateRegistrations(source, new Set(['IRideTemplate'])), [
    { iface: 'IFoo', count: 2 },
  ]);
});
