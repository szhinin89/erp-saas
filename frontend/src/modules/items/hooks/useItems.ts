import { useState } from 'react';
import { useAsync } from '../../../hooks/useAsync';
import { formatApiRequestError } from '../../lib/apiError';
import { itemService, type CreateItemRequest, type UpdateItemRequest } from '../api/itemService';
import type { GetItemsParams } from '../api/itemService';
import type { ItemDto } from '../../../types/items';

export function useItems(params: GetItemsParams = {}) {
  const itemsState = useAsync(() => itemService.getAll(params));

  const [creating, setCreating] = useState(false);
  const [updating, setUpdating] = useState(false);
  const [toggleError, setToggleError] = useState<string | null>(null);
  const [toggling, setToggling]       = useState(false);

  const createItem = async (payload: CreateItemRequest): Promise<ItemDto> => {
    setCreating(true);
    try {
      const created = await itemService.create(payload);
      itemsState.refetch();
      return created;
    } finally {
      setCreating(false);
    }
  };

  const updateItem = async (payload: UpdateItemRequest): Promise<ItemDto> => {
    setUpdating(true);
    try {
      const updated = await itemService.update(payload);
      itemsState.refetch();
      return updated;
    } finally {
      setUpdating(false);
    }
  };

  const toggleStatus = async (id: string, enable: boolean): Promise<boolean> => {
    setToggleError(null);
    setToggling(true);
    try {
      const fn = enable ? itemService.enable : itemService.disable;
      await fn(id);
      itemsState.refetch();
      return true;
    } catch (err) {
      setToggleError(formatApiRequestError(err, { generic: 'Error al actualizar el estado del ítem.' }));
      return false;
    } finally {
      setToggling(false);
    }
  };

  return {
    itemsPage:    itemsState.data,
    items:        itemsState.data?.items ?? [],
    totalCount:   itemsState.data?.totalCount ?? 0,
    loading:      itemsState.loading,
    error:        itemsState.error,
    refetch:      itemsState.refetch,
    creating, createItem,
    updating, updateItem,
    toggling, toggleError, toggleStatus,
  };
}
