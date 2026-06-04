import { useState } from 'react';
import { useAsync } from '../../../hooks/useAsync';
import { formatApiError } from '../../lib/formatApiError';
import { itemService, type CreateItemRequest, type UpdateItemRequest } from '../api/itemService';
import type { GetItemsParams } from '../api/itemService';
import type { ItemDetailDto, ItemDto } from '../../../types/items';

export function useItems(params: GetItemsParams = {}) {
  const itemsState = useAsync(() => itemService.getAll(params));

  const [createError, setCreateError] = useState<string | null>(null);
  const [creating, setCreating]       = useState(false);
  const [updateError, setUpdateError] = useState<string | null>(null);
  const [updating, setUpdating]       = useState(false);
  const [toggleError, setToggleError] = useState<string | null>(null);
  const [toggling, setToggling]       = useState(false);

  const createItem = async (payload: CreateItemRequest): Promise<ItemDto | null> => {
    setCreateError(null);
    setCreating(true);
    try {
      const created = await itemService.create(payload);
      itemsState.refetch();
      return created;
    } catch (err) {
      setCreateError(formatApiError(err));
      return null;
    } finally {
      setCreating(false);
    }
  };

  const updateItem = async (payload: UpdateItemRequest): Promise<ItemDto | null> => {
    setUpdateError(null);
    setUpdating(true);
    try {
      const updated = await itemService.update(payload);
      itemsState.refetch();
      return updated;
    } catch (err) {
      setUpdateError(formatApiError(err));
      return null;
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
      setToggleError(formatApiError(err));
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
    creating, createError, createItem,
    updating, updateError, updateItem,
    toggling, toggleError, toggleStatus,
  };
}

export function useItemDetail(id: string | null) {
  const state = useAsync(() => (id ? itemService.getById(id) : Promise.resolve(null)), [id]);
  return {
    item:    state.data as ItemDetailDto | null,
    loading: state.loading,
    error:   state.error,
    refetch: state.refetch,
  };
}
