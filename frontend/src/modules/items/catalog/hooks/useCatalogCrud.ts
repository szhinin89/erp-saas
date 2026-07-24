import { useCallback, useEffect, useState } from 'react';
import { useForm, type DefaultValues, type FieldValues, type Path } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type { ZodType, ZodTypeDef } from 'zod';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import { applyServerErrors } from '../../../lib/validationErrors';

interface CatalogCrudService<TDto, TCreate, TUpdate, TListArgs extends unknown[] = []> {
  list:    (...args: TListArgs) => Promise<TDto[]>;
  create:  (payload: TCreate) => Promise<TDto>;
  update:  (id: string, payload: TUpdate) => Promise<TDto>;
  enable:  (id: string) => Promise<boolean>;
  disable: (id: string) => Promise<boolean>;
}

interface CatalogCrudOptions<
  TDto,
  TForm extends FieldValues,
  TCreate,
  TUpdate,
  TListArgs extends unknown[] = [],
  // Schemas with `.default(...)` fields have a narrower z.output than z.input (defaulted
  // fields are optional on input). `TForm` reflects the runtime/output shape used across
  // register/control/handleSubmit — `TSchemaInput` lets the *schema* prop stay compatible
  // with the zod schema's actual (wider) input type without leaking that laxity into TForm.
  TSchemaInput extends FieldValues = TForm,
> {
  service: CatalogCrudService<TDto, TCreate, TUpdate, TListArgs>;
  schema: ZodType<TForm, ZodTypeDef, TSchemaInput>;
  emptyForm: () => TForm;
  toCreatePayload: (form: TForm) => TCreate;
  toUpdatePayload: (id: string, form: TForm) => TUpdate;
  toFormValues: (dto: TDto) => TForm;
  getId: (dto: TDto) => string;
  permissionPrefix: string;
  listArgs?: () => TListArgs;
}

export function useCatalogCrud<
  TDto extends { isActive: boolean; name: string },
  TForm extends FieldValues,
  TCreate = unknown,
  TUpdate = unknown,
  TListArgs extends unknown[] = [],
  TSchemaInput extends FieldValues = TForm,
>(opts: CatalogCrudOptions<TDto, TForm, TCreate, TUpdate, TListArgs, TSchemaInput>) {
  const { service, schema, emptyForm, toCreatePayload, toUpdatePayload, toFormValues, getId, permissionPrefix, listArgs } = opts;

  const { canShow } = usePermissionsUi();
  const canView   = canShow(`${permissionPrefix}.view`) || canShow(permissionPrefix);
  const canCreate = canShow(`${permissionPrefix}.create`) || canShow(permissionPrefix);
  const canUpdate = canShow(`${permissionPrefix}.update`) || canShow(permissionPrefix);
  const canDelete = canShow(`${permissionPrefix}.delete`) || canShow(permissionPrefix);

  const [items, setItems]     = useState<TDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError]     = useState('');
  const [search, setSearch]   = useState('');

  const [modalOpen, setModalOpen]   = useState(false);
  const [editingId, setEditingId]   = useState<string | null>(null);
  const [saving, setSaving]         = useState(false);
  const [saveError, setSaveError]   = useState('');

  const form = useForm<TForm>({
    resolver: zodResolver(schema),
    defaultValues: emptyForm() as DefaultValues<TForm>,
  });

  const { register, control, handleSubmit, reset, setError: setFieldError, formState: { errors } } = form;

  const fetchList = useCallback(async () => {
    setError('');
    setLoading(true);
    try {
      const args = listArgs ? listArgs() : ([] as unknown as TListArgs);
      setItems(await service.list(...args));
    } catch {
      setError('Error al cargar los datos.');
    } finally {
      setLoading(false);
    }
  }, [service, listArgs]);

  useEffect(() => { void fetchList(); }, [fetchList]);

  const filtered = search.trim()
    ? items.filter((i) => i.name.toLowerCase().includes(search.toLowerCase()))
    : items;

  const totals = {
    total:    items.length,
    active:   items.filter((i) => i.isActive).length,
    inactive: items.filter((i) => !i.isActive).length,
  };

  const openCreateModal = () => {
    setEditingId(null);
    setSaveError('');
    reset(emptyForm() as DefaultValues<TForm>);
    setModalOpen(true);
  };

  const openEditModal = (item: TDto) => {
    setEditingId(getId(item));
    setSaveError('');
    reset(toFormValues(item) as DefaultValues<TForm>);
    setModalOpen(true);
  };

  const closeModal = () => {
    setModalOpen(false);
    setEditingId(null);
    setSaveError('');
  };

  const save = handleSubmit(async (formValues) => {
    setSaveError('');
    setSaving(true);
    try {
      if (editingId) {
        await service.update(editingId, toUpdatePayload(editingId, formValues));
      } else {
        await service.create(toCreatePayload(formValues));
      }
      await fetchList();
      closeModal();
    } catch (err: unknown) {
      const applied = applyServerErrors(
        err,
        (field, errObj) => setFieldError(field as Path<TForm>, errObj),
        (msg) => setSaveError(msg),
      );
      if (!applied) setSaveError('Error al guardar.');
    } finally {
      setSaving(false);
    }
  });

  const toggleDisable = async (item: TDto) => {
    setError('');
    try {
      const id = getId(item);
      if (item.isActive) {
        await service.disable(id);
      } else {
        await service.enable(id);
      }
      await fetchList();
    } catch {
      setError('Error al cambiar el estado.');
    }
  };

  return {
    canView, canCreate, canUpdate, canDelete,
    items, filtered, loading, error, search, setSearch, totals, fetchList,
    modalOpen, editingId, saving, saveError,
    register, control, errors,
    openCreateModal, openEditModal, closeModal, save, toggleDisable,
  };
}

export type CatalogCrudContext<
  TDto extends { isActive: boolean; name: string } = { isActive: boolean; name: string },
  TForm extends FieldValues = FieldValues,
> = ReturnType<typeof useCatalogCrud<TDto, TForm>>;
