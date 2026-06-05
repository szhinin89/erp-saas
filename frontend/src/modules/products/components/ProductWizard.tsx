import { useCallback, useEffect, useMemo, useState } from 'react';
import { useFieldArray, useForm, type Resolver } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import type { Product } from '../../../types/product';
import type { ProductCatalogs } from '../hooks/useProducts';
import {
  defaultProductValues,
  productSchema,
  toOptionalGuid,
  type ProductFormValues,
} from '../schemas/productSchema';
import { useI18n } from '../../../i18n/i18n';

// ─────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────

interface Props {
  catalogs: ProductCatalogs | null | undefined;
  catalogsLoading: boolean;
  submitting: boolean;
  editingProduct: Product | null;
  onSubmit: (values: ProductFormValues) => Promise<boolean>;
  onCancel: () => void;
}

type StepId = 1 | 2 | 3 | 4;

const STEPS: { id: StepId; icon: string; labelKey: string; labelFb: string }[] = [
  { id: 1, icon: 'description', labelKey: 'products.wizard.step.basicData',      labelFb: 'Datos Básicos' },
  { id: 2, icon: 'category',    labelKey: 'products.wizard.step.classification', labelFb: 'Clasificación' },
  { id: 3, icon: 'settings',    labelKey: 'products.wizard.step.config',         labelFb: 'Impuestos y Config.' },
  { id: 4, icon: 'check_circle',labelKey: 'products.wizard.step.review',         labelFb: 'Revisar y Guardar' },
];

// Step 1 fields for validation gating
const STEP1_FIELDS: (keyof ProductFormValues)[] = [
  'saleCode', 'shortName', 'description',
];
const STEP2_FIELDS: (keyof ProductFormValues)[] = [
  'lineId', 'categoryId', 'subcategoryId',
  'unitOfMeasureId', 'brandId', 'productTypeId', 'tariffId',
];
const STEP3_FIELDS: (keyof ProductFormValues)[] = [];

const DRAFT_KEY = 'erp.products.draft';
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

// ─────────────────────────────────────────────
// Draft helpers
// ─────────────────────────────────────────────
interface DraftSnapshot {
  saleCode: string;
  shortName: string;
  description: string;
  savedAt: string;
}

function saveDraft(vals: ProductFormValues) {
  const snap: DraftSnapshot = {
    saleCode: vals.saleCode,
    shortName: vals.shortName,
    description: vals.description,
    savedAt: new Date().toISOString(),
  };
  localStorage.setItem(DRAFT_KEY, JSON.stringify(snap));
}

function loadDraft(): DraftSnapshot | null {
  try {
    const raw = localStorage.getItem(DRAFT_KEY);
    return raw ? (JSON.parse(raw) as DraftSnapshot) : null;
  } catch {
    return null;
  }
}

function clearDraft() {
  localStorage.removeItem(DRAFT_KEY);
}

// ─────────────────────────────────────────────
// Small field components
// ─────────────────────────────────────────────

function FieldWrap({
  label,
  required,
  error,
  children,
  hint,
  t,
}: {
  label: string;
  required?: boolean;
  error?: string;
  children: React.ReactNode;
  hint?: string;
  t?: (key: string) => string;
}) {
  const resolvedError = error && t ? t(error) : error;
  return (
    <div className={`prd-wiz-field ${resolvedError ? 'prd-wiz-field--error' : ''}`}>
      <label className="prd-wiz-label">
        {label}
        {required && <span className="prd-wiz-required" aria-hidden>*</span>}
      </label>
      {children}
      {resolvedError && <span className="prd-wiz-error-msg" role="alert">{resolvedError}</span>}
      {!resolvedError && hint && <span className="prd-wiz-hint">{hint}</span>}
    </div>
  );
}

function OptionCard({
  checked,
  onChange,
  icon,
  title,
  disabled,
  id,
}: {
  checked: boolean;
  onChange: (v: boolean) => void;
  icon: string;
  title: string;
  disabled?: boolean;
  id: string;
}) {
  return (
    <label
      htmlFor={id}
      className={`prd-opt-card ${checked ? 'prd-opt-card--on' : ''} ${disabled ? 'prd-opt-card--disabled' : ''}`}
    >
      <input
        id={id}
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        disabled={disabled}
        className="prd-opt-card__input"
        aria-checked={checked}
      />
      <span className="material-symbols-outlined prd-opt-card__icon">{icon}</span>
      <span className="prd-opt-card__title">{title}</span>
    </label>
  );
}

