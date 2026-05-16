import { useState } from 'react';
import { useAsync } from '../../../../hooks/useAsync';
import { formatApiError } from '../../../lib/formatApiError';
import { supplierService, type CreateSupplierRequest } from '../api/supplierService';

export function useSuppliers() {
  const listState = useAsync(() => supplierService.getAll());

  const [saving,    setSaving]    = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  const createSupplier = async (payload: CreateSupplierRequest) => {
    setSaveError(null);
    setSaving(true);
    try {
      const created = await supplierService.create(payload);
      listState.refetch();
      return created;
    } catch (err) {
      setSaveError(formatApiError(err));
      return null;
    } finally {
      setSaving(false);
    }
  };

  const updateSupplier = async (id: string, payload: CreateSupplierRequest) => {
    setSaveError(null);
    setSaving(true);
    try {
      const updated = await supplierService.update(id, payload);
      listState.refetch();
      return updated;
    } catch (err) {
      setSaveError(formatApiError(err));
      return null;
    } finally {
      setSaving(false);
    }
  };

  const setSupplierStatus = async (id: string, status: 'active' | 'pending' | 'inactive') => {
    setSaveError(null);
    setSaving(true);
    try {
      const updated = await supplierService.setStatus(id, status);
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
    suppliers:       listState.data ?? [],
    loading:         listState.loading,
    error:           listState.error,
    saving,
    saveError,
    createSupplier,
    updateSupplier,
    setSupplierStatus,
  };
}
