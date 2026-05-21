import { useMemo, useState } from 'react';
import { catalogService, type CatalogItem } from '../../catalog/api/catalogService';
import { useAsync } from '../../../hooks/useAsync';
import { formatApiError } from '../../lib/formatApiError';
import { productService, type CreateProductRequest, type UpdateProductRequest } from '../api/productService';
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

  const [updateError, setUpdateError] = useState<string | null>(null);
  const [updating, setUpdating] = useState(false);

  const [toggleError, setToggleError] = useState<string | null>(null);
  const [toggling, setToggling] = useState(false);

  const toggleProductStatus = async (id: string, enable: boolean): Promise<Product | null> => {
    setToggleError(null);
    setToggling(true);
    try {
      const updated = enable
        ? await productService.enable(id)
        : await productService.disable(id);
      productsState.refetch();
      return updated;
    } catch (error) {
      setToggleError(formatApiError(error));
      return null;
    } finally {
      setToggling(false);
    }
  };

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

  const updateProduct = async (payload: UpdateProductRequest): Promise<Product | null> => {
    setUpdateError(null);
    setUpdating(true);
    try {
      const updated = await productService.update(payload);
      productsState.refetch();
      return updated;
    } catch (error) {
      setUpdateError(formatApiError(error));
      return null;
    } finally {
      setUpdating(false);
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
    updating,
    updateError,
    updateProduct,
    toggling,
    toggleError,
    toggleProductStatus,
  };
}
