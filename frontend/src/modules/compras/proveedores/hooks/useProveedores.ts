import { useState } from 'react';
import { useAsync } from '../../../../hooks/useAsync';
import { formatApiError } from '../../../lib/formatApiError';
import { proveedorService, type CreateProveedorRequest } from '../api/proveedorService';

export function useProveedores() {
  const listState = useAsync(() => proveedorService.getAll());

  const [saving, setSaving]       = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const createProveedor = async (payload: CreateProveedorRequest) => {
    setSaveError(null);
    setSaving(true);
    try {
      const created = await proveedorService.create(payload);
      listState.refetch();
      return created;
    } catch (err) {
      setSaveError(formatApiError(err));
      return null;
    } finally {
      setSaving(false);
    }
  };

  const updateProveedor = async (id: string, payload: CreateProveedorRequest) => {
    setSaveError(null);
    setSaving(true);
    try {
      const updated = await proveedorService.update(id, payload);
      listState.refetch();
      return updated;
    } catch (err) {
      setSaveError(formatApiError(err));
      return null;
    } finally {
      setSaving(false);
    }
  };

  const setProveedorStatus = async (id: string, status: 'activo' | 'pendiente' | 'inactivo') => {
    setSaveError(null);
    setSaving(true);
    try {
      const updated = await proveedorService.setStatus(id, status);
      listState.refetch();
      return updated;
    } catch (err) {
      setSaveError(formatApiError(err));
      return null;
    } finally {
      setSaving(false);
    }
  };

  return {
    proveedores: listState.data ?? [],
    loading:     listState.loading,
    error:       listState.error,
    saving,
    saveError,
    createProveedor,
    updateProveedor,
    setProveedorStatus,
  };
}
