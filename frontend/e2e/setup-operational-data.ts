import { request, type APIRequestContext } from "@playwright/test";
import {
  API_BASE,
  companyHeaders,
  login,
  listMyCompanies,
  switchCompany,
} from "./helpers/api";

type CatalogCode = {
  id: string;
  code: string;
  name?: string;
};

const operationalItems = [
  { sku: "E2E-SALE-ITEM-001", name: "Producto E2E Venta A" },
  { sku: "E2E-SALE-ITEM-002", name: "Producto E2E Venta B" },
] as const;

export async function setupOperationalData() {
  const api = await request.newContext();
  try {
    const session = await login(api);
    const companies = await listMyCompanies(api, session.token);
    if (companies.length < 2) {
      throw new Error("E2E setup requires two active companies.");
    }

    for (const [index, company] of companies.slice(0, 2).entries()) {
      const auth = await switchCompany(api, session.token, company.companyId);
      await ensureCompanyOperationalData(
        api,
        auth.token,
        operationalItems[index]!,
        index === 0,
      );
    }
  } finally {
    await api.dispose();
  }
}

async function ensureCompanyOperationalData(
  api: APIRequestContext,
  token: string,
  itemDefinition: (typeof operationalItems)[number],
  requiresOpenCashSession: boolean,
) {
  const companyContextHeaders = companyHeaders(token);
  const branchesResponse = await api.get(`${API_BASE}/api/v1/settings/branches`, {
    headers: companyContextHeaders,
  });
  if (!branchesResponse.ok()) {
    throw new Error(`branches failed: ${branchesResponse.status()} ${await branchesResponse.text()}`);
  }
  const branchesData = (await branchesResponse.json()).data;
  const branch = (branchesData?.items ?? branchesData)?.[0];
  if (!branch) throw new Error("No active E2E branch.");

  const switchBranchResponse = await api.post(`${API_BASE}/api/v1/session/switch-branch`, {
    headers: companyContextHeaders,
    data: { branchId: branch.id },
  });
  if (!switchBranchResponse.ok()) {
    throw new Error(`switch branch failed: ${switchBranchResponse.status()} ${await switchBranchResponse.text()}`);
  }
  const headers = { ...companyContextHeaders, "X-Branch-Id": branch.id };

  if (requiresOpenCashSession) await ensureOpenCashSession(api, headers);
  const item = await ensureItem(api, headers, itemDefinition);
  await ensureStock(api, headers, item, itemDefinition.name);
}

async function ensureOpenCashSession(api: APIRequestContext, headers: Record<string, string>) {
  const currentCashSession = await api.get(`${API_BASE}/api/v1/cash-sessions/my`, { headers });
  if (!currentCashSession.ok()) {
    throw new Error(`cash session lookup failed: ${currentCashSession.status()} ${await currentCashSession.text()}`);
  }
  if ((await currentCashSession.json()).data) return;

  const cashRegisters = await api.get(`${API_BASE}/api/v1/cash-registers?activeOnly=true`, {
    headers,
  });
  if (!cashRegisters.ok()) {
    throw new Error(`cash registers failed: ${cashRegisters.status()} ${await cashRegisters.text()}`);
  }
  const cashRegister = (await cashRegisters.json()).data?.[0];
  if (!cashRegister) throw new Error("No active E2E cash register.");
  const opened = await api.post(`${API_BASE}/api/v1/cash-sessions/open`, {
    headers,
    data: { cashRegisterId: cashRegister.id, openingAmount: 0, notes: "E2E cash session" },
  });
  if (!opened.ok()) {
    throw new Error(`cash session open failed: ${opened.status()} ${await opened.text()}`);
  }
}

