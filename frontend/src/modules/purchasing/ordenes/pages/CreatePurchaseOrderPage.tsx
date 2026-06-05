import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { ZhDecimalInput, ZhDateInput } from '../../../../components/zh/inputs';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { usePurchaseOrderActions } from '../hooks/usePurchaseOrders';
import { useAuthStore } from '../../../../store/authStore';
import { businessPartnerFacade } from '../../../masterData/api/businessPartnerFacade';
import { api } from '../../../lib/api';
import type { ApiResponse } from '../../../../types/api';
import type { PurchaseOrderItemRequest } from '../api/purchaseOrderService';
import './orden-compra-page.css';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';

import type { SupplierPickerRow } from '../../../masterData/types/businessPartner.types';
interface ProductoOpcion  { id: string; shortName: string; isActive: boolean; }

interface ItemRow {
  productId: string;
  quantity: string;
  unitPrice: string;
  vatPct: string;
}

const emptyItem = (): ItemRow => ({ productId: '', quantity: '', unitPrice: '', vatPct: '15' });

export function CrearOrdenCompraPage() {
  const { canShow } = usePermissionsUi();
  const navigate  = useNavigate();
  const canCreate = canShow('purchases.orders.create');

  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);
  // V2: businessPartnerId es el ID canónico del proveedor (antes: proveedorId)
  const [businessPartnerId, setBusinessPartnerId] = useState('');
  const [proveedores, setProveedores] = useState<SupplierPickerRow[]>([]);
  const [productos,   setProductos]   = useState<ProductoOpcion[]>([]);

  const loadPickerData = useCallback(() => {
    setBusinessPartnerId('');
    void businessPartnerFacade.searchSuppliersForPicker().then(setProveedores);
    api.get<ApiResponse<ProductoOpcion[]>>('/api/inventory/products')
      .then((r) => setProductos((r.data.responseObject ?? []).filter((p) => p.isActive)));
  }, []);

  useEffect(() => {
    loadPickerData();
  }, [loadPickerData, companySessionVersion]);

  const [requiredDate,   setFechaRequerida]   = useState('');
  const [targetWarehouseId]                       = useState('');
  const [deliveryAddress, setDireccionEntrega] = useState('');
  const [notes,    setObservaciones]    = useState('');
  const [items,            setItems]            = useState<ItemRow[]>([emptyItem()]);
  const [localError,       setLocalError]       = useState<string | null>(null);

  const { loading, error, crear } = usePurchaseOrderActions(() => navigate('/compras/ordenes'));

  const updateItem = (idx: number, field: keyof ItemRow, val: string) =>
    setItems((prev) => prev.map((it, i) => i === idx ? { ...it, [field]: val } : it));

  const removeItem = (idx: number) =>
    setItems((prev) => prev.filter((_, i) => i !== idx));

  const subtotal = items.reduce((sum, it) => {
    return sum + (parseFloat(it.quantity) || 0) * (parseFloat(it.unitPrice) || 0);
  }, 0);

  const total = items.reduce((sum, it) => {
    const sub = (parseFloat(it.quantity) || 0) * (parseFloat(it.unitPrice) || 0);
    return sum + sub + sub * ((parseFloat(it.vatPct) || 0) / 100);
  }, 0);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLocalError(null);
    if (!businessPartnerId) return setLocalError('Seleccione un proveedor.');
    if (!requiredDate)    return setLocalError('Ingrese la fecha requerida.');
    if (items.some((it) => !it.productId || !it.cantidad || !it.unitPrice))
      return setLocalError('Complete todos los campos de los ítems.');

    const parsedItems: PurchaseOrderItemRequest[] = items.map((it) => ({
      productId:     it.productId,
      quantity:       parseFloat(it.quantity),
      unitPrice: parseFloat(it.unitPrice),
      vatPct:  parseFloat(it.vatPct) || 15,
    }));

    await crear({
      businessPartnerId,
      requiredDate:   new Date(requiredDate).toISOString(),
      targetWarehouseId:  targetWarehouseId  || null,
      deliveryAddress: deliveryAddress || null,
      notes:    notes    || null,
      items: parsedItems,
    });
  };

  if (!canCreate) return <NoAccessPage title="Nueva Orden de Compra" />;

  return (
    <ErpPageTemplate
      kicker="Compras"
      title="Nueva Orden de Compra"
      subtitle="Cree una nueva solicitud de compra a proveedor."
    >
      {(localError || error) && (
        <ZHPageNotice variant="error" message="Error" detail={localError ?? error ?? ''} />
      )}

      <form onSubmit={(e) => void handleSubmit(e)}>
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">info</span>
              <span className="pg-section-label">Información General</span>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label="Proveedor" required>
                <select className="zh-input" value={businessPartnerId}
                  onChange={(e) => setBusinessPartnerId(e.target.value)} required>
                  <option value="">Seleccionar proveedor…</option>
                  {proveedores.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.fullName} ({p.identificationNumber})
                    </option>
                  ))}
                </select>
              </ZHField>

              <ZHField label="Fecha requerida" required>
                <ZhDateInput value={requiredDate}
                  onChange={(e) => setFechaRequerida(e.target.value)} required />
              </ZHField>

              <ZHField label="notes">
                <input className="zh-input" value={notes}
                  onChange={(e) => setObservaciones(e.target.value)}
                  placeholder="Notas adicionales…" />
              </ZHField>

              <ZHField label="Dirección de entrega">
                <input className="zh-input" value={deliveryAddress}
                  onChange={(e) => setDireccionEntrega(e.target.value)}
                  placeholder="Dirección opcional…" />
              </ZHField>
            </div>
          </div>
        </div>

        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">list</span>
              <span className="pg-section-label">Ítems</span>
            </div>
            <ZHBtn variant="ghost" size="sm" type="button" onClick={() => setItems((p) => [...p, emptyItem()])}>
              + Agregar ítem
            </ZHBtn>
          </div>
          <div className="pg-section-body">
            <table className="oc-items-table">
              <thead>
                <tr>
                  <th>Producto</th>
                  <th>Cantidad</th>
                  <th>Precio Unit.</th>
                  <th>IVA %</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {items.map((it, idx) => (
                  <tr key={idx}>
                    <td>
                      <select className="zh-input" value={it.productId}
                        onChange={(e) => updateItem(idx, 'productId', e.target.value)} required>
                        <option value="">Seleccionar…</option>
                        {productos.map((p) => (
                          <option key={p.id} value={p.id}>{p.shortName}</option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <ZhDecimalInput className="zh-input" decimals={2} positiveOnly
                        value={it.cantidad} onChange={(e) => updateItem(idx, 'cantidad', e.target.value)} />
                    </td>
                    <td>
                      <ZhDecimalInput className="zh-input" decimals={2} positiveOnly
                        value={it.unitPrice} onChange={(e) => updateItem(idx, 'unitPrice', e.target.value)} />
                    </td>
                    <td>
                      <ZhDecimalInput className="zh-input" decimals={0} positiveOnly
                        value={it.vatPct} onChange={(e) => updateItem(idx, 'vatPct', e.target.value)} />
                    </td>
                    <td>
                      {items.length > 1 && (
                        <ZHBtn variant="ghost" size="sm" type="button" onClick={() => removeItem(idx)}>✕</ZHBtn>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="oc-totals">
              <span>Subtotal: <strong>${subtotal.toFixed(2)}</strong></span>
              <span>Total: <strong>${total.toFixed(2)}</strong></span>
            </div>
          </div>
        </div>

        <div className="pg-actions">
          <ZHBtn variant="ghost" type="button" onClick={() => navigate('/compras/ordenes')}>Cancelar</ZHBtn>
          <ZHBtn variant="primary" type="submit" disabled={loading}>
            {loading ? 'Guardando…' : 'Crear Orden'}
          </ZHBtn>
        </div>
      </form>
    </ErpPageTemplate>
  );
}
