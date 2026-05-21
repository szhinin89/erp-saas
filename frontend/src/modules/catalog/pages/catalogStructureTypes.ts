import type {
  CatalogItem,
  ProductCategoryListItem,
  ProductSubcategoryListItem,
} from '../api/catalogService';

export type CatalogStructureModalType =
  | { kind: 'create-line' }
  | { kind: 'create-category' }
  | { kind: 'create-subcategory' }
  | { kind: 'edit-line'; item: CatalogItem }
  | { kind: 'edit-category'; item: ProductCategoryListItem }
  | { kind: 'edit-subcategory'; item: ProductSubcategoryListItem };

export type CatalogStructureModalForm = {
  code: string;
  name: string;
  lineId: string;
  categoryId: string;
};
