import { EmptyState } from '../../../../components/PageShell';
import { ZHBtn } from '../../../../components/zh/ZHForm';
import type { ItemImageDto, ItemUnitConversionDto, ItemSubstituteDto, ItemPackagingLevelDto } from '../../../../types/items';

type T = (key: string, fallback?: string) => string;

interface BaseSectionProps {
  title: string;
  icon: string;
  emptyMessage: string;
  children: React.ReactNode;
  count: number;
}

function SectionWrapper({ title, icon, emptyMessage, children, count }: BaseSectionProps) {
  return (
    <div className="pg-section">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">{icon}</span>
          <span className="pg-section-label">{title} ({count})</span>
        </div>
      </div>
      {count === 0 ? (
        <div className="pg-pad-40"><EmptyState message={emptyMessage} /></div>
      ) : children}
    </div>
  );
}

export function ImagesSection({ t, images, disabled = false, onDisable }: {
  t: T; images: ItemImageDto[]; disabled?: boolean; onDisable: (id: string) => Promise<void>;
}) {
  const active = images.filter(i => i.isActive);
  return (
    <SectionWrapper
      title={t('items.images.sectionTitle', 'Imágenes')}
      icon="image"
      emptyMessage={t('items.images.empty', 'No hay imágenes.')}
      count={active.length}
    >
      <div className="pg-overflow-x">
        <table className="table">
          <thead>
            <tr>
              <th>{t('items.images.col.storageObjectId', 'ID Objeto')}</th>
              <th>{t('items.images.col.altText', 'Alt Text')}</th>
              <th>{t('items.images.col.isMain', 'Principal')}</th>
              <th>{t('items.images.col.isEcommerce', 'eCommerce')}</th>
              <th>{t('items.images.col.sortOrder', 'Orden')}</th>
              <th className="pg-th-right">{t('common.actions', 'Acciones')}</th>
            </tr>
          </thead>
          <tbody>
            {active.map(img => (
              <tr key={img.id}>
                <td><code>{img.storageObjectId.slice(0, 12)}…</code></td>
                <td>{img.altText || '—'}</td>
                <td>{img.isMain ? t('common.yes', 'Sí') : t('common.no', 'No')}</td>
                <td>{img.isEcommerce ? t('common.yes', 'Sí') : t('common.no', 'No')}</td>
                <td>{img.sortOrder}</td>
                <td className="pg-td-right">
                  <ZHBtn type="button" variant="ghost" size="sm" title={t('common.deactivate', 'Desactivar')}
                    onClick={() => void onDisable(img.id)} disabled={disabled}>
                    <span className="material-symbols-outlined">block</span>
                  </ZHBtn>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </SectionWrapper>
  );
}

export function UnitConversionsSection({ t, conversions }: { t: T; conversions: ItemUnitConversionDto[] }) {
  const active = conversions.filter(c => c.isActive);
  return (
    <SectionWrapper
      title={t('items.conversions.sectionTitle', 'Conversiones de Unidad')}
      icon="swap_horiz"
      emptyMessage={t('items.conversions.empty', 'No hay conversiones configuradas.')}
      count={active.length}
    >
      <div className="pg-overflow-x">
        <table className="table">
          <thead>
            <tr>
              <th>{t('items.conversions.col.from', 'Desde')}</th>
              <th>{t('items.conversions.col.to', 'Hacia')}</th>
              <th>{t('items.conversions.col.factor', 'Factor')}</th>
            </tr>
          </thead>
          <tbody>
            {active.map(c => (
              <tr key={c.id}>
                <td><code title={c.fromUomCode}>{c.fromUomAbbrev}</code></td>
                <td><code title={c.toUomCode}>{c.toUomAbbrev}</code></td>
                <td>{c.factor}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </SectionWrapper>
  );
}

export function SubstitutesSection({ t, substitutes }: { t: T; substitutes: ItemSubstituteDto[] }) {
  const active = substitutes.filter(s => s.isActive);
  return (
    <SectionWrapper
      title={t('items.substitutes.sectionTitle', 'Sustitutos')}
      icon="swap_calls"
      emptyMessage={t('items.substitutes.empty', 'No hay sustitutos configurados.')}
      count={active.length}
    >
      <div className="pg-overflow-x">
        <table className="table">
          <thead>
            <tr>
              <th>{t('items.substitutes.col.itemId', 'Item Sustituto ID')}</th>
              <th>{t('items.substitutes.col.priority', 'Prioridad')}</th>
              <th>{t('items.substitutes.col.note', 'Nota')}</th>
            </tr>
          </thead>
          <tbody>
            {active.map(s => (
              <tr key={s.id}>
                <td><code>{s.substituteItemId.slice(0, 12)}…</code></td>
                <td>{s.priority}</td>
                <td>{s.note || '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </SectionWrapper>
  );
}

export function PackagingLevelsSection({ t, levels }: { t: T; levels: ItemPackagingLevelDto[] }) {
  const active = levels.filter(l => l.isActive);
  return (
    <SectionWrapper
      title={t('items.packaging.sectionTitle', 'Niveles de Empaque')}
      icon="inventory_2"
      emptyMessage={t('items.packaging.empty', 'No hay niveles de empaque configurados.')}
      count={active.length}
    >
      <div className="pg-overflow-x">
        <table className="table">
          <thead>
            <tr>
              <th>{t('items.packaging.col.level', 'Nivel')}</th>
              <th>{t('items.packaging.col.name', 'Nombre')}</th>
              <th>{t('items.packaging.col.baseQuantity', 'Cantidad Base')}</th>
              <th>{t('items.packaging.col.uom', 'UOM')}</th>
              <th>{t('items.packaging.col.barcode', 'Barcode')}</th>
              <th>{t('items.packaging.col.weight', 'Peso')}</th>
              <th>{t('items.packaging.col.isBaseUnit', 'Unidad Base')}</th>
              <th>{t('items.packaging.col.isPurchaseDefault', 'Compra')}</th>
              <th>{t('items.packaging.col.isSaleDefault', 'Venta')}</th>
            </tr>
          </thead>
          <tbody>
            {active.map(l => (
              <tr key={l.id}>
                <td>{l.level}</td>
                <td><strong>{l.name}</strong></td>
                <td>{l.baseQuantity}</td>
                <td><code title={l.uomCode}>{l.uomAbbrev}</code></td>
                <td>{l.barcode ? <code>{l.barcode}</code> : '—'}</td>
                <td>{l.weight ?? '—'}</td>
                <td>{l.isBaseUnit ? t('common.yes', 'Sí') : t('common.no', 'No')}</td>
                <td>{l.isPurchaseDefault ? t('common.yes', 'Sí') : t('common.no', 'No')}</td>
                <td>{l.isSaleDefault ? t('common.yes', 'Sí') : t('common.no', 'No')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </SectionWrapper>
  );
}
