import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage, PageShell } from '../../../../components/PageShell';
import { Card } from '../../../../components/ui/Card';
import { ZHBtn, ZHField, ZHFormSection, ZHGrid } from '../../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useOrdenCompraAcciones } from '../hooks/useOrdenesCompra';
import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';
import type { ItemOrdenCompraRequest } from '../api/ordenCompraService';
import './ordenes-compra-pages.css';

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
  const canCreate = hasPerm('compras.ordenes.create');

  const [proveedores, setProveedores] = useState<ProveedorOpcion[]>([]);
  const [productos,   setProductos]   = useState<ProductoOpcion[]>([]);

  useEffect(() => {
    api.get<ApiResponse<ProveedorOpcion[]>>('/api/proveedores')
      .then((r) => setProveedores(r.data.responseObject ?? []));
    api.get<ApiResponse<ProductoOpcion[]>>('/api/products')
      .then((r) => setProductos((r.data.responseObject ?? []).filter((p) => p.isActive)));
  }, []);

  const [proveedorId,      setProveedorId]      = useState('');
  const [fechaRequerida,   setFechaRequerida]   = useState('');
  const [bodegaDestinoId] = useState('');
  const [direccionEntrega, setDireccionEntrega] = useState('');
  const [observaciones,    setObservaciones]    = useState('');
  const [items,            setItems]            = useState<ItemRow[]>([emptyItem()]);
  const [localError,       setLocalError]       = useState<string | null>(null);

  const { loading, error, crear } = useOrdenCompraAcciones(
    () => navigate('/compras/ordenes')
  );

  const updateItem = (idx: number, field: keyof ItemRow, val: string) =>
    setItems((prev) => prev.map((it, i) => i === idx ? { ...it, [field]: val } : it));

  const removeItem = (idx: number) =>
    setItems((prev) => prev.filter((_, i) => i !== idx));

  const subtotal = items.reduce((sum, it) => {
    const qty   = parseFloat(it.cantidad) || 0;
    const price = parseFloat(it.precioUnitario) || 0;
    return sum + qty * price;
  }, 0);

  const total = items.reduce((sum, it) => {
    const qty   = parseFloat(it.cantidad)      || 0;
    const price = parseFloat(it.precioUnitario) || 0;
    const iva   = parseFloat(it.ivaPorcentaje)  || 0;
    const sub   = qty * price;
    return sum + sub + sub * (iva / 100);
  }, 0);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLocalError(null);

    if (!proveedorId)    return setLocalError('Seleccione un proveedor.');
    if (!fechaRequerida) return setLocalError('Ingrese la fecha requerida.');
    if (items.some((it) => !it.productoId || !it.cantidad || !it.precioUnitario))
      return setLocalError('Complete todos los campos de los ítems.');

    const parsedItems: ItemOrdenCompraRequest[] = items.map((it) => ({
      productoId:    it.productoId,
      cantidad:      parseFloat(it.cantidad),
      precioUnitario: parseFloat(it.precioUnitario),
      ivaPorcentaje: parseFloat(it.ivaPorcentaje) || 15,
    }));

    await crear({
      proveedorId,
      fechaRequerida: new Date(fechaRequerida).toISOString(),
      bodegaDestinoId:  bodegaDestinoId  || null,
      direccionEntrega: direccionEntrega || null,
      observaciones:    observaciones    || null,
      items: parsedItems,
    });
  };

  if (!canCreate) return <NoAccessPage title="Nueva Orden de Compra" />;

  return (
    <PageShell kicker="Compras" title="Nueva orden de compra">
      <form onSubmit={handleSubmit}>
        <Card>
          {(localError || error) && (
            <ZHPageNotice variant="error" message="Error" detail={localError ?? error ?? ''} />
          )}

          <ZHFormSection title="Información general">
            <ZHGrid cols={2}>
              <ZHField label="Proveedor *">
                <select value={proveedorId} onChange={(e) => setProveedorId(e.target.value)} required>
                  <option value="">Seleccionar proveedor…</option>
                  {proveedores.map((p) => (
                    <option key={p.id} value={p.id}>{p.razonSocial}</option>
                  ))}
                </select>
              </ZHField>

              <ZHField label="Fecha requerida *">
                <input
                  type="date"
                  value={fechaRequerida}
                  onChange={(e) => setFechaRequerida(e.target.value)}
                  required
                />
              </ZHField>

              <ZHField label="Observaciones">
                <textarea
                  value={observaciones}
                  onChange={(e) => setObservaciones(e.target.value)}
                  rows={2}
                />
              </ZHField>

              <ZHField label="Dirección de entrega">
                <input
                  type="text"
                  value={direccionEntrega}
                  onChange={(e) => setDireccionEntrega(e.target.value)}
                  placeholder="Opcional"
                />
              </ZHField>
            </ZHGrid>
          </ZHFormSection>

          <ZHFormSection title="Líneas de la orden">
            <table className="table oc-create-lines-table">
              <thead>
                <tr>
                  <th>Producto</th>
                  <th className="oc-col-qty">Cantidad</th>
                  <th className="oc-col-price">Precio unit.</th>
                  <th className="oc-col-tax">IVA %</th>
                  <th className="oc-col-total">Total línea</th>
                  <th className="oc-col-actions"></th>
                </tr>
              </thead>
              <tbody>
                {items.map((it, idx) => {
                  const qty   = parseFloat(it.cantidad)      || 0;
                  const price = parseFloat(it.precioUnitario) || 0;
                  const iva   = parseFloat(it.ivaPorcentaje)  || 0;
                  const sub   = qty * price;
                  const tot   = sub + sub * (iva / 100);
                  return (
                    <tr key={idx}>
                      <td>
                        <select
                          value={it.productoId}
                          onChange={(e) => updateItem(idx, 'productoId', e.target.value)}
                        >
                          <option value="">Seleccionar…</option>
                          {productos.map((p) => (
                            <option key={p.id} value={p.id}>{p.shortName}</option>
                          ))}
                        </select>
                      </td>
                      <td>
                        <input
                          type="number" min="0.001" step="0.001"
                          value={it.cantidad}
                          onChange={(e) => updateItem(idx, 'cantidad', e.target.value)}
                        />
                      </td>
                      <td>
                        <input
                          type="number" min="0" step="0.01"
                          value={it.precioUnitario}
                          onChange={(e) => updateItem(idx, 'precioUnitario', e.target.value)}
                        />
                      </td>
                      <td>
                        <select
                          value={it.ivaPorcentaje}
                          onChange={(e) => updateItem(idx, 'ivaPorcentaje', e.target.value)}
                        >
                          <option value="0">0%</option>
                          <option value="5">5%</option>
                          <option value="15">15%</option>
                        </select>
                      </td>
                      <td className="oc-cell-right">${tot.toFixed(2)}</td>
                      <td>
                        {items.length > 1 && (
                          <ZHBtn variant="ghost" size="sm" onClick={() => removeItem(idx)}>✕</ZHBtn>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>

            <div className="oc-lines-footer">
              <ZHBtn variant="secondary" size="sm" onClick={() => setItems((prev) => [...prev, emptyItem()])}>
                + Agregar línea
              </ZHBtn>
              <div className="oc-lines-summary">
                <div>Subtotal: ${subtotal.toFixed(2)}</div>
                <div className="oc-lines-total">
                  Total: ${total.toFixed(2)}
                </div>
              </div>
            </div>
          </ZHFormSection>

          <div className="oc-form-actions">
            <ZHBtn variant="ghost" size="md" onClick={() => navigate('/compras/ordenes')}>
              Cancelar
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="submit" disabled={loading}>
              {loading ? 'Guardando…' : 'Crear orden'}
            </ZHBtn>
          </div>
        </Card>
      </form>
    </PageShell>
  );
}