// ─────────────────────────────────────────────
// Main wizard
// ─────────────────────────────────────────────

export function ProductWizard({
  catalogs,
  catalogsLoading,
  submitting,
  editingProduct,
  onSubmit,
  onCancel,
}: Props) {
  const { t } = useI18n();
  const [step, setStep]         = useState<StepId>(1);
  const [completed, setCompleted] = useState<Set<StepId>>(new Set());
  const [draft, setDraft]       = useState<DraftSnapshot | null>(null);

  const {
    register,
    handleSubmit,
    control,
    watch,
    setValue,
    trigger,
    reset,
    formState: { errors },
  } = useForm<ProductFormValues>({
    resolver: zodResolver(productSchema) as Resolver<ProductFormValues>,
    defaultValues: defaultProductValues,
    mode: 'onChange',
  });

  const { fields: barcodeFields, append, remove } = useFieldArray({
    control,
    name: 'barcodes',
  });

  const barcodeTypes = [
    { value: 1, label: t('products.form.barcodeType.ean13', 'EAN-13') },
    { value: 2, label: t('products.form.barcodeType.ean8',  'EAN-8') },
    { value: 3, label: t('products.form.barcodeType.qr',    'QR') },
    { value: 4, label: t('products.form.barcodeType.code128','Code 128') },
    { value: 5, label: t('products.form.barcodeType.internal','Interno') },
    { value: 99, label: t('products.form.barcodeType.other', 'Otro') },
  ];

  const selectedLineId     = watch('lineId');
  const selectedCategoryId = watch('categoryId');
  const allValues          = watch();

  // Cascade resets
  useEffect(() => {
    setValue('categoryId',    EMPTY_GUID, { shouldValidate: true });
    setValue('subcategoryId', EMPTY_GUID, { shouldValidate: true });
  }, [selectedLineId, setValue]);

  useEffect(() => {
    setValue('subcategoryId', EMPTY_GUID, { shouldValidate: true });
  }, [selectedCategoryId, setValue]);

  const filteredCategories = useMemo(() =>
    (catalogs?.categories ?? []).filter((c) =>
      'lineId' in c ? c.lineId === selectedLineId : true
    ), [catalogs, selectedLineId]);

  const filteredSubcategories = useMemo(() =>
    (catalogs?.subcategories ?? []).filter((c) =>
      'categoryId' in c ? c.categoryId === selectedCategoryId : true
    ), [catalogs, selectedCategoryId]);

  // Load editing product or draft
  useEffect(() => {
    if (editingProduct) {
      clearDraft();
      setDraft(null);
      (Object.keys(defaultProductValues) as (keyof ProductFormValues)[]).forEach((k) => {
        const v = editingProduct[k as keyof Product];
        if (v !== undefined) setValue(k, v as ProductFormValues[typeof k]);
      });
      if (editingProduct.barcodes?.length) {
        editingProduct.barcodes.forEach((b) => append({ code: b.code, type: b.type }));
      }
      setStep(1);
      setCompleted(new Set());
      return;
    }
    const d = loadDraft();
    if (d) setDraft(d);
  }, [editingProduct, append, setValue]);

  const applyDraft = useCallback(() => {
    if (!draft) return;
    setValue('saleCode',    draft.saleCode);
    setValue('shortName',   draft.shortName);
    setValue('description', draft.description);
    setDraft(null);
  }, [draft, setValue]);

  const discardDraft = useCallback(() => {
    clearDraft();
    setDraft(null);
  }, []);

  // ── Navigation ──
  const STEP_FIELDS: Record<StepId, (keyof ProductFormValues)[]> = {
    1: STEP1_FIELDS,
    2: STEP2_FIELDS,
    3: STEP3_FIELDS,
    4: [],
  };

  const goNext = useCallback(async () => {
    const fields = STEP_FIELDS[step];
    const ok = fields.length === 0 || await trigger(fields);
    if (!ok) return;
    setCompleted((prev) => new Set([...prev, step]));
    setStep((s) => Math.min(s + 1, 4) as StepId);
  }, [step, trigger]);

  const goPrev = useCallback(() => {
    setStep((s) => Math.max(s - 1, 1) as StepId);
  }, []);

  const goToStep = useCallback((target: StepId) => {
    if (target < step || completed.has(target) || target === step) {
      setStep(target);
    }
  }, [step, completed]);

  const handleSaveDraft = useCallback(() => {
    saveDraft(allValues);
  }, [allValues]);

  const handleFormSubmit = useCallback(async (values: ProductFormValues) => {
    const ok = await onSubmit(values);
    if (ok) {
      clearDraft();
      reset(defaultProductValues);
      setStep(1);
      setCompleted(new Set());
    }
  }, [onSubmit, reset]);

  // ── Summary helpers ──
  const labelFor = useCallback((list: { id: string; name: string }[], id: string) =>
    list.find((x) => x.id === id)?.name ?? '—', []);

  const summaryRows = useMemo(() => [
    [t('products.wizard.summary.sku','SKU'),          allValues.saleCode        || ''],
    [t('products.wizard.summary.shortName','Nombre corto'), allValues.shortName        || ''],
    [t('products.wizard.summary.description','Descripción'),  allValues.description      || ''],
    [t('products.wizard.summary.line','Línea'),        labelFor(catalogs?.lines ?? [], allValues.lineId)],
    [t('products.wizard.summary.category','Categoría'),    labelFor(catalogs?.categories ?? [], allValues.categoryId)],
    [t('products.wizard.summary.subcategory','Subcategoría'), labelFor(catalogs?.subcategories ?? [], allValues.subcategoryId)],
    [t('products.wizard.summary.unit','Unidad'),       labelFor(catalogs?.units ?? [], allValues.unitOfMeasureId)],
    [t('products.wizard.summary.brand','Marca'),        labelFor(catalogs?.brands ?? [], allValues.brandId)],
    [t('products.wizard.summary.type','Tipo'),         labelFor(catalogs?.productTypes ?? [], allValues.productTypeId)],
    [t('products.wizard.summary.tariff','Arancel'),      labelFor(catalogs?.tariffs ?? [], allValues.tariffId)],
    [t('products.wizard.summary.vatSale','IVA Ventas'),   allValues.appliesVatOnSale ? labelFor(catalogs?.taxRates ?? [], allValues.saleTaxId) : t('products.wizard.summary.notApply','No aplica')],
    [t('products.wizard.summary.vatPurchase','IVA Compras'),  allValues.appliesVatOnPurchase ? labelFor(catalogs?.taxRates ?? [], allValues.purchaseTaxId) : t('products.wizard.summary.notApply','No aplica')],
    [t('products.wizard.summary.isService','Es servicio'),  allValues.isService ? t('products.wizard.summary.yes','Sí') : t('products.wizard.summary.no','No')],
    [t('products.wizard.summary.tracksStock','Controla stock'), allValues.tracksStock ? t('products.wizard.summary.yes','Sí') : t('products.wizard.summary.no','No')],
  ], [allValues, catalogs, labelFor]);

  // ─── Render steps ────────────────────────────────────────
  return (
    <div className="prd-wizard prd-fadein">

      {/* Draft banner */}
      {draft && !editingProduct && (
        <div className="prd-draft-banner" role="status">
          <span className="material-symbols-outlined" style={{ fontSize: 18 }}>save</span>
          <span>{t('products.wizard.draft.banner','Tienes un borrador guardado del')} <strong>{new Date(draft.savedAt).toLocaleString()}</strong></span>
          <div className="prd-draft-banner__actions">
            <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" onClick={applyDraft}>{t('products.wizard.draft.recover','Recuperar')}</button>
            <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" onClick={discardDraft}>{t('products.wizard.draft.discard','Descartar')}</button>
          </div>
        </div>
      )}

      {/* Progress bar */}
      <div className="prd-wiz-progress" role="tablist" aria-label="Pasos del formulario">
        {STEPS.map((s, i) => {
          const isDone    = completed.has(s.id);
          const isCurrent = s.id === step;
          const canClick  = isDone || s.id < step;
          return (
            <button
              key={s.id}
              type="button"
              role="tab"
              aria-selected={isCurrent}
              className={[
                'prd-wiz-step',
                isDone    ? 'prd-wiz-step--done'    : '',
                isCurrent ? 'prd-wiz-step--current' : '',
              ].join(' ')}
              onClick={() => canClick && goToStep(s.id)}
              disabled={!canClick && !isCurrent}
              aria-disabled={!canClick && !isCurrent}
            >
              <span className="prd-wiz-step__num">
                {isDone ? (
                  <span className="material-symbols-outlined" style={{ fontSize: 14 }}>check</span>
                ) : (
                  s.id
                )}
              </span>
              <span className="prd-wiz-step__label">{t(s.labelKey, s.labelFb)}</span>
              {i < STEPS.length - 1 && <span className="prd-wiz-step__line" aria-hidden />}
            </button>
          );
        })}
      </div>

      {/* Step body */}
      <form
        onSubmit={handleSubmit(handleFormSubmit)}
        noValidate
        className="prd-wiz-body"
      >

        {/* ── STEP 1: Datos Básicos ─────────────────────── */}
        {step === 1 && (
          <div className="prd-wiz-panel prd-fadein">
            <div className="prd-wiz-panel__head">
              <span className="prd-wiz-panel__icon prd-wiz-panel__icon--blue material-symbols-outlined">description</span>
              <div>
                <h3 className="prd-wiz-panel__title">{t('products.wizard.basicData.title','📋 Datos Básicos')}</h3>
                <p className="prd-wiz-panel__desc">{t('products.wizard.basicData.desc','Identifica el producto en el catálogo y facturas.')}</p>
              </div>
            </div>

            <div className="prd-wiz-grid prd-wiz-grid--2">
              <FieldWrap t={t}
                label={t('products.wizard.field.sku','SKU / Código de venta')}
                required
                error={errors.saleCode?.message}
              >
                <div className="prd-wiz-input-wrap">
                  <input
                    {...register('saleCode')}
                    className={`zh-input prd-wiz-input ${errors.saleCode ? 'prd-wiz-input--error' : ''}`}
                    placeholder="Ej: PROD-001"
                    maxLength={50}
                    aria-required="true"
                    aria-invalid={!!errors.saleCode}
                  />
                  <span className={`prd-wiz-input__state material-symbols-outlined ${errors.saleCode ? 'prd-wiz-input__state--error' : (allValues.saleCode ? 'prd-wiz-input__state--ok' : '')}`}>
                    {errors.saleCode ? 'close' : (allValues.saleCode ? 'check' : '')}
                  </span>
                </div>
              </FieldWrap>

              <FieldWrap t={t}
                label={t('products.wizard.field.shortName','Nombre corto')}
                required
                error={errors.shortName?.message}
              >
                <div className="prd-wiz-input-wrap">
                  <input
                    {...register('shortName')}
                    className={`zh-input prd-wiz-input ${errors.shortName ? 'prd-wiz-input--error' : ''}`}
                    placeholder="Ej: Monitor Samsung 27"
                    maxLength={50}
                    aria-required="true"
                    aria-invalid={!!errors.shortName}
                  />
                  <span className={`prd-wiz-input__state material-symbols-outlined ${errors.shortName ? 'prd-wiz-input__state--error' : (allValues.shortName ? 'prd-wiz-input__state--ok' : '')}`}>
                    {errors.shortName ? 'close' : (allValues.shortName ? 'check' : '')}
                  </span>
                </div>
              </FieldWrap>

              <FieldWrap t={t}
                label={t('products.wizard.field.purchaseCode','Código de compra')}
                error={errors.purchaseCode?.message}
              >
                <input
                  {...register('purchaseCode')}
                  className="zh-input prd-wiz-input"
                  placeholder="Código del proveedor (opcional)"
                  maxLength={50}
                />
              </FieldWrap>

              <FieldWrap t={t}
                label={t('products.wizard.field.description','Descripción')}
                required
                error={errors.description?.message}
              >
                <div className="prd-wiz-input-wrap">
                  <input
                    {...register('description')}
                    className={`zh-input prd-wiz-input ${errors.description ? 'prd-wiz-input--error' : ''}`}
                    placeholder="Descripción para facturas"
                    maxLength={254}
                    aria-required="true"
                    aria-invalid={!!errors.description}
                  />
                  <span className={`prd-wiz-input__state material-symbols-outlined ${errors.description ? 'prd-wiz-input__state--error' : (allValues.description ? 'prd-wiz-input__state--ok' : '')}`}>
                    {errors.description ? 'close' : (allValues.description ? 'check' : '')}
                  </span>
                </div>
              </FieldWrap>
            </div>

            <FieldWrap t={t} label={t('products.wizard.field.observations','notes')} error={errors.observations?.message}>
              <textarea
                {...register('observations')}
                className="zh-input prd-wiz-input prd-wiz-textarea"
                placeholder="notes adicionales (opcional)"
                rows={2}
                maxLength={500}
              />
            </FieldWrap>

            {/* Barcodes */}
            <div className="prd-barcodes">
              <div className="prd-barcodes__header">
                <span className="prd-barcodes__title">
                  <span className="material-symbols-outlined" style={{ fontSize: 16 }}>qr_code</span>
                  Códigos de barras
                </span>
                <button
                  type="button"
                  className="zh-btn zh-btn--ghost zh-btn--sm"
                  onClick={() => append({ code: '', type: 1 })}
                >
                  <span className="material-symbols-outlined" style={{ fontSize: 14 }}>add</span>
                  {t('products.wizard.barcode.add','Agregar Código')}
                </button>
              </div>

              {barcodeFields.length === 0 ? (
                <div className="prd-barcodes__empty">
                  <span className="material-symbols-outlined" style={{ fontSize: 28, color: 'var(--color-text-secondary)' }}>qr_code_scanner</span>
                  <p>{t('products.wizard.barcode.empty','Sin códigos de barras')}</p>
                </div>
              ) : (
                <div className="prd-barcodes__list">
                  {barcodeFields.map((field, idx) => (
                    <div key={field.id} className="prd-barcodes__row prd-slidedown">
                      <input
                        {...register(`barcodes.${idx}.code`)}
                        className="zh-input prd-wiz-input"
                        placeholder="Código de barras"
                        aria-label={`Código de barras ${idx + 1}`}
                      />
                      <select
                        {...register(`barcodes.${idx}.type`, { valueAsNumber: true })}
                        className="zh-input prd-wiz-input prd-wiz-select"
                        aria-label={`Tipo de código ${idx + 1}`}
                      >
                        {barcodeTypes.map((bt) => (
                          <option key={bt.value} value={bt.value}>{bt.label}</option>
                        ))}
                      </select>
                      <button
                        type="button"
                        className="zh-btn zh-btn--ghost zh-btn--sm prd-btn-remove"
                        onClick={() => remove(idx)}
                        aria-label={`Quitar código ${idx + 1}`}
                        title="Quitar"
                      >
                        <span className="material-symbols-outlined" style={{ fontSize: 16 }}>delete</span>
                        Quitar
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        )}

        {/* ── STEP 2: Clasificación ─────────────────────── */}
        {step === 2 && (
          <div className="prd-wiz-panel prd-fadein">
            <div className="prd-wiz-panel__head">
              <span className="prd-wiz-panel__icon prd-wiz-panel__icon--green material-symbols-outlined">category</span>
              <div>
                <h3 className="prd-wiz-panel__title">{t('products.wizard.classification.title','📂 Clasificación')}</h3>
                <p className="prd-wiz-panel__desc">{t('products.wizard.classification.desc','Organiza el producto en el árbol de categorías y catálogos.')}</p>
              </div>
            </div>

            {catalogsLoading ? (
              <div className="prd-wiz-skeleton-grid">
                {Array.from({ length: 6 }).map((_, i) => (
                  <div key={i} className="prd-wiz-skeleton" />
                ))}
              </div>
            ) : (
              <>
                <div className="prd-wiz-grid prd-wiz-grid--3">
                  <FieldWrap t={t} label={t('products.wizard.field.line','Línea')} required error={errors.lineId?.message}>
                    <select {...register('lineId')} className="zh-input prd-wiz-input prd-wiz-select" aria-required="true">
                      <option value={EMPTY_GUID}>Seleccionar…</option>
                      {(catalogs?.lines ?? []).map((x) => (
                        <option key={x.id} value={x.id}>{x.name}</option>
                      ))}
                    </select>
                  </FieldWrap>

                  <FieldWrap t={t} label={t('products.wizard.field.category','Categoría')} required error={errors.categoryId?.message}>
                    <select {...register('categoryId')} className="zh-input prd-wiz-input prd-wiz-select" disabled={selectedLineId === EMPTY_GUID} aria-required="true">
                      <option value={EMPTY_GUID}>Seleccionar…</option>
                      {filteredCategories.map((x) => (
                        <option key={x.id} value={x.id}>{x.name}</option>
                      ))}
                    </select>
                  </FieldWrap>

                  <FieldWrap t={t} label={t('products.wizard.field.subcategory','Subcategoría')} required error={errors.subcategoryId?.message}>
                    <select {...register('subcategoryId')} className="zh-input prd-wiz-input prd-wiz-select" disabled={selectedCategoryId === EMPTY_GUID} aria-required="true">
                      <option value={EMPTY_GUID}>Seleccionar…</option>
                      {filteredSubcategories.map((x) => (
                        <option key={x.id} value={x.id}>{x.name}</option>
                      ))}
                    </select>
                  </FieldWrap>
                </div>

                <div className="prd-wiz-grid prd-wiz-grid--3">
                  <FieldWrap t={t} label={t('products.wizard.field.unit','Unidad de medida')} required error={errors.unitOfMeasureId?.message}>
                    <select {...register('unitOfMeasureId')} className="zh-input prd-wiz-input prd-wiz-select" aria-required="true">
                      <option value={EMPTY_GUID}>Seleccionar…</option>
                      {(catalogs?.units ?? []).map((x) => (
                        <option key={x.id} value={x.id}>{x.name}</option>
                      ))}
                    </select>
                  </FieldWrap>

                  <FieldWrap t={t} label={t('products.wizard.field.brand','Marca')} required error={errors.brandId?.message}>
                    <select {...register('brandId')} className="zh-input prd-wiz-input prd-wiz-select" aria-required="true">
                      <option value={EMPTY_GUID}>Seleccionar…</option>
                      {(catalogs?.brands ?? []).map((x) => (
                        <option key={x.id} value={x.id}>{x.name}</option>
                      ))}
                    </select>
                  </FieldWrap>

                  <FieldWrap t={t} label={t('products.wizard.field.productType','Tipo de producto')} required error={errors.productTypeId?.message}>
                    <select {...register('productTypeId')} className="zh-input prd-wiz-input prd-wiz-select" aria-required="true">
                      <option value={EMPTY_GUID}>Seleccionar…</option>
                      {(catalogs?.productTypes ?? []).map((x) => (
                        <option key={x.id} value={x.id}>{x.name}</option>
                      ))}
                    </select>
                  </FieldWrap>
                </div>

                <div className="prd-wiz-grid prd-wiz-grid--2">
                  <FieldWrap t={t} label={t('products.wizard.field.tariff','Arancel')} required error={errors.tariffId?.message}>
                    <select {...register('tariffId')} className="zh-input prd-wiz-input prd-wiz-select" aria-required="true">
                      <option value={EMPTY_GUID}>Seleccionar…</option>
                      {(catalogs?.tariffs ?? []).map((x) => (
                        <option key={x.id} value={x.id}>{x.name}</option>
                      ))}
                    </select>
                  </FieldWrap>
                </div>
              </>
            )}
          </div>
        )}

        {/* ── STEP 3: Impuestos y Config ────────────────── */}
        {step === 3 && (
          <div className="prd-wiz-panel prd-fadein">
            <div className="prd-wiz-panel__head">
              <span className="prd-wiz-panel__icon prd-wiz-panel__icon--amber material-symbols-outlined">settings</span>
              <div>
                <h3 className="prd-wiz-panel__title">{t('products.wizard.config.title','⚙️ Impuestos y Configuración')}</h3>
                <p className="prd-wiz-panel__desc">{t('products.wizard.config.desc','Define los impuestos, comportamiento de inventario y canales de venta.')}</p>
              </div>
            </div>

            {/* Impuestos */}
            <div className="prd-wiz-subsec">
              <h4 className="prd-wiz-subsec__title">{t('products.wizard.section.taxes','💰 Impuestos')}</h4>
              <div className="prd-wiz-grid prd-wiz-grid--2">
                <FieldWrap t={t} label={t('products.wizard.field.vatSale','IVA en ventas')}>
                  <select
                    className="zh-input prd-wiz-input prd-wiz-select"
                    onChange={(e) => {
                      const v = e.target.value;
                      if (v === 'none') {
                        setValue('appliesVatOnSale', false);
                        setValue('saleTaxId', EMPTY_GUID);
                      } else {
                        setValue('appliesVatOnSale', true);
                        setValue('saleTaxId', v);
                      }
                    }}
                    value={allValues.appliesVatOnSale ? allValues.saleTaxId : 'none'}
                  >
                    <option value="none">Ninguno</option>
                    {(catalogs?.taxRates ?? []).map((x) => (
                      <option key={x.id} value={x.id}>{x.name}</option>
                    ))}
                  </select>
                </FieldWrap>

                <FieldWrap t={t} label={t('products.wizard.field.vatPurchase','IVA en compras')}>
                  <select
                    className="zh-input prd-wiz-input prd-wiz-select"
                    onChange={(e) => {
                      const v = e.target.value;
                      if (v === 'none') {
                        setValue('appliesVatOnPurchase', false);
                        setValue('purchaseTaxId', EMPTY_GUID);
                      } else {
                        setValue('appliesVatOnPurchase', true);
                        setValue('purchaseTaxId', v);
                      }
                    }}
                    value={allValues.appliesVatOnPurchase ? allValues.purchaseTaxId : 'none'}
                  >
                    <option value="none">Ninguno</option>
                    {(catalogs?.taxRates ?? []).map((x) => (
                      <option key={x.id} value={x.id}>{x.name}</option>
                    ))}
                  </select>
                </FieldWrap>
              </div>
            </div>

            {/* Control de inventario */}
            <div className="prd-wiz-subsec">
              <h4 className="prd-wiz-subsec__title">{t('products.wizard.section.stock','📦 Control de Inventario')}</h4>
              <div className="prd-opt-grid">
                {[
                  { field: 'tracksStock' as const, icon: 'inventory_2', title: t('products.wizard.stock.tracksStock','Controla Stock') },
                  { field: 'tracksSeries' as const, icon: 'pin',        title: t('products.wizard.stock.tracksSeries','Controla Serie') },
                  { field: 'tracksLot'    as const, icon: 'batch_prediction', title: t('products.wizard.stock.tracksLot','Controla Lote') },
                  { field: 'isService'    as const, icon: 'build',      title: t('products.wizard.stock.isService','Es Servicio') },
                ].map((opt) => (
                  <OptionCard
                    key={opt.field}
                    id={`opt-${opt.field}`}
                    checked={!!allValues[opt.field]}
                    onChange={(v) => setValue(opt.field, v)}
                    icon={opt.icon}
                    title={opt.title}
                  />
                ))}
              </div>
            </div>

            {/* Canales de venta */}
            <div className="prd-wiz-subsec">
              <h4 className="prd-wiz-subsec__title">{t('products.wizard.section.channels','🛒 Canales de Venta')}</h4>
              <div className="prd-opt-grid">
                {[
                  { field: 'isForSale'          as const, icon: 'sell',         title: t('products.wizard.channel.isForSale','En Venta') },
                  { field: 'availableOnWeb'      as const, icon: 'language',     title: t('products.wizard.channel.web','Tienda Web') },
                  { field: 'availableOnMobile'   as const, icon: 'smartphone',   title: t('products.wizard.channel.mobile','App Móvil') },
                  { field: 'isFavorite'          as const, icon: 'star',         title: t('products.wizard.channel.favorite','Favorito') },
                ].map((opt) => (
                  <OptionCard
                    key={opt.field}
                    id={`opt-${opt.field}`}
                    checked={!!allValues[opt.field]}
                    onChange={(v) => setValue(opt.field, v)}
                    icon={opt.icon}
                    title={opt.title}
                  />
                ))}
              </div>
            </div>
          </div>
        )}

        {/* ── STEP 4: Revisar y Guardar ─────────────────── */}
        {step === 4 && (
          <div className="prd-wiz-panel prd-fadein">
            <div className="prd-wiz-panel__head">
              <span className="prd-wiz-panel__icon prd-wiz-panel__icon--teal material-symbols-outlined">check_circle</span>
              <div>
                <h3 className="prd-wiz-panel__title">{t('products.wizard.review.title','✅ Revisar y Guardar')}</h3>
                <p className="prd-wiz-panel__desc">{t('products.wizard.review.desc','Revisa que todo esté correcto antes de guardar.')}</p>
              </div>
            </div>

            <div className="prd-review-card">
              <div className="prd-review-card__title">{t('products.wizard.review.cardTitle','✅ Resumen del Producto')}</div>
              <div className="prd-review-grid">
                {summaryRows.map(([label, value]) => (
                  <div key={label} className="prd-review-row">
                    <span className="prd-review-row__label">{label}</span>
                    <span className={`prd-review-row__value ${!value || value === '—' ? 'prd-review-row__value--empty' : ''}`}>
                      {value || t('products.wizard.summary.notSet','(Sin especificar)')}
                    </span>
                  </div>
                ))}
              </div>
            </div>

            <div className="prd-review-warning" role="note">
              <span className="material-symbols-outlined" style={{ fontSize: 18 }}>warning</span>
              <span>{t('products.wizard.review.warning','⚠️ Una vez guardado, el producto aparecerá en su catálogo.')}</span>
            </div>
          </div>
        )}

        {/* ── Footer ────────────────────────────────────── */}
        <div className="prd-wiz-footer">
          <div className="prd-wiz-footer__left">
            {step > 1 && (
              <button
                type="button"
                className="zh-btn zh-btn--ghost zh-btn--md"
                onClick={goPrev}
              >
                <span className="material-symbols-outlined" style={{ fontSize: 16 }}>arrow_back</span>{t('products.wizard.btn.prev','Anterior')}</button>
            )}
          </div>

          <div className="prd-wiz-footer__right">
            {editingProduct && (
              <button
                type="button"
                className="zh-btn zh-btn--ghost zh-btn--md"
                onClick={onCancel}
              >
                Cancelar
              </button>
            )}

            <button
              type="button"
              className="zh-btn zh-btn--ghost zh-btn--md"
              onClick={handleSaveDraft}
              title="Guardar borrador en el navegador"
            >
              <span className="material-symbols-outlined" style={{ fontSize: 16 }}>save</span>{t('products.wizard.draft.save','Guardar Borrador')}</button>

            {step < 4 ? (
              <button
                type="button"
                className="zh-btn zh-btn--primary zh-btn--md"
                onClick={goNext}
              >{t('products.wizard.btn.next','Siguiente')}<span className="material-symbols-outlined" style={{ fontSize: 16 }}>arrow_forward</span>
              </button>
            ) : (
              <button
                type="submit"
                className="zh-btn zh-btn--md prd-btn--success"
                disabled={submitting}
              >
                {submitting ? (
                  <>
                    <span className="prd-spinner" aria-hidden />
                    {t('products.wizard.btn.saving','Guardando…')}
                  </>
                ) : (
                  <>
                    <span className="material-symbols-outlined" style={{ fontSize: 16 }}>check</span>
                    {editingProduct ? t('products.wizard.btn.update','Actualizar Producto') : t('products.wizard.btn.save','Guardar Producto')}
                  </>
                )}
              </button>
            )}
          </div>
        </div>
      </form>
    </div>
  );
}

// Re-export the build payload helper so ProductPage can use it
export function buildProductPayload(values: ProductFormValues) {
  return {
    saleCode:              values.saleCode,
    purchaseCode:          values.purchaseCode || undefined,
    shortName:             values.shortName,
    description:           values.description,
    observations:          values.observations || undefined,
    lineId:                values.lineId,
    categoryId:            values.categoryId,
    subcategoryId:         values.subcategoryId,
    unitOfMeasureId:       values.unitOfMeasureId,
    brandId:               values.brandId,
    productTypeId:         values.productTypeId,
    tariffId:              values.tariffId,
    appliesVatOnSale:      values.appliesVatOnSale,
    appliesVatOnPurchase:  values.appliesVatOnPurchase,
    appliesExciseTax:      values.appliesExciseTax,
    saleTaxId:             toOptionalGuid(values.saleTaxId),
    purchaseTaxId:         toOptionalGuid(values.purchaseTaxId),
    exciseTaxId:           toOptionalGuid(values.exciseTaxId),
    saleVatAccountId:      null,
    purchaseVatAccountId:  null,
    exciseAccountId:       null,
    isService:             values.isService,
    tracksStock:           values.tracksStock,
    tracksLot:             values.tracksLot,
    tracksSeries:          values.tracksSeries,
    hasRecipe:             values.hasRecipe,
    stockWithDecimal:      values.stockWithDecimal,
    saleWithDecimal:       values.saleWithDecimal,
    maxItemDiscountPercent: values.maxItemDiscountPercent,
    availableOnWeb:        values.availableOnWeb,
    availableOnMobile:     values.availableOnMobile,
    isEcommerceActive:     values.isEcommerceActive,
    isFavorite:            values.isFavorite,
    isForSale:             values.isForSale,
    baseColor:             values.baseColor || undefined,
    hasMultipleColors:     values.hasMultipleColors,
    hasSizes:              values.hasSizes,
    handlesTariff:         values.handlesTariff,
    barcodes:              (values.barcodes ?? []).map((b) => ({ code: b.code, type: b.type })),
  };
}
