import { useState } from 'react';
import { useAsync } from '../../../hooks/useAsync';
import { formatApiError } from '../../lib/formatApiError';
import { inventoryItemService, type RegisterLotRequest, type RegisterSerialRequest } from '../api/inventoryItemService';

export function useLots(itemId: string, warehouseId?: string) {
  const state = useAsync(
    () => inventoryItemService.getLots(itemId, warehouseId),
    [itemId, warehouseId]
  );

  const [registerError, setRegisterError] = useState<string | null>(null);
  const [registering, setRegistering]     = useState(false);

  const registerLot = async (request: RegisterLotRequest) => {
    setRegisterError(null);
    setRegistering(true);
    try {
      const lot = await inventoryItemService.registerLot(request);
      state.refetch();
      return lot;
    } catch (err) {
      setRegisterError(formatApiError(err));
      return null;
    } finally {
      setRegistering(false);
    }
  };

  return {
    lots: state.data ?? [],
    loading: state.loading,
    error: state.error,
    registering, registerError, registerLot,
  };
}

export function useSerials(itemId: string, warehouseId?: string) {
  const state = useAsync(
    () => inventoryItemService.getSerials(itemId, warehouseId),
    [itemId, warehouseId]
  );

  const [registerError, setRegisterError] = useState<string | null>(null);
  const [registering, setRegistering]     = useState(false);

  const registerSerials = async (request: RegisterSerialRequest) => {
    setRegisterError(null);
    setRegistering(true);
    try {
      const serials = await inventoryItemService.registerSerials(request);
      state.refetch();
      return serials;
    } catch (err) {
      setRegisterError(formatApiError(err));
      return null;
    } finally {
      setRegistering(false);
    }
  };

  return {
    serials: state.data ?? [],
    loading: state.loading,
    error: state.error,
    registering, registerError, registerSerials,
  };
}
