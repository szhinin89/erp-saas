import { useState } from 'react';
import { useAsync } from '../../../hooks/useAsync';
import { formatApiError } from '../../lib/formatApiError';
import { customerService, type CreateCustomerRequest } from '../api/customerService';

export function useCustomers() {
  const listState = useAsync(() => customerService.getAll());
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const createCustomer = async (payload: CreateCustomerRequest) => {
    setCreateError(null);
    setCreating(true);
    try {
      const created = await customerService.create(payload);
      listState.refetch();
      return created;
    } catch (error) {
      setCreateError(formatApiError(error));
      return null;
    } finally {
      setCreating(false);
    }
  };

  const updateCustomer = async (id: string, payload: CreateCustomerRequest) => {
    setCreateError(null);
    setCreating(true);
    try {
      const updated = await customerService.update(id, payload);
      listState.refetch();
      return updated;
    } catch (error) {
      setCreateError(formatApiError(error));
      return null;
    } finally {
      setCreating(false);
    }
  };

  const toggleCustomerStatus = async (id: string, isActive: boolean) => {
    setCreateError(null);
    setCreating(true);
    try {
      const updated = await customerService.setActive(id, isActive);
      listState.refetch();
      return updated;
    } catch (error) {
      setCreateError(formatApiError(error));
      return null;
    } finally {
      setCreating(false);
    }
  };

  return {
    customers: listState.data ?? [],
    loading: listState.loading,
    error: listState.error,
    creating,
    createError,
    createCustomer,
    updateCustomer,
    toggleCustomerStatus,
  };
}
