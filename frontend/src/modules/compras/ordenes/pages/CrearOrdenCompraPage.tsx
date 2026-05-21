import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage } from '../../../../components/PageShell';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useOrdenCompraAcciones } from '../hooks/useOrdenesCompra';
import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';
import type { ItemOrdenCompraRequest } from '../api/ordenCompraService';

interface ProveedorOpcion { id: string; razonSocial: string; }
interface ProductoOpcion  { id: string; shortName: string; isActive: boolean; }

interface ItemRow {
  productoId: string;
  cantidad: string;
  precioUnitario: string;
  ivaPorcentaje: string;
}

const emptyItem = (): ItemRow => ({ productoId: '', cantidad: '', precioUnitario: '', ivaPorcentaje: '15' });

export function CrearOrdenCompraPage() {
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const canCreate = hasPerm('purchases.orders.create');

  const [proveedores, setProveedores] = useState<ProveedorOpcion[]>([]);
  const [productos,   setProductos]   = useState<ProductoOpcion[]>([]);

  useEffect(() => {
    api.get<ApiResponse<ProveedorOpcion[]>>('/api/purchases/suppliers')
      .then((r) => setProveedores(r.data.responseObject ?? []));
    api.get<ApiResponse<ProductoOpcion[]>>('/api/inventory/products')
      .then((r) => setProductos((r.data.responseObject ?? []).filter((p) => p.isActive)));
  }, []);

  const [proveedorId,      setProveedorId]      = useState('');
  const [fechaRequerida,   setFechaRequerida]   = useState('');
  const [bodegaDestinoId]                       = useState('');
  const [direccionEntrega, setDireccionEntrega] = useState('');
  const [observaciones,    setObservaciones]    = useState('');
  const [items,            setItems]            = useState<ItemRow[]>([emptyItem()]);
  const [localError,       setLocalError]       = useState<string | null>(null);

  const { loading, error, crear } = useOrdenCompraAcciones(() => navigate('/compras/ordenes'));

  const updateItem = (idx: number, field: keyof ItemRow, val: string) =>
    setItems((prev) => prev.map((it, i) => i === idx ? { ...it, [field]: val } : it));

  const removeItem = (idx: number) =>
    setItems((prev) => prev.filter((_, i) => i !== idx));

  const subtotal = items.reduce((sum, it) => {
    return sum + (parseFloat(it.cantidad) || 0) * (parseFloat(it.precioUnitario) || 0);
  }, 0);

  const total = items.reduce((sum, it) => {
    const sub = (parseFloat(it.cantidad) || 0) * (parseFloat(it.precioUnitario) || 0);
    return sum + sub + sub * ((parseFloat(it.ivaPorcentaje) || 0) / 100);
  }, 0);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLocalError(null);
    if (!proveedorId)    return setLocalError('Seleccione un proveedor.');
    if (!fechaRequerida) return setLocalError('Ingrese la fecha requerida.');
    if (items.some((it) => !it.productoId || !it.cantidad || !it.precioUnitario))
      return setLocalError('Complete todos los campos de los ítems.');

    const parsedItems: ItemOrdenCompraRequest[] = items.map((it) => ({
      productoId:     it.productoId,
      cantidad:       parseFloat(it.cantidad),
      precioUnitario: parseFloat(it.precioUnitario),
      ivaPorcentaje:  parseFloat(it.ivaPorcentaje) || 15,
    }));

    await crear({
      proveedorId,
      fechaRequerida:   new Date(fechaRequerida).toISOString(),
      bodegaDestinoId:  bodegaDestinoId  || null,
      direccionEntrega: direccionEntrega || null,
      observaciones:    observaciones    || null,
      items: parsedItems,
    });
  };

  if (!canCreate) return <NoAccessPage title="Nueva Orden de Compra" />;

  return (
    <div className="pg-page">

      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">Compras</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Nueva Orden</span>
          </nav>
          <h1 className="pg-title">Nueva Orden de Compra</h1>
          <p className="pg-subtitle">Cree una nueva solicitud de compra a proveedor.</p>
        </div>
      </div>

      {(localError || error) && (
        <ZHPageNotice variant="error" message="Error" detail={localError ?? error ?? ''} />
      )}

      <form onSubmit={(e) => void handleSubmit(e)}>

        {/* General info section */}
        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">info</span>
              <span className="pg-section-label">Información General</span>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label="Proveedor" required>
                <select className="zh-input" value={proveedorId}
                  onChange={(e) => setProveedorId(e.target.value)} required>
                  <option value="">Seleccionar proveedor…</option>
                  {proveedores.map((p) => <option key={p.id} value={p.id}>{p.razonSocial}</option>)}
                </select>
              </ZHField>

              <ZHField label="Fecha requerida" required>
                <input className="zh-input" type="date" value={fechaRequerida}
                  onChange={(e) => setFechaRequerida(e.target.value)} required />
              </ZHField>

              <ZHField label="Observaciones">
                <textarea className="zh-input" rows={2} value={observaciones}
                  onChange={(e) => setObservaciones(e.target.value)} />
              </ZHField>

              <ZHField label="Dirección de entrega">
                <input className="zh-input" type="text" value={direccionEntrega}
                  onChange={(e) => setDireccionEntrega(e.target.value)} placeholder="Opcional" />
              </ZHField>
            </div>
          </div>
        </div>

        {/* Lines section */}
        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">list_alt</span>
              <span className="pg-section-label">Líneas de la Orden</span>
            </div>
            <ZHBtn variant="ghost" size="sm" type="button"
              onClick={() => setItems((prev) => [...prev, emptyItem()])}>
              <span className="material-symbols-outlined">add</span>
              Agregar línea
            </ZHBtn>
          </div>
          <div style={{ overflowX: 'auto' }}>
            <table className="table">
              <thead>
                <tr>
                  <th>Producto</th>
                  <th style={{ width: 100, textAlign: 'right' }}>Cantidad</th>
                  <th style={{ width: 130, textAlign: 'right' }}>Precio unit.</th>
                  <th style={{ width: 90, textAlign: 'right' }}>IVA %</th>
                  <th style={{ width: 120, textAlign: 'right' }}>Total línea</th>
                  <th style={{ width: 48 }} />
                </tr>
              </thead>
              <tbody>
                {items.map((it, idx) => {
                  const sub = (parseFloat(it.cantidad) || 0) * (parseFloat(it.precioUnitario) || 0);
                  const tot = sub + sub * ((parseFloat(it.ivaPorcentaje) || 0) / 100);
                  return (
                    <tr key={idx}>
                      <td>
                        <select className="pg-editable-input" value={it.productoId}
                          onChange={(e) => updateItem(idx, 'productoId', e.target.value)}>
                          <option value="">Seleccionar…</option>
                          {productos.map((p) => <option key={p.id} value={p.id}>{p.shortName}</option>)}
                        </select>
                      </td>
                      <td>
                        <input className="pg-editable-input pg-editable-input--right"
                          type="number" min="0.001" step="0.001"
                          value={it.cantidad}
                          onChange={(e) => updateItem(idx, 'cantidad', e.target.value)} />
                      </td>
                      <td>
                        <input className="pg-editable-input pg-editable-input--right"
                          type="number" min="0" step="0.01"
                          value={it.precioUnitario}
                          onChange={(e) => updateItem(idx, 'precioUnitario', e.target.value)} />
                      </td>
                      <td>
                        <select className="pg-editable-input pg-editable-input--right"
                          value={it.ivaPorcentaje}
                          onChange={(e) => updateItem(idx, 'ivaPorcentaje', e.target.value)}>
                          <option value="0">0%</option>
                          <option value="5">5%</option>
                          <option value="15">15%</option>
                        </select>
                      </td>
                      <td style={{ textAlign: 'right', fontWeight: 600, fontSize: 'var(--text-body-md-size)' }}>
                        ${tot.toFixed(2)}
                      </td>
                      <td style={{ textAlign: 'center' }}>
                        {items.length > 1 && (
                          <button type="button" className="pg-row-delete"
                            onClick={() => removeItem(idx)} aria-label="Eliminar">
                            <span className="material-symbols-outlined" style={{ fontSize: 18 }}>delete</span>
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>

            <button type="button" className="pg-add-row"
              onClick={() => setItems((prev) => [...prev, emptyItem()])}>
              <span className="material-symbols-outlined">add_circle</span>
              Haz clic para agregar una línea
            </button>
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 'var(--space-4)',
            padding: 'var(--space-4) var(--space-5)', borderTop: '1px solid var(--color-border)' }}>
            <span className="subtle">Subtotal: <strong>${subtotal.toFixed(2)}</strong></span>
            <span>Total: <strong className="mono" style={{ color: 'var(--color-primary)', fontSize: 16 }}>
              ${total.toFixed(2)}
            </strong></span>
          </div>
        </div>

        <div className="pg-actions-bar">
          <div className="pg-actions-info" />
          <div className="pg-actions-buttons">
            <ZHBtn variant="ghost" size="md" type="button"
              disabled={loading} onClick={() => navigate('/compras/ordenes')}>
              Cancelar
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="submit" disabled={loading}>
              <span className="material-symbols-outlined">save</span>
              {loading ? 'Guardando…' : 'Crear Orden'}
            </ZHBtn>
          </div>
        </div>
      </form>
    </div>
  );
}
