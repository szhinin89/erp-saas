import type { ItemDto } from '../../../types/items';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { Badge } from '../../../components/PageShell';

type Props = {
  items:    ItemDto[];
  loading:  boolean;
  t:        (key: string, fallback?: string) => string;
  onView:   (item: ItemDto) => void;
  onEdit:   (item: ItemDto) => void;
  onToggle: (item: ItemDto) => void;
  toggling: boolean;
};

export function ItemListTable({ items, loading, t, onView, onEdit, onToggle, toggling }: Props) {
  if (loading) {
    return <p className="loading-state">{t('common.loading', 'Cargando...')}</p>;
  }

  if (items.length === 0) {
    return (
      <p className="empty-state">
        {t('items.list.empty', 'No se encontraron ítems con los filtros actuales.')}
      </p>
    );
  }

  return (
    <div className="prd-table-wrap">
      <table className="table">
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
            <tr key={item.id} className={item.isActive ? undefined : 'prd-row--inactive'}>
              <td>
                <code className="prd-sku">{item.sku}</code>
              </td>
              <td>
                <div className="prd-name">{item.shortName}</div>
                <div className="prd-desc-subtle">{item.description}</div>
              </td>
              <td>
                <span title={item.itemTypeName}>{item.itemTypeName}</span>
              </td>
              <td><code title={item.defaultUomCode}>{item.defaultUomAbbrev}</code></td>
              <td>
                <div className="prd-badge-wrap">
                  {item.tracksLot    && <Badge label="LOT" variant="blue" size="md" title="Lotes" />}
                  {item.tracksSeries && <Badge label="SER" variant="blue" size="md" title="Series" />}
                  {item.isForSale    && <Badge label="VENTA" variant="green" size="md" title="En venta" />}
                  {item.isEcommerceActive && <Badge label="EC" variant="gray" size="md" title="eCommerce" />}
                </div>
              </td>
              <td>
                <span className={item.isActive ? 'prd-status-badge prd-status-badge--active' : 'prd-status-badge prd-status-badge--inactive'}>
                  {item.isActive ? t('common.active', 'Activo') : t('common.inactive', 'Inactivo')}
                </span>
              </td>
              <td>
                <div className="prd-row-actions">
                  <ZHBtn
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => onView(item)}
                    title={t('common.viewDetail', 'Ver detalle')}
                  >
                    <span className="material-symbols-outlined zh-icon-lg">visibility</span>
                  </ZHBtn>
                  <ZHBtn
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => onEdit(item)}
                    title={t('common.edit', 'Editar')}
                  >
                    <span className="material-symbols-outlined zh-icon-lg">edit</span>
                  </ZHBtn>
                  <ZHBtn
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => onToggle(item)}
                    disabled={toggling}
                    title={item.isActive ? t('common.disable', 'Deshabilitar') : t('common.enable', 'Habilitar')}
                  >
                    <span className="material-symbols-outlined zh-icon-lg">
                      {item.isActive ? 'toggle_on' : 'toggle_off'}
                    </span>
                  </ZHBtn>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
