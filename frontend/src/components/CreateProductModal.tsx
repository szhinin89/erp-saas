import { useState, type FormEvent } from 'react';
import { Modal } from './Modal';
import { productService, type CreateProductRequest } from '../services/productService';

interface Props {
  onClose: () => void;
  onCreated: () => void;
}

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

const EMPTY: CreateProductRequest = {
  saleCode: '',
  purchaseCode: '',
  shortName: '',
  description: '',
  lineId: EMPTY_GUID,
  categoryId: EMPTY_GUID,
  subcategoryId: EMPTY_GUID,
  unitOfMeasureId: EMPTY_GUID,
  brandId: EMPTY_GUID,
  productTypeId: EMPTY_GUID,
  tariffId: EMPTY_GUID,
  saleTaxId: EMPTY_GUID,
  purchaseTaxId: EMPTY_GUID,
  isService: false,
  isForSale: true,
  availableOnWeb: false,
  availableOnMobile: false,
};

export function CreateProductModal({ onClose, onCreated }: Props) {
  const [form, setForm]       = useState<CreateProductRequest>(EMPTY);
  const [error, setError]     = useState('');
  const [loading, setLoading] = useState(false);

  const set = (field: keyof CreateProductRequest, value: unknown) =>
    setForm((f) => ({ ...f, [field]: value }));

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await productService.create(form);
      onCreated();
      onClose();
    } catch (err: unknown) {
      setError(
        (err as { response?: { data?: { error?: string } } })
          ?.response?.data?.error ?? 'Error al crear el producto'
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal title="Nuevo producto" onClose={onClose} width={560}>
      <form onSubmit={handleSubmit}>
        <div className="form-grid form-grid--2col">
          <div className="field">
            <label htmlFor="saleCode">Código de venta *</label>
            <input
              id="saleCode"
              value={form.saleCode}
              onChange={(e) => set('saleCode', e.target.value)}
              placeholder="PROD-001"
              required
            />
          </div>

          <div className="field">
            <label htmlFor="purchaseCode">Código de compra</label>
            <input
              id="purchaseCode"
              value={form.purchaseCode ?? ''}
              onChange={(e) => set('purchaseCode', e.target.value)}
              placeholder="COMP-001"
            />
          </div>

          <div className="field field--span2">
            <label htmlFor="shortName">Nombre corto *</label>
            <input
              id="shortName"
              value={form.shortName}
              onChange={(e) => set('shortName', e.target.value)}
              placeholder="Laptop Dell Inspiron"
              required
            />
          </div>

          <div className="field field--span2">
            <label htmlFor="description">Descripción *</label>
            <input
              id="description"
              value={form.description}
              onChange={(e) => set('description', e.target.value)}
              placeholder="Descripción completa del producto"
              required
            />
          </div>

          <div className="field field--span2" style={{ borderTop: '1px solid #f0f0f0', paddingTop: 12 }}>
            <p style={{ margin: '0 0 10px', fontSize: 12, color: '#6b7280' }}>
              Comportamiento
            </p>
            <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap' }}>
              {([
                ['isService', 'Es servicio'],
                ['isForSale', 'Disponible para venta'],
                ['availableOnWeb', 'Disponible en web'],
                ['availableOnMobile', 'Disponible en móvil'],
              ] as [keyof CreateProductRequest, string][]).map(([key, label]) => (
                <div key={key} className="field field--inline">
                  <input
                    id={key}
                    type="checkbox"
                    checked={form[key] as boolean}
                    onChange={(e) => set(key, e.target.checked)}
                  />
                  <label htmlFor={key}>{label}</label>
                </div>
              ))}
            </div>
          </div>
        </div>

        {error && <p className="form-error" style={{ marginTop: 14 }}>{error}</p>}

        <div className="form-actions">
          <button type="button" className="btn btn--ghost" onClick={onClose}>Cancelar</button>
          <button type="submit" className="btn btn--primary" disabled={loading}>
            {loading ? 'Guardando...' : 'Crear producto'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
