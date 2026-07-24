import { useCallback, useEffect, useState } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { ZHField, ZHToggle, ZHGrid, ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZhPhoneInput, ZhDateInput } from '../../../components/zh/inputs';
import { LoadingState } from '../../../components/PageShell';
import { branchService, type BranchDetailDto, type GeographyItemDto } from '../api/branchService';
import { branchFormSchema, type BranchFormValues } from '../schemas/branchSchema';
import { branchFormFromDto } from '../hooks/useBranchesPage';
import { applyServerErrors } from '../../lib/validationErrors';
import { message } from '../../../lib/messages';
import { usePermissionsUi } from '../../../access/usePermissionsUi';

interface Props {
  branchId: string;
  initialData: BranchDetailDto;
  onSaved: (updated: BranchDetailDto) => void;
}

export function BranchGeneralSection({ branchId, initialData, onSaved }: Props) {
  const { canShow } = usePermissionsUi();
  const canUpdate = canShow('settings.branches.update');

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');

  const [countries, setCountries] = useState<GeographyItemDto[]>([]);
  const [provinces, setProvinces] = useState<GeographyItemDto[]>([]);
  const [cantons, setCantons] = useState<GeographyItemDto[]>([]);
  const [parishes, setParishes] = useState<GeographyItemDto[]>([]);
  const [loadingProvinces, setLoadingProvinces] = useState(false);
  const [loadingCantons, setLoadingCantons] = useState(false);
  const [loadingParishes, setLoadingParishes] = useState(false);
  const [geoReady, setGeoReady] = useState(false);

  const {
    register,
    control,
    handleSubmit,
    reset,
    watch,
    setValue,
    setError: setFieldError,
    formState: { errors, isDirty },
  } = useForm<BranchFormValues>({
    resolver: zodResolver(branchFormSchema),
    defaultValues: branchFormFromDto(initialData),
  });
  const formWatch = watch();

  const loadProvinces = useCallback(async (countryId: string) => {
    if (!countryId) { setProvinces([]); return; }
    setLoadingProvinces(true);
    try { setProvinces(await branchService.provinces(countryId)); }
    catch { setProvinces([]); }
    finally { setLoadingProvinces(false); }
  }, []);

  const loadCantons = useCallback(async (provinceId: string) => {
    if (!provinceId) { setCantons([]); return; }
    setLoadingCantons(true);
    try { setCantons(await branchService.cantons(provinceId)); }
    catch { setCantons([]); }
    finally { setLoadingCantons(false); }
  }, []);

  const loadParishes = useCallback(async (cantonId: string) => {
    if (!cantonId) { setParishes([]); return; }
    setLoadingParishes(true);
    try { setParishes(await branchService.parishes(cantonId)); }
    catch { setParishes([]); }
    finally { setLoadingParishes(false); }
  }, []);

  useEffect(() => {
    void (async () => {
      const [ctrs] = await Promise.all([branchService.countries()]);
      setCountries(ctrs);
      if (initialData.countryId) await loadProvinces(initialData.countryId);
      if (initialData.provinceId) await loadCantons(initialData.provinceId);
      if (initialData.cantonId) await loadParishes(initialData.cantonId);
      setGeoReady(true);
    })();
  }, [initialData, loadProvinces, loadCantons, loadParishes]);

  const onCountryChange = async (countryId: string) => {
    setValue('provinceId', '');
    setValue('cantonId', '');
    setValue('parishId', '');
    setProvinces([]);
    setCantons([]);
    setParishes([]);
    await loadProvinces(countryId);
  };

  const onProvinceChange = async (provinceId: string) => {
    setValue('cantonId', '');
    setValue('parishId', '');
    setCantons([]);
    setParishes([]);
    await loadCantons(provinceId);
  };

  const onCantonChange = async (cantonId: string) => {
    setValue('parishId', '');
    setParishes([]);
    await loadParishes(cantonId);
  };

  const discard = () => {
    reset(branchFormFromDto(initialData));
    setSaveError('');
  };

  const onSubmit = handleSubmit(async (form) => {
    setSaveError('');
    setSaving(true);
    try {
      const payload = {
        name: form.name.trim(),
        address: form.address.trim(),
        description: form.description || null,
        reference: form.reference || null,
        postalCode: form.postalCode || null,
        phone: form.phone || null,
        secondaryPhone: form.secondaryPhone || null,
        email: form.email || null,
        website: form.website || null,
        managerName: form.managerName || null,
        managerPosition: form.managerPosition || null,
        managerEmail: form.managerEmail || null,
        managerPhone: form.managerPhone || null,
        countryId: form.countryId || null,
        provinceId: form.provinceId || null,
        cantonId: form.cantonId || null,
        parishId: form.parishId || null,
        latitude: form.latitude || null,
        longitude: form.longitude || null,
        openingDate: form.openingDate || null,
        internalNotes: form.internalNotes || null,
        isActive: form.isActive,
        isMainBranch: form.isMainBranch,
      };
      await branchService.update(branchId, { id: branchId, ...payload });
      const updated = await branchService.getById(branchId);
      reset(branchFormFromDto(updated));
      onSaved(updated);
      message.success('Sucursal actualizada correctamente.');
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setFieldError, (msg) => setSaveError(msg));
      if (!applied) setSaveError('Error al guardar la sucursal.');
    } finally {
      setSaving(false);
    }
  });

  if (!geoReady) {
    return <div className="pg-pad-40"><LoadingState /></div>;
  }

  return (
    <form onSubmit={(e) => { e.preventDefault(); void onSubmit(); }} noValidate>

      {saveError && (
        <ZHPageNotice variant="error" message="Error" detail={saveError} />
      )}

      {/* Identificación */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">info</span>
            <span className="pg-section-label">Identificación</span>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHGrid cols={2}>
            <ZHField label="Nombre de la Sucursal" required error={errors.name?.message}>
              <input
                className="zh-input"
                placeholder="Ej: Sucursal Norte Central"
                disabled={saving || !canUpdate}
                {...register('name')}
              />
            </ZHField>
            <ZHField label="Código">
              <input className="zh-input mono br-code-readonly" readOnly value={initialData.code ?? '—'} />
            </ZHField>
            <ZHField label="Descripción" error={errors.description?.message}>
              <input
                className="zh-input"
                placeholder="Descripción breve"
                disabled={saving || !canUpdate}
                {...register('description')}
              />
            </ZHField>
          </ZHGrid>
          <Controller
            name="isMainBranch"
            control={control}
            render={({ field }) => (
              <ZHToggle
                label="Sucursal Principal"
                description="Solo puede haber una sucursal principal por empresa."
                value={field.value}
                onChange={field.onChange}
                disabled={saving || !canUpdate}
              />
            )}
          />
        </div>
      </div>

      {/* Dirección */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">location_on</span>
            <span className="pg-section-label">Dirección</span>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHGrid cols={2}>
            <ZHField label="País">
              <select
                className="zh-input"
                disabled={saving || !canUpdate || countries.length === 0}
                {...register('countryId', {
                  onChange: async (e) => { await onCountryChange(e.target.value); },
                })}
              >
                <option value="">— seleccionar —</option>
                {countries.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </ZHField>
            <ZHField label={loadingProvinces ? 'Provincia (cargando…)' : 'Provincia'}>
              <select
                className="zh-input"
                disabled={saving || !canUpdate || !formWatch.countryId || loadingProvinces}
                {...register('provinceId', {
                  onChange: async (e) => { await onProvinceChange(e.target.value); },
                })}
              >
                <option value="">— seleccionar —</option>
                {provinces.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </ZHField>
            <ZHField label={loadingCantons ? 'Cantón (cargando…)' : 'Cantón'}>
              <select
                className="zh-input"
                disabled={saving || !canUpdate || !formWatch.provinceId || loadingCantons}
                {...register('cantonId', {
                  onChange: async (e) => { await onCantonChange(e.target.value); },
                })}
              >
                <option value="">— seleccionar —</option>
                {cantons.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </ZHField>
            <ZHField label={loadingParishes ? 'Parroquia (cargando…)' : 'Parroquia'}>
              <select
                className="zh-input"
                disabled={saving || !canUpdate || !formWatch.cantonId || loadingParishes}
                {...register('parishId')}
              >
                <option value="">— seleccionar —</option>
                {parishes.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </ZHField>
          </ZHGrid>
          <ZHField label="Dirección" required error={errors.address?.message}>
            <input
              className="zh-input"
              placeholder="Calle, número, colonia..."
              disabled={saving || !canUpdate}
              {...register('address')}
            />
          </ZHField>
          <ZHGrid cols={2}>
            <ZHField label="Referencia">
              <input className="zh-input" disabled={saving || !canUpdate} {...register('reference')} />
            </ZHField>
            <ZHField label="Código postal" error={errors.postalCode?.message}>
              <input className="zh-input" disabled={saving || !canUpdate} {...register('postalCode')} />
            </ZHField>
            <ZHField label="Latitud" error={errors.latitude?.message}>
              <input className="zh-input mono" disabled={saving || !canUpdate} {...register('latitude')} />
            </ZHField>
            <ZHField label="Longitud" error={errors.longitude?.message}>
              <input className="zh-input mono" disabled={saving || !canUpdate} {...register('longitude')} />
            </ZHField>
          </ZHGrid>
        </div>
      </div>

      {/* Contacto */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">contact_phone</span>
            <span className="pg-section-label">Contacto</span>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHGrid cols={2}>
            <ZHField label="Teléfono principal" error={errors.phone?.message}>
              <Controller
                name="phone"
                control={control}
                render={({ field }) => <ZhPhoneInput {...field} disabled={saving || !canUpdate} />}
              />
            </ZHField>
            <ZHField label="Teléfono secundario" error={errors.secondaryPhone?.message}>
              <Controller
                name="secondaryPhone"
                control={control}
                render={({ field }) => <ZhPhoneInput {...field} disabled={saving || !canUpdate} />}
              />
            </ZHField>
            <ZHField label="Correo Electrónico" error={errors.email?.message}>
              <input className="zh-input" type="email" disabled={saving || !canUpdate} {...register('email')} />
            </ZHField>
            <ZHField label="Sitio web" error={errors.website?.message}>
              <input className="zh-input" disabled={saving || !canUpdate} {...register('website')} />
            </ZHField>
          </ZHGrid>
        </div>
      </div>

      {/* Responsable */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">badge</span>
            <span className="pg-section-label">Responsable</span>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHGrid cols={2}>
            <ZHField label="Nombre completo" error={errors.managerName?.message}>
              <input className="zh-input" disabled={saving || !canUpdate} {...register('managerName')} />
            </ZHField>
            <ZHField label="Cargo" error={errors.managerPosition?.message}>
              <input className="zh-input" disabled={saving || !canUpdate} {...register('managerPosition')} />
            </ZHField>
            <ZHField label="Correo" error={errors.managerEmail?.message}>
              <input className="zh-input" type="email" disabled={saving || !canUpdate} {...register('managerEmail')} />
            </ZHField>
            <ZHField label="Teléfono" error={errors.managerPhone?.message}>
              <Controller
                name="managerPhone"
                control={control}
                render={({ field }) => <ZhPhoneInput {...field} disabled={saving || !canUpdate} />}
              />
            </ZHField>
          </ZHGrid>
        </div>
      </div>

      {/* Operación */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">settings_input_component</span>
            <span className="pg-section-label">Operación</span>
          </div>
        </div>
        <div className="pg-section-body">
          <Controller
            name="isActive"
            control={control}
            render={({ field }) => (
              <ZHToggle
                label="Sucursal Activa"
                description="La sucursal aparece en listados y puede operar."
                value={field.value}
                onChange={field.onChange}
                disabled={saving || !canUpdate}
              />
            )}
          />
          <ZHGrid cols={2}>
            <ZHField label="Fecha de apertura" error={errors.openingDate?.message}>
              <ZhDateInput disabled={saving || !canUpdate} {...register('openingDate')} />
            </ZHField>
          </ZHGrid>
          <ZHField label="Notas internas" error={errors.internalNotes?.message}>
            <textarea
              className="zh-input"
              rows={3}
              maxLength={1000}
              disabled={saving || !canUpdate}
              {...register('internalNotes')}
            />
          </ZHField>
        </div>
      </div>

      {canUpdate && (
        <div className="pg-actions-bar">
          <div className="pg-actions-buttons">
            <ZHBtn variant="ghost" type="button" onClick={discard} disabled={saving || !isDirty}>
              Descartar cambios
            </ZHBtn>
            <ZHBtn variant="primary" type="submit" disabled={saving}>
              {saving ? 'Guardando…' : 'Guardar cambios'}
            </ZHBtn>
          </div>
        </div>
      )}
    </form>
  );
}
