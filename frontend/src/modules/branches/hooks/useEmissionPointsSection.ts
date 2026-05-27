import { useCallback, useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { emissionPointService, type EmissionPointDto } from '../api/emissionPointService';
import {
  emissionPointFormSchema,
  emptyEmissionPointForm,
  type EmissionPointFormValues,
} from '../schemas/emissionPointSchema';
import { usePermissionsUi } from '../../../access/usePermissionsUi';

export function useEmissionPointsSection(establishmentId: string) {
  const { canShow } = usePermissionsUi();
  const canCreate = canShow('settings.branches.create');
  const canUpdate = canShow('settings.branches.update');
  const canDelete = canShow('settings.branches.delete');

  const [items, setItems] = useState<EmissionPointDto[]>([]);
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
  } = useForm<EmissionPointFormValues>({
    resolver: zodResolver(emissionPointFormSchema),
    defaultValues: emptyEmissionPointForm(),
  });

  const fetchList = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      setItems(await emissionPointService.list(establishmentId));
    } catch {
      setError('Error al cargar los puntos de emisión.');
    } finally {
      setLoading(false);
    }
  }, [establishmentId]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const openCreateModal = () => {
    setEditingId(null);
    setSaveError('');
    reset(emptyEmissionPointForm());
    setModalOpen(true);
  };

  const openEditModal = (item: EmissionPointDto) => {
    setEditingId(item.id);
    setSaveError('');
    reset({ code: item.code, name: item.name ?? '', isDefault: item.isDefault });
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
        await emissionPointService.update(establishmentId, editingId, {
          id: editingId,
          name: form.name || null,
          isDefault: form.isDefault,
        });
      } else {
        await emissionPointService.create(establishmentId, {
          establishmentId,
          code: form.code.trim(),
          name: form.name || null,
          isDefault: form.isDefault,
        });
      }
      await fetchList();
      closeModal();
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        'Error al guardar el punto de emisión.';
      setSaveError(msg);
    } finally {
      setSaving(false);
    }
  });

  const toggleDisable = async (item: EmissionPointDto) => {
    setError('');
    try {
      if (item.isActive) {
        await emissionPointService.disable(establishmentId, item.id);
      } else {
        await emissionPointService.enable(establishmentId, item.id);
      }
      await fetchList();
    } catch {
      setError('Error al cambiar el estado del punto de emisión.');
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

export type EmissionPointsSectionContext = ReturnType<typeof useEmissionPointsSection>;
