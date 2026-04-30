import { useMemo, useState } from 'react';
import { productService } from '../services/productService';
import { useAsync } from '../hooks/useAsync';
import {
  PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge,
} from '../components/PageShell';
import { CreateProductModal } from '../components/CreateProductModal';
import { useI18n } from '../i18n/i18n';
import { usePermissionsStore } from '../store/permissionsStore';
import { catalogService } from '../services/catalogService';
import { useAuthStore } from '../store/authStore';

export function ProductsPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const role = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const canView = isAdmin || hasPerm('catalog.products.view');
  const canCreate = isAdmin || hasPerm('catalog.products.create');
  const { data: products, loading, error, refetch } = useAsync(productService.getAll);
  const { data: catalogs } = useAsync(async () => {
    const [lines, categories, subcategories, brands, productTypes, units, taxRates, tariffs] = await Promise.all([
      catalogService.productLines({ activeStatus: 'all' }),
      catalogService.categories({ activeStatus: 'all' }),
      catalogService.subcategories({ activeStatus: 'all' }),
      catalogService.brands(false),
      catalogService.productTypes(false),
      catalogService.units(false),
      catalogService.taxRates(false),
      catalogService.tariffs(false),
    ]);
    return { lines, categories, subcategories, brands, productTypes, units, taxRates, tariffs };
  });
  const [showModal, setShowModal] = useState(false);

  const maps = useMemo(() => {
    const toMap = (arr: { id: string; code: string; name: string }[] | undefined) =>
      new Map((arr ?? []).map((x) => [x.id, `${x.code} — ${x.name}`] as const));
    return {
      lines: toMap(catalogs?.lines),
      categories: toMap(catalogs?.categories),
      subcategories: toMap(catalogs?.subcategories),
      brands: toMap(catalogs?.brands),
      productTypes: toMap(catalogs?.productTypes),
      units: toMap(catalogs?.units),
      taxRates: toMap(catalogs?.taxRates),
      tariffs: toMap(catalogs?.tariffs),
    };
  }, [catalogs]);

  const show = (m: Map<string, string>, id: string) => {
    if (!id || id === '00000000-0000-0000-0000-000000000000') return '—';
    return m.get(id) ?? id;
  };

  if (!canView) {
    return (
      <div className="page-shell">
        <h1 className="page-title">{t('products.title')}</h1>
        <p className="page-subtitle">{t('common.noAccess')}</p>
      </div>
    );
  }

  return (
    <PageShell
      title={t('products.title')}
      action={
        canCreate
          ? <button type="button" onClick={() => setShowModal(true)}>{t('products.actions.create')}</button>
          : undefined
      }
    >
      {showModal && (
        <CreateProductModal
          onClose={() => setShowModal(false)}
          onCreated={refetch}
        />
      )}
      {loading && <LoadingState />}
      {error   && <ErrorState message={error} />}
      {!loading && !error && products?.length === 0 && (
        <EmptyState message={t('products.empty')} />
      )}
      {!loading && !error && products && products.length > 0 && (
        <TableCard>
          <table>
            <thead>
              <tr>
                <th>{t('products.table.code')}</th>
                <th>{t('products.table.name')}</th>
                <th>{t('products.table.description')}</th>
                <th>{t('products.table.line')}</th>
                <th>{t('products.table.brand')}</th>
                <th>{t('products.table.productType')}</th>
                <th>{t('products.table.unit')}</th>
                <th>{t('products.table.category')}</th>
                <th>{t('products.table.subcategory')}</th>
                <th>{t('products.table.saleTax')}</th>
                <th>{t('products.table.purchaseTax')}</th>
                <th>{t('products.table.tariff')}</th>
                <th>{t('products.table.type')}</th>
                <th>{t('products.table.sale')}</th>
                <th>{t('products.table.status')}</th>
              </tr>
            </thead>
            <tbody>
              {products.map((p) => (
                <tr key={p.id}>
                  <td><span className="mono">{p.saleCode}</span></td>
                  <td>{p.shortName}</td>
                  <td style={{ color: '#6b7280' }}>{p.description}</td>
                  <td>{show(maps.lines, p.lineId)}</td>
                  <td>{show(maps.brands, p.brandId)}</td>
                  <td>{show(maps.productTypes, p.productTypeId)}</td>
                  <td>{show(maps.units, p.unitOfMeasureId)}</td>
                  <td>{show(maps.categories, p.categoryId)}</td>
                  <td>{show(maps.subcategories, p.subcategoryId)}</td>
                  <td>{show(maps.taxRates, p.saleTaxId)}</td>
                  <td>{show(maps.taxRates, p.purchaseTaxId)}</td>
                  <td>{show(maps.tariffs, p.tariffId)}</td>
                  <td>
                    <Badge
                      label={p.isService ? t('products.badge.service') : t('products.badge.product')}
                      variant={p.isService ? 'blue' : 'gray'}
                    />
                  </td>
                  <td>
                    <Badge
                      label={p.isForSale ? t('common.yes') : t('common.no')}
                      variant={p.isForSale ? 'green' : 'gray'}
                    />
                  </td>
                  <td>
                    <Badge
                      label={p.isActive ? t('common.active') : t('common.inactive')}
                      variant={p.isActive ? 'green' : 'red'}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableCard>
      )}
    </PageShell>
  );
}
