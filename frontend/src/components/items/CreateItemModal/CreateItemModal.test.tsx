// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, fireEvent, cleanup } from '@testing-library/react';
import { I18nProvider } from '../../../i18n/i18n';
import { CreateItemModal } from './CreateItemModal';
import { itemService } from '../../../modules/items/api/itemService';
import { apiGet } from '../../../modules/lib/apiEnvelope';
import type { CreateItemInitialData, ItemCreatedResult } from './types';

vi.mock('../../../modules/items/api/itemService', () => ({
  itemService: { create: vi.fn() },
}));

vi.mock('../../../modules/items/hooks/useItemTypeOptions', () => ({
  useItemTypeOptions: () => ({ data: [{ id: 'type-1', name: 'Bien' }] }),
}));

vi.mock('../../../modules/lib/apiEnvelope', () => ({
  apiGet: vi.fn((url: string) => {
    if (url.includes('brands')) return Promise.resolve([{ id: 'brand-1', name: 'Marca X' }]);
    if (url.includes('category-nodes')) {
      return Promise.resolve({ nodes: [{ id: 'cat-1', name: 'Categoría 1', path: '', parentId: null, isActive: true }] });
    }
    if (url.includes('sri-uom')) return Promise.resolve([{ code: 'UNIT', name: 'Unidad', abbrev: 'U' }]);
    if (url.includes('barcode-types')) return Promise.resolve([{ code: 'EAN13', name: 'EAN-13' }]);
    return Promise.resolve([]);
  }),
}));

function renderModal(props: {
  open?: boolean;
  initialData?: CreateItemInitialData;
  onClose?: () => void;
  onCreated?: (item: ItemCreatedResult) => void;
} = {}) {
  const onClose = props.onClose ?? vi.fn();
  const onCreated = props.onCreated ?? vi.fn();
  const utils = render(
    <I18nProvider>
      <CreateItemModal
        open={props.open ?? true}
        initialData={props.initialData}
        onClose={onClose}
        onCreated={onCreated}
      />
    </I18nProvider>,
  );
  return { ...utils, onClose, onCreated };
}

function fieldByName(container: HTMLElement, name: string): HTMLInputElement {
  const el = container.querySelector(`[name="${name}"]`);
  if (!el) throw new Error(`[name="${name}"] no encontrado`);
  return el as HTMLInputElement;
}

async function waitForCatalogs() {
  await waitFor(() => expect(vi.mocked(apiGet)).toHaveBeenCalled());
}

async function fillRequiredFields(container: HTMLElement, overrides: Partial<Record<'sku' | 'barcode', string>> = {}) {
  fireEvent.change(fieldByName(container, 'sku'), { target: { value: overrides.sku ?? 'SKU-1' } });
  fireEvent.change(fieldByName(container, 'itemTypeId'), { target: { value: 'type-1' } });
  fireEvent.change(fieldByName(container, 'categoryNodeId'), { target: { value: 'cat-1' } });
  fireEvent.change(fieldByName(container, 'brandId'), { target: { value: 'brand-1' } });
  fireEvent.change(fieldByName(container, 'defaultUomCode'), { target: { value: 'UNIT' } });
  if (overrides.barcode !== undefined) {
    fireEvent.change(fieldByName(container, 'barcode'), { target: { value: overrides.barcode } });
  }
  fireEvent.change(fieldByName(container, 'barcodeType'), { target: { value: 'EAN13' } });
}

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  cleanup();
});

describe('CreateItemModal — render', () => {
  it('no renderiza el formulario cuando open=false', () => {
    renderModal({ open: false });

    expect(screen.queryByText('Crear producto')).toBeNull();
  });

  it('prellena los campos desde initialData', async () => {
    const { container } = renderModal({
      initialData: { name: 'ACEITE GIRASOL 1L', barcode: '00125', supplierCode: '00125', supplierName: 'Distribuidora ABC' },
    });
    await waitForCatalogs();

    expect(fieldByName(container, 'shortName').value).toBe('ACEITE GIRASOL 1L');
    expect(fieldByName(container, 'description').value).toBe('ACEITE GIRASOL 1L');
    expect(fieldByName(container, 'barcode').value).toBe('00125');
    expect(screen.getByDisplayValue('Distribuidora ABC')).toBeTruthy();
  });
});

describe('CreateItemModal — envío', () => {
  it('éxito: llama itemService.create con el payload esperado (incluye supplierCodes) y dispara onCreated', async () => {
    vi.mocked(itemService.create).mockResolvedValue({ id: 'item-1', sku: 'SKU-1', shortName: 'Aceite' } as never);
    const { container, onCreated } = renderModal({
      initialData: { name: 'Aceite', barcode: '00125', supplierCode: '00125', supplierId: 'supplier-1' },
    });
    await waitForCatalogs();

    await fillRequiredFields(container);
    fireEvent.click(screen.getByRole('button', { name: 'Crear Producto' }));

    await waitFor(() => {
      expect(itemService.create).toHaveBeenCalledWith(expect.objectContaining({
        sku: 'SKU-1',
        itemTypeId: 'type-1',
        categoryNodeId: 'cat-1',
        brandId: 'brand-1',
        defaultUomCode: 'UNIT',
        barcodes: [{ code: '00125', barcodeType: 'EAN13', isPrimary: true }],
        supplierCodes: [{ supplierId: 'supplier-1', code: '00125', isPrimary: true }],
      }));
      expect(onCreated).toHaveBeenCalledWith({ id: 'item-1', sku: 'SKU-1', shortName: 'Aceite' });
    });
  });

  it('error del backend se muestra en el campo correspondiente del formulario, sin cerrar el modal', async () => {
    const validationError = {
      isAxiosError: true,
      response: { status: 422, data: { data: { errors: { sku: ["Ya existe un ítem con SKU 'SKU-1'."] } } } },
    };
    vi.mocked(itemService.create).mockRejectedValue(validationError);
    const { container, onClose, onCreated } = renderModal({ initialData: { name: 'Aceite' } });
    await waitForCatalogs();

    await fillRequiredFields(container, { barcode: '00125' });
    fireEvent.click(screen.getByRole('button', { name: 'Crear Producto' }));

    await waitFor(() => expect(screen.getByText("Ya existe un ítem con SKU 'SKU-1'.")).toBeTruthy());
    expect(onClose).not.toHaveBeenCalled();
    expect(onCreated).not.toHaveBeenCalled();
  });
});
