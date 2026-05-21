import { test, expect } from '@playwright/test';
import {
  API_BASE,
  apiReachable,
  login,
  listMyCompanies,
  switchCompany,
} from './helpers/api';
import {
  createInvoiceDraft,
  emitInvoice,
  getInvoice,
  getStockForSale,
  listCustomers,
  getBranches,
  listInvoices,
  listProducts,
  listWarehouses,
  validateInvoice,
} from './helpers/sales';

test.describe('Enterprise sales company scope', () => {
  test.beforeEach(async ({ request }) => {
    const ok = await apiReachable(request);
    test.skip(!ok, `API no disponible en ${API_BASE}`);
  });

  test('sale in company A is not visible from company B', async ({ request }) => {
    test.setTimeout(90_000);

    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    test.skip(companies.length < 2, 'Se requieren al menos 2 empresas en el tenant demo');

    const companyA = companies[0]!.companyId;
    const companyB = companies[1]!.companyId;

    const tokenA = (await switchCompany(request, session.token, companyA)).token;

    const [customers, warehouses, branches, products] = await Promise.all([
      listCustomers(request, tokenA),
      listWarehouses(request, tokenA),
      getBranches(request, tokenA),
      listProducts(request, tokenA),
    ]);

    expect(customers.length).toBeGreaterThan(0);
    expect(warehouses.length).toBeGreaterThan(0);
    expect(branches.length).toBeGreaterThan(0);
    expect(products.length).toBeGreaterThan(0);

    const invoiceId = await createInvoiceDraft(request, tokenA, {
      customerId: customers[0]!.id,
      warehouseId: warehouses[0]!.id,
      branchId: branches[0]!.id,
      productId: products[0]!.id,
      quantity: 1,
      unitPrice: 10,
    });

    const tokenB = (await switchCompany(request, session.token, companyB)).token;
    const listB = await listInvoices(request, tokenB, 100);
    const idsB = listB.items.map((x) => x.id);
    expect(idsB).not.toContain(invoiceId);

    const detailB = await getInvoice(request, tokenB, invoiceId);
    expect(detailB.ok).toBeFalsy();
  });

  test('authorized sale reduces stock in company A', async ({ request }) => {
    test.setTimeout(120_000);

    const session = await login(request);
    const companies = await listMyCompanies(request, session.token);
    expect(companies.length).toBeGreaterThan(0);

    const companyA = companies[0]!.companyId;
    const tokenA = (await switchCompany(request, session.token, companyA)).token;

    const [customers, warehouses, branches, products] = await Promise.all([
      listCustomers(request, tokenA),
      listWarehouses(request, tokenA),
      getBranches(request, tokenA),
      listProducts(request, tokenA),
    ]);

    const productId = products[0]!.id;
    const warehouseId = warehouses[0]!.id;

    const stockBefore = await getStockForSale(request, tokenA, productId, warehouseId);
    expect(stockBefore).toBeGreaterThan(0);

    const invoiceId = await createInvoiceDraft(request, tokenA, {
      customerId: customers[0]!.id,
      warehouseId,
      branchId: branches[0]!.id,
      productId,
      quantity: 1,
      unitPrice: 10,
    });

    await validateInvoice(request, tokenA, invoiceId);
    await emitInvoice(request, tokenA, invoiceId);

    const stockAfter = await getStockForSale(request, tokenA, productId, warehouseId);
    expect(stockAfter).toBe(stockBefore - 1);
  });
});