async function ensureItem(
  api: APIRequestContext,
  headers: Record<string, string>,
  itemDefinition: (typeof operationalItems)[number],
) {
  const found = await api.get(
    `${API_BASE}/api/v1/items?sku=${itemDefinition.sku}&pageNumber=1&pageSize=20`,
    { headers },
  );
  if (!found.ok()) {
    throw new Error(`item lookup failed: ${found.status()} ${await found.text()}`);
  }
  const existing = (await found.json()).data?.items?.[0];
  if (existing) return existing;

  const [types, uoms, barcodes, brands, vat, categories] = await Promise.all([
    api.get(`${API_BASE}/api/v1/item-types?onlyActive=true`, { headers }),
    api.get(`${API_BASE}/api/v1/catalog/sri-uom`, { headers }),
    api.get(`${API_BASE}/api/v1/catalog/barcode-types`, { headers }),
    api.get(`${API_BASE}/api/v1/catalog/brands?isActive=true`, { headers }),
    api.get(`${API_BASE}/api/v1/catalog/sri-vat-rates`, { headers }),
    api.get(`${API_BASE}/api/v1/catalog/category-nodes`, { headers }),
  ]);
  for (const response of [types, uoms, barcodes, brands, vat, categories]) {
    if (!response.ok()) throw new Error(`catalog failed: ${response.status()} ${await response.text()}`);
  }

  const type = (await types.json()).data.find((x: CatalogCode) => x.code === "Physical");
  const uom = (await uoms.json()).data[0];
  const barcode = (await barcodes.json()).data[0];
  const brand = (await brands.json()).data.find((x: CatalogCode) => x.code === "NO_APLICA");
  const rate = (await vat.json()).data[0];
  const categoryTree = (await categories.json()).data;
  const category = (categoryTree.nodes ?? []).find(
    (x: CatalogCode) => x.code === "NO_APLICA" || x.name === "No aplica",
  );
  if (!type || !uom || !barcode || !brand || !rate || !category) {
    throw new Error("Required E2E item catalog value is missing.");
  }

  const created = await api.post(`${API_BASE}/api/v1/items`, {
    headers,
    data: {
      sku: itemDefinition.sku,
      shortName: itemDefinition.name,
      description: itemDefinition.name,
      itemTypeId: type.id,
      defaultUomCode: uom.code,
      categoryNodeId: category.id,
      brandId: brand.id,
      barcodes: [{ code: itemDefinition.sku, barcodeType: barcode.code, isPrimary: true }],
      saleVatCode: rate.code,
      exciseTaxCode: null,
      baseSalePrice: 10,
      isForSale: true,
      tracksStock: true,
    },
  });
  if (!created.ok()) {
    throw new Error(`item create failed: ${created.status()} ${await created.text()}`);
  }
  return (await created.json()).data;
}

async function ensureStock(
  api: APIRequestContext,
  headers: Record<string, string>,
  item: { id: string },
  itemName: string,
) {
  const warehousesResponse = await api.get(`${API_BASE}/api/v1/inventory/warehouses`, {
    headers,
  });
  if (!warehousesResponse.ok()) {
    throw new Error(`warehouses failed: ${warehousesResponse.status()} ${await warehousesResponse.text()}`);
  }
  const warehouseData = (await warehousesResponse.json()).data;
  const warehouse = (warehouseData?.items ?? warehouseData)?.[0];
  if (!warehouse) throw new Error("No active E2E warehouse.");

  const stockResponse = await api.get(
    `${API_BASE}/api/v1/inventory/stock?itemId=${item.id}&warehouseId=${warehouse.id}`,
    { headers },
  );
  if (!stockResponse.ok()) {
    throw new Error(`stock lookup failed: ${stockResponse.status()} ${await stockResponse.text()}`);
  }
  const stocks = (await stockResponse.json()).data ?? [];
  if (stocks[0]?.availableQuantity > 0) return;

  const reasonId = await ensureIngresoAdjustmentReason(api, headers);

  const draft = await api.post(`${API_BASE}/api/v1/inventory/stock/adjustments`, {
    headers,
    data: {
      warehouseId: warehouse.id,
      warehouseName: warehouse.name,
      movementType: "Ingreso",
      reasonId,
      notes: `E2E-STOCK-INITIAL-${item.id}`,
      lines: [
        {
          itemId: item.id,
          itemName,
          quantity: 10,
          unitCostBase: 1,
        },
      ],
    },
  });
  if (!draft.ok()) {
    throw new Error(`stock adjustment failed: ${draft.status()} ${await draft.text()}`);
  }
  const adjustment = (await draft.json()).data;
  const executed = await api.post(`${API_BASE}/api/v1/inventory/stock/adjustments/${adjustment.id}/execute`, {
    headers,
  });
  if (!executed.ok()) {
    throw new Error(`stock adjustment execute failed: ${executed.status()} ${await executed.text()}`);
  }
}

const E2E_ADJUSTMENT_REASON_CODE = "E2E-INGRESO";

async function ensureIngresoAdjustmentReason(
  api: APIRequestContext,
  headers: Record<string, string>,
): Promise<string> {
  const list = await api.get(
    `${API_BASE}/api/v1/inventory/adjustment-reasons?includeInactive=true`,
    { headers },
  );
  if (!list.ok()) {
    throw new Error(`adjustment reasons lookup failed: ${list.status()} ${await list.text()}`);
  }
  const existing = ((await list.json()).data ?? []).find(
    (r: { code?: string }) => r.code === E2E_ADJUSTMENT_REASON_CODE,
  );
  if (existing) return existing.id;

  const created = await api.post(`${API_BASE}/api/v1/inventory/adjustment-reasons`, {
    headers,
    data: {
      code: E2E_ADJUSTMENT_REASON_CODE,
      name: "Stock inicial E2E",
      allowedMovementType: "Ingreso",
      requiresNotes: false,
      sortOrder: 0,
    },
  });
  if (!created.ok()) {
    throw new Error(`adjustment reason create failed: ${created.status()} ${await created.text()}`);
  }
  return (await created.json()).data.id;
}
