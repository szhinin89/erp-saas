import { useMemo, useState } from 'react';
import { catalogService, type CatalogItem } from '../../../services/catalogService';
import { useAsync } from '../../../hooks/useAsync';
import { formatApiError } from '../../lib/formatApiError';
import { productService, type CreateProductRequest } from '../api/productService';
import type { Product } from '../../../types/product';

export type ProductCatalogs = {
  lines: CatalogItem[];
  categories: CatalogItem[];
  subcategories: CatalogItem[];
  units: CatalogItem[];
  brands: CatalogItem[];
  productTypes: CatalogItem[];
  taxRates: CatalogItem[];
  tariffs: CatalogItem[];
};

export function useProducts() {
  const productsState = useAsync(productService.getAll);
  const catalogsState = useAsync<ProductCatalogs>(async () => {
    const [lines, categories, subcategories, units, brands, productTypes, taxRates, tariffs] = await Promise.all([
      catalogService.productLines({ activeStatus: 'all' }),
      catalogService.categories({ activeStatus: 'all' }),
      catalogService.subcategories({ activeStatus: 'all' }),
      catalogService.units(false),
      catalogService.brands(false),
      catalogService.productTypes(false),
      catalogService.taxRates(false),
      catalogService.tariffs(false),
    ]);

    return { lines, categories, subcategories, units, brands, productTypes, taxRates, tariffs };
  });

  const [createError, setCreateError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const createProduct = async (payload: CreateProductRequest): Promise<Product | null> => {
    setCreateError(null);
    setCreating(true);
    try {
      const created = await productService.create(payload);
      productsState.refetch();
      return created;
    } catch (error) {
      setCreateError(formatApiError(error));
      return null;
    } finally {
      setCreating(false);
    }
  };

  const recentProducts = useMemo(() => {
    const list = productsState.data ?? [];
    return [...list].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  }, [productsState.data]);

  return {
    products: productsState.data ?? [],
    productsLoading: productsState.loading,
    productsError: productsState.error,
    catalogs: catalogsState.data,
    catalogsLoading: catalogsState.loading,
    catalogsError: catalogsState.error,
    recentProducts,
    creating,
    createError,
    createProduct,
  };
}
