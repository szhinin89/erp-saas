import type { ItemDto } from '../../../types/items';

type Props = {
  items:    ItemDto[];
  loading:  boolean;
  t:        (key: string, fallback?: string) => string;
  onEdit:   (item: ItemDto) => void;
  onToggle: (item: ItemDto) => void;
  toggling: boolean;
};

const ITEM_TYPE_ICONS: Record<string, string> = {
  Physical: '📦',
  Service:  '🔧',
  Digital:  '💾',
  Kit:      '🧩',
  Bundle:   '🎁',
};

export function ItemListTable({ items, loading, t, onEdit, onToggle, toggling }: Props) {
  if (loading) {
    return <p style={{ padding: 24, color: 'var(--color-text-secondary)' }}>{t('common.loading', 'Cargando...')}</p>;
  }

  if (items.length === 0) {
    return (
      <p style={{ padding: 24, color: 'var(--color-text-secondary)' }}>
        {t('items.list.empty', 'No se encontraron ítems con los filtros actuales.')}
      </p>
    );
  }

  return (
    <table className="zh-table">
      <thead>
        <tr>
          <th>{t('items.list.col.sku', 'SKU')}</th>
          <th>{t('items.list.col.name', 'Nombre')}</th>
          <th>{t('items.list.col.type', 'Tipo')}</th>
          <th>{t('items.list.col.uom', 'UOM')}</th>
          <th>{t('items.list.col.flags', 'Flags')}</th>
          <th>{t('common.status', 'Estado')}</th>
          <th>{t('common.actions', 'Acciones')}</th>
        </tr>
      </thead>
      <tbody>
        {items.map((item) => (
          <tr key={item.id} style={{ opacity: item.isActive ? 1 : 0.6 }}>
            <td>
              <code style={{ fontWeight: 600, fontSize: 13 }}>{item.sku}</code>
              {item.purchaseCode && (
                <div style={{ fontSize: 11, color: 'var(--color-text-tertiary)' }}>
                  {item.purchaseCode}
                </div>
              )}
            </td>
            <td>
              <div style={{ fontWeight: 500 }}>{item.shortName}</div>
              <div style={{ fontSize: 12, color: 'var(--color-text-tertiary)' }}>{item.description}</div>
            </td>
            <td>
              <span title={item.itemType}>
                {ITEM_TYPE_ICONS[item.itemType] ?? '?'} {item.itemType}
              </span>
            </td>
            <td><code>{item.defaultUomCode}</code></td>
            <td>
              <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
                {item.tracksLot    && <span className="zh-badge zh-badge--info" title="Lotes">LOT</span>}
                {item.tracksSeries && <span className="zh-badge zh-badge--info" title="Series">SER</span>}
                {item.isForSale    && <span className="zh-badge zh-badge--success" title="En venta">VENTA</span>}
                {item.isEcommerceActive && <span className="zh-badge zh-badge--neutral" title="eCommerce">EC</span>}
              </div>
            </td>
            <td>
              <span className={item.isActive ? 'zh-badge zh-badge--success' : 'zh-badge zh-badge--neutral'}>
                {item.isActive ? t('common.active', 'Activo') : t('common.inactive', 'Inactivo')}
              </span>
            </td>
            <td>
              <div style={{ display: 'flex', gap: 8 }}>
                <button
                  type="button"
                  className="zh-btn zh-btn--ghost zh-btn--sm"
                  onClick={() => onEdit(item)}
                  title={t('common.edit', 'Editar')}
                >
                  <span className="material-symbols-outlined" style={{ fontSize: 18 }}>edit</span>
                </button>
                <button
                  type="button"
                  className="zh-btn zh-btn--ghost zh-btn--sm"
                  onClick={() => onToggle(item)}
                  disabled={toggling}
                  title={item.isActive ? t('common.disable', 'Deshabilitar') : t('common.enable', 'Habilitar')}
                >
                  <span className="material-symbols-outlined" style={{ fontSize: 18 }}>
                    {item.isActive ? 'toggle_on' : 'toggle_off'}
                  </span>
                </button>
              </div>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
