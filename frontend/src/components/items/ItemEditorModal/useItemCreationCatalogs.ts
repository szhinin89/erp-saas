import { useAsync } from "../../../hooks/useAsync";
import { useItemTypeOptions } from "../../../modules/items/hooks/useItemTypeOptions";
import {
  barcodeTypeService,
  brandService,
  sriLookupService,
  type BarcodeTypeLookup,
  type BrandDto,
  type SriIceRateLookup,
  type SriUomLookup,
  type SriVatRateLookup,
} from "../../../modules/items/catalog/api/catalogService";
import {
  categoryNodeService,
  type CategoryNodeDto,
} from "../../../modules/items/catalog/api/categoryNodeService";

// CONTRACT: única carga de catálogos de un Item — cada GET pasa por el service canónico del owner
// (items/catalog: brandService, categoryNodeService, sriLookupService, barcodeTypeService); aquí
// solo vive la normalización que la UI necesita (categorías hoja con ruta legible).
export type BrandOption = Pick<BrandDto, "id" | "name">;
export interface CategoryOption {
  id: string;
  name: string;
}
export type UomOption = SriUomLookup;
export type BarcodeTypeOption = BarcodeTypeLookup;

/**
 * Categorías hoja activas con su ruta completa "Línea > Categoría > Subcategoría" (desde
 * `path`, ids separados por "/"): la creación/edición de Items exige un nodo final sin hijos, y la
 * ruta evita ambigüedad entre hojas del mismo nombre en ramas distintas.
 */
export function toLeafCategoryOptions(nodes: CategoryNodeDto[]): CategoryOption[] {
  const nodesById = new Map(nodes.map((n) => [n.id, n]));
  const parentIds = new Set(
    nodes
      .filter((n) => n.isActive)
      .map((n) => n.parentId)
      .filter(Boolean),
  );
  const breadcrumb = (node: CategoryNodeDto) =>
    node.path
      .split("/")
      .filter(Boolean)
      .map((id) => nodesById.get(id)?.name)
      .filter(Boolean)
      .join(" > ") || node.name;
  return nodes
    .filter((n) => n.isActive && !parentIds.has(n.id))
    .map((n) => ({ id: n.id, name: breadcrumb(n) }));
}

/**
 * Catálogos necesarios para crear/editar un Item (tipo, marca, categoría hoja, UOM SRI, tipo de
 * código de barras, IVA e ICE). Única carga compartida por el formulario completo de Items
 * (`ItemFormTabs`), el editor individual (`ItemEditorForm`) y la creación masiva de Compras
 * (COMPRAS-METODO-ZH-01B). Un catálogo que falla se expone vacío (el formulario sigue usable).
 */
export function useItemCreationCatalogs() {
  const itemTypesState = useItemTypeOptions();

  const brandsState = useAsync(() =>
    brandService.list().catch(() => [] as BrandDto[]),
  );

  const categoriesState = useAsync(() =>
    categoryNodeService
      .getTree()
      .then((tree) => tree.nodes)
      .catch(() => [] as CategoryNodeDto[]),
  );
  const categoryOptions = toLeafCategoryOptions(categoriesState.data ?? []);

  const uomState = useAsync(() =>
    sriLookupService.uoms().catch(() => [] as SriUomLookup[]),
  );

  const barcodeTypeState = useAsync(() =>
    barcodeTypeService.list().catch(() => [] as BarcodeTypeLookup[]),
  );

  // Único catálogo de IVA — opciones del selector y porcentaje de referencia (misma fuente).
  const vatRatesState = useAsync(() =>
    sriLookupService.vatRates().catch(() => [] as SriVatRateLookup[]),
  );
  const vatRateOptions = vatRatesState.data ?? [];

  const iceRatesState = useAsync(() =>
    sriLookupService.iceRates().catch(() => [] as SriIceRateLookup[]),
  );

  return {
    itemTypesState,
    itemTypeOptions: itemTypesState.data ?? [],
    brandsState,
    brandOptions: (brandsState.data ?? []) as BrandOption[],
    categoriesState,
    categoryOptions,
    uomState,
    uomOptions: uomState.data ?? [],
    barcodeTypeState,
    barcodeTypeOptions: barcodeTypeState.data ?? [],
    vatRatesState,
    vatRateOptions,
    vatRateByCode: new Map(vatRateOptions.map((v) => [v.code, v])),
    iceRatesState,
    iceRateOptions: iceRatesState.data ?? [],
    ready:
      itemTypesState.data != null &&
      brandsState.data != null &&
      categoriesState.data != null &&
      uomState.data != null &&
      barcodeTypeState.data != null &&
      vatRatesState.data != null,
  };
}
