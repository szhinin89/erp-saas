import { useAsync } from "../../../hooks/useAsync";
import { apiGet } from "../../../modules/lib/apiEnvelope";
import { useItemTypeOptions } from "../../../modules/items/hooks/useItemTypeOptions";
import {
  sriLookupService,
  type SriVatRateLookup,
} from "../../../modules/items/catalog/api/catalogService";

// CONTRACT: mismos catálogos ya consumidos por ItemFormTabs.tsx (formulario completo de Items) —
// no se crea un segundo contrato, solo se reutilizan los GET ya existentes.
export interface BrandOption {
  id: string;
  name: string;
}
interface CategoryNodeApi {
  id: string;
  name: string;
  path: string;
  parentId: string | null;
  isActive: boolean;
}
export interface CategoryOption {
  id: string;
  name: string;
}
export interface UomOption {
  code: string;
  name: string;
  abbrev: string | null;
}
export interface BarcodeTypeOption {
  code: string;
  name: string;
}

/**
 * Catálogos necesarios para crear un Item (tipo, marca, categoría hoja, UOM SRI, tipo de código de
 * barras e IVA). Única carga compartida por el editor individual (`ItemEditorForm`) y la creación
 * masiva de Compras (COMPRAS-METODO-ZH-01B) — ambos consumen exactamente los mismos endpoints.
 */
export function useItemCreationCatalogs() {
  const itemTypesState = useItemTypeOptions();

  const brandsState = useAsync(() =>
    apiGet<BrandOption[]>("/api/v1/catalog/brands").catch(
      () => [] as BrandOption[],
    ),
  );

  const categoriesState = useAsync(() =>
    apiGet<{ nodes: CategoryNodeApi[] }>(
      "/api/v1/catalog/category-nodes",
    ).catch(() => ({ nodes: [] })),
  );
  const allNodes = categoriesState.data?.nodes ?? [];
  const nodesById = new Map(allNodes.map((n) => [n.id, n]));
  const parentIds = new Set(
    allNodes
      .filter((n) => n.isActive)
      .map((n) => n.parentId)
      .filter(Boolean),
  );
  const breadcrumb = (node: CategoryNodeApi) =>
    node.path
      .split("/")
      .filter(Boolean)
      .map((id) => nodesById.get(id)?.name)
      .filter(Boolean)
      .join(" > ") || node.name;
  // Solo categorías hoja activas: la creación de Items exige un nodo final sin hijos.
  const categoryOptions: CategoryOption[] = allNodes
    .filter((n) => n.isActive && !parentIds.has(n.id))
    .map((n) => ({ id: n.id, name: breadcrumb(n) }));

  const uomState = useAsync(() =>
    apiGet<UomOption[]>("/api/v1/catalog/sri-uom").catch(
      () => [] as UomOption[],
    ),
  );

  const barcodeTypeState = useAsync(() =>
    apiGet<BarcodeTypeOption[]>("/api/v1/catalog/barcode-types").catch(
      () => [] as BarcodeTypeOption[],
    ),
  );

  // Único catálogo de IVA — opciones del selector y porcentaje de referencia (misma fuente).
  const vatRatesState = useAsync(() =>
    sriLookupService.vatRates().catch(() => [] as SriVatRateLookup[]),
  );
  const vatRateOptions = vatRatesState.data ?? [];

  return {
    itemTypesState,
    itemTypeOptions: itemTypesState.data ?? [],
    brandsState,
    brandOptions: brandsState.data ?? [],
    categoriesState,
    categoryOptions,
    uomState,
    uomOptions: uomState.data ?? [],
    barcodeTypeState,
    barcodeTypeOptions: barcodeTypeState.data ?? [],
    vatRatesState,
    vatRateOptions,
    vatRateByCode: new Map(vatRateOptions.map((v) => [v.code, v])),
    ready:
      itemTypesState.data != null &&
      brandsState.data != null &&
      categoriesState.data != null &&
      uomState.data != null &&
      barcodeTypeState.data != null &&
      vatRatesState.data != null,
  };
}
