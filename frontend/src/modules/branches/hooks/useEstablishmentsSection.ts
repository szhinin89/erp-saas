import { useCallback, useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { establishmentService, type EstablishmentDto } from '../api/establishmentService';
import {
  establishmentFormSchema,
  emptyEstablishmentForm,
  type EstablishmentFormValues,
} from '../schemas/establishmentSchema';
import { usePermissionsUi } from '../../../access/usePermissionsUi';

export function useEstablishmentsSection(branchId: string) {
  const { canShow } = usePermissionsUi();
  const canCreate = canShow('settings.branches.create');
  const canUpdate = canShow('settings.branches.update');
  const canDelete = canShow('settings.branches.delete');

  const [items, setItems] = useState<EstablishmentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const [modalOpen, setModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');

  const {
    register,
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<EstablishmentFormValues>({
    resolver: zodResolver(establishmentFormSchema),
    defaultValues: emptyEstablishmentForm(),
  });

  const fetchList = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      setItems(await establishmentService.list(branchId));
    } catch {
      setError('Error al cargar los establecimientos.');
    } finally {
      setLoading(false);
    }
  }, [branchId]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const openCreateModal = () => {
    setEditingId(null);
    setSaveError('');
    reset(emptyEstablishmentForm());
    setModalOpen(true);
  };

  const openEditModal = (item: EstablishmentDto) => {
    setEditingId(item.id);
    setSaveError('');
    reset({ code: item.code, name: item.name, address: item.address, phone: item.phone ?? '', isMain: item.isMain });
    setModalOpen(true);
  };

  const closeModal = () => {
    setModalOpen(false);
    setEditingId(null);
    setSaveError('');
  };

  const save = handleSubmit(async (form) => {
    setSaveError('');
    setSaving(true);
    try {
      if (editingId) {
        await establishmentService.update(branchId, editingId, {
          id: editingId,
          name: form.name.trim(),
          address: form.address.trim(),
          phone: form.phone || null,
          isMain: form.isMain,
        });
      } else {
        await establishmentService.create(branchId, {
          branchId,
          code: form.code.trim(),
          name: form.name.trim(),
          address: form.address.trim(),
          phone: form.phone || null,
          isMain: form.isMain,
        });
      }
      await fetchList();
      closeModal();
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        'Error al guardar el establecimiento.';
      setSaveError(msg);
    } finally {
      setSaving(false);
    }
  });

  const toggleDisable = async (item: EstablishmentDto) => {
    setError('');
    try {
      if (item.isActive) {
        await establishmentService.disable(branchId, item.id);
      } else {
        await establishmentService.enable(branchId, item.id);
      }
      await fetchList();
    } catch {
      setError('Error al cambiar el estado del establecimiento.');
    }
  };

  return {
    canCreate,
    canUpdate,
    canDelete,
    items,
    loading,
    error,
    modalOpen,
    editingId,
    saving,
    saveError,
    register,
    control,
    errors,
    openCreateModal,
    openEditModal,
    closeModal,
    save,
    toggleDisable,
    fetchList,
  };
}

export type EstablishmentsSectionContext = ReturnType<typeof useEstablishmentsSection>;
