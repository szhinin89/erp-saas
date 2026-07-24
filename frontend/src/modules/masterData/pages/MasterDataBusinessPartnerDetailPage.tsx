import { useCallback, useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHBtn, ZHField, ZHGrid, ZHFormActions } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ZHModal } from '../../../components/zh/ZHModal';
import { message } from '../../../lib/messages';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { applyServerErrors } from '../../lib/validationErrors';
import { businessPartnerFacade } from '../api/businessPartnerFacade';
import { formatDate } from '../../../lib/formatters/dateFormatters';
import { geographyService, type GeoOption } from '../api/geographyService';
import { useSriIdTypes, getSriIdTypeName } from '../api/useSriIdTypes';
import {
  LocationTypeEnum, LocationPurposeEnum, ContactRoleEnum,
} from '../types/businessPartner.types';
import type {
  BusinessPartnerDetailDto, BusinessPartnerRoleDto,
  BpLocationDto, BpContactDto,
  CreateLocationBody, UpdateLocationBody,
  CreateContactBody, UpdateContactBody,
} from '../types/businessPartner.types';
import './masterdata-pages.css';

// ── Zod schemas ──────────────────────────────────────────────────────────────

const locationSchema = z.object({
  name:        z.string().min(1, 'Nombre obligatorio.').max(150),
  type:        z.number({ required_error: 'Seleccione un tipo.' }),
  purpose:     z.number().min(0).default(0),
  addressLine: z.string().min(5, 'Mínimo 5 caracteres.').max(300),
  provinceCode: z.string().max(2).optional().or(z.literal('')),
  cantonCode:   z.string().max(4).optional().or(z.literal('')),
  parishCode:   z.string().max(6).optional().or(z.literal('')),
  phone:       z.string().max(20).optional().or(z.literal('')),
  email:       z.string().max(254).optional().or(z.literal('')),
  isPrimary:   z.boolean().default(false),
  otherDescription: z.string().max(100).optional().or(z.literal('')),
});
type LocationFormValues = z.infer<typeof locationSchema>;

const contactSchema = z.object({
  firstName:    z.string().min(1, 'Nombre obligatorio.').max(100),
  lastName:     z.string().max(100).optional().or(z.literal('')),
  position:     z.string().max(150).optional().or(z.literal('')),
  role:         z.number({ required_error: 'Seleccione un rol.' }),
  locationId:   z.string().optional().or(z.literal('')),
  phone:        z.string().max(20).optional().or(z.literal('')),
  mobile:       z.string().max(20).optional().or(z.literal('')),
  email:        z.string().max(254).optional().or(z.literal('')),
  notes:        z.string().max(2000).optional().or(z.literal('')),
  isPrimary:    z.boolean().default(false),
  otherDescription: z.string().max(100).optional().or(z.literal('')),
});
type ContactFormValues = z.infer<typeof contactSchema>;

// ── Enum labels ──────────────────────────────────────────────────────────────

const LOCATION_TYPES = [
  { value: LocationTypeEnum.Matrix,        label: 'Matriz' },
  { value: LocationTypeEnum.Branch,        label: 'Sucursal' },
  { value: LocationTypeEnum.Office,        label: 'Oficina' },
  { value: LocationTypeEnum.Warehouse,     label: 'Bodega' },
  { value: LocationTypeEnum.DeliveryPoint, label: 'Punto de entrega' },
  { value: LocationTypeEnum.Other,         label: 'Otro' },
];

const CONTACT_ROLES = [
  { value: ContactRoleEnum.Commercial,  label: 'Comercial' },
  { value: ContactRoleEnum.Accounting,  label: 'Contabilidad' },
  { value: ContactRoleEnum.Management,  label: 'Gerencia' },
  { value: ContactRoleEnum.Reception,   label: 'Recepción' },
  { value: ContactRoleEnum.Dispatch,    label: 'Despacho' },
  { value: ContactRoleEnum.Billing,     label: 'Facturación' },
  { value: ContactRoleEnum.Technical,   label: 'Técnico' },
  { value: ContactRoleEnum.Purchasing,  label: 'Compras' },
  { value: ContactRoleEnum.Legal,       label: 'Legal' },
  { value: ContactRoleEnum.Other,       label: 'Otro' },
];

const PURPOSE_FLAGS = [
  { flag: LocationPurposeEnum.Billing,        label: 'Facturación' },
  { flag: LocationPurposeEnum.Delivery,       label: 'Entrega' },
  { flag: LocationPurposeEnum.Fiscal,         label: 'Fiscal' },
  { flag: LocationPurposeEnum.Correspondence, label: 'Correspondencia' },
];

// ── Helpers ──────────────────────────────────────────────────────────────────

function Row({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="md-detail-row">
      <span className="md-detail-label">{label}</span>
      <span className="md-detail-value">{value ?? <em className="md-detail-empty">—</em>}</span>
    </div>
  );
}

// ── Main page ────────────────────────────────────────────────────────────────

type TabId = 'identity' | 'roles' | 'locations' | 'contacts';
type LocModal = { mode: 'create' | 'edit'; data?: BpLocationDto } | null;
type ConModal = { mode: 'create' | 'edit'; data?: BpContactDto } | null;

export function MasterDataBusinessPartnerDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { canShow } = usePermissionsUi();
  const canUpdate = canShow('masterdata.businesspartners.update');
  const canDisable = canShow('masterdata.businesspartners.disable');
  const { options: idTypes } = useSriIdTypes();

  const [bp, setBp]             = useState<BusinessPartnerDetailDto | null>(null);
  const [locations, setLocations] = useState<BpLocationDto[]>([]);
  const [contacts, setContacts]   = useState<BpContactDto[]>([]);
  const [loading, setLoading]   = useState(true);
  const [error, setError]       = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<TabId>('identity');
  const [saving, setSaving]     = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const [locModal, setLocModal] = useState<LocModal>(null);
  const [conModal, setConModal] = useState<ConModal>(null);
  const [locFilter, setLocFilter] = useState<'all' | 'active' | 'inactive'>('active');
  const [geoProvinces, setGeoProvinces] = useState<GeoOption[]>([]);
  const [geoCantons, setGeoCantons]     = useState<GeoOption[]>([]);
  const [geoParishes, setGeoParishes]   = useState<GeoOption[]>([]);

  const reload = useCallback(async (showLoading = false) => {
    if (!id) return;
    if (showLoading) setLoading(true);
    setError(null);
    try {
      const [bpData, locs, cons] = await Promise.all([
        businessPartnerFacade.getBusinessPartner(id),
        businessPartnerFacade.getLocations(id, undefined),
        businessPartnerFacade.getContacts(id, undefined),
      ]);
      setBp(bpData);
      setLocations(locs);
      setContacts(cons);
    } catch {
      setError('No se pudo cargar el BusinessPartner.');
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { void reload(true); }, [reload]);

  // ── Location form ──────────────────────────────────────────────────────────

  const locForm = useForm<LocationFormValues>({
    resolver: zodResolver(locationSchema),
    defaultValues: { name: '', type: LocationTypeEnum.Matrix, purpose: 0, addressLine: '', isPrimary: false },
  });

  const loadProvinces = async () => {
    try { setGeoProvinces(await geographyService.provinces('EC')); } catch { /* empty */ }
  };
  const loadCantons = async (provinceId: string) => {
    if (!provinceId) { setGeoCantons([]); setGeoParishes([]); return; }
    try { setGeoCantons(await geographyService.cantons(provinceId)); } catch { /* empty */ }
    setGeoParishes([]);
  };
  const loadParishes = async (cantonId: string) => {
    if (!cantonId) { setGeoParishes([]); return; }
    try { setGeoParishes(await geographyService.parishes(cantonId)); } catch { /* empty */ }
  };

  const openLocCreate = async () => {
    locForm.reset({ name: '', type: LocationTypeEnum.Matrix, purpose: 0, addressLine: '', isPrimary: false,
      provinceCode: '', cantonCode: '', parishCode: '', phone: '', email: '', otherDescription: '' });
    setActionError(null);
    setGeoCantons([]); setGeoParishes([]);
    await loadProvinces();
    setLocModal({ mode: 'create' });
  };

  const openLocEdit = async (loc: BpLocationDto) => {
    const purposeMap: Record<string, number> = { 'Facturación': 1, Entrega: 2, Fiscal: 4, Correspondencia: 8 };
    const purposeVal = loc.purposes.reduce((acc, p) => acc | (purposeMap[p] ?? 0), 0);
    const typeVal = LocationTypeEnum[loc.locationType as keyof typeof LocationTypeEnum] ?? LocationTypeEnum.Other;
    locForm.reset({
      name: loc.name, type: typeVal, purpose: purposeVal, addressLine: loc.addressLine,
      provinceCode: loc.provinceCode ?? '', cantonCode: loc.cantonCode ?? '', parishCode: loc.parishCode ?? '',
      phone: loc.phone ?? '', email: loc.email ?? '', otherDescription: loc.otherDescription ?? '', isPrimary: false,
    });
    setActionError(null);
    await loadProvinces();
    if (loc.provinceCode) await loadCantons(loc.provinceCode);
    if (loc.cantonCode) await loadParishes(loc.cantonCode);
    setLocModal({ mode: 'edit', data: loc });
  };

  const saveLoc = locForm.handleSubmit(async (values) => {
    if (!id) return;
    setActionError(null);
    setSaving(true);
    try {
      const body = {
        name: values.name, type: values.type, purpose: values.purpose, addressLine: values.addressLine,
        provinceCode: values.provinceCode || null, cantonCode: values.cantonCode || null,
        parishCode: values.parishCode || null, phone: values.phone || null, email: values.email || null,
        otherDescription: values.otherDescription || null, isPrimary: values.isPrimary,
      };
      if (locModal?.mode === 'edit' && locModal.data) {
        const { isPrimary: _, ...updateBody } = body;
        await businessPartnerFacade.updateLocation(id, locModal.data.id, updateBody as UpdateLocationBody);
        message.success('Ubicación actualizada correctamente.');
      } else {
        await businessPartnerFacade.createLocation(id, body as CreateLocationBody);
        message.success('Ubicación creada correctamente.');
      }
      setLocModal(null);
      await reload();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, locForm.setError, (msg) => setActionError(msg));
      if (!applied) setActionError('Error al guardar la ubicación.');
    } finally {
      setSaving(false);
    }
  });

  const toggleLoc = async (loc: BpLocationDto) => {
    if (!id) return;
    setSaving(true);
    try {
      if (loc.isActive) await businessPartnerFacade.deactivateLocation(id, loc.id);
      else await businessPartnerFacade.activateLocation(id, loc.id);
      await reload();
    } catch { setActionError('Error al cambiar estado de la ubicación.'); }
    finally { setSaving(false); }
  };

  const setPrimaryLoc = async (loc: BpLocationDto) => {
    if (!id) return;
    setSaving(true);
    try { await businessPartnerFacade.setLocationPrimary(id, loc.id); await reload(); }
    catch { setActionError('Error al marcar como principal.'); }
    finally { setSaving(false); }
  };

  // ── Contact form ───────────────────────────────────────────────────────────

  const conForm = useForm<ContactFormValues>({
    resolver: zodResolver(contactSchema),
    defaultValues: { firstName: '', role: ContactRoleEnum.Commercial, isPrimary: false },
  });

  const openConCreate = () => {
    conForm.reset({ firstName: '', lastName: '', position: '', role: ContactRoleEnum.Commercial,
      locationId: '', phone: '', mobile: '', email: '', notes: '', isPrimary: false, otherDescription: '' });
    setActionError(null);
    setConModal({ mode: 'create' });
  };

  const openConEdit = (con: BpContactDto) => {
    const roleVal = ContactRoleEnum[con.contactRole as keyof typeof ContactRoleEnum] ?? ContactRoleEnum.Other;
    conForm.reset({
      firstName: con.firstName, lastName: con.lastName ?? '', position: con.position ?? '',
      role: roleVal, locationId: con.locationId ?? '', phone: con.phone ?? '',
      mobile: con.mobile ?? '', email: con.email ?? '', notes: con.notes ?? '',
      isPrimary: false, otherDescription: con.otherDescription ?? '',
    });
    setActionError(null);
    setConModal({ mode: 'edit', data: con });
  };

  const saveCon = conForm.handleSubmit(async (values) => {
    if (!id) return;
    setActionError(null);
    setSaving(true);
    try {
      const body = {
        firstName: values.firstName, role: values.role,
        lastName: values.lastName || null, position: values.position || null,
        locationId: values.locationId || null, phone: values.phone || null,
        mobile: values.mobile || null, email: values.email || null,
        notes: values.notes || null, isPrimary: values.isPrimary,
        otherDescription: values.otherDescription || null,
      };
      if (conModal?.mode === 'edit' && conModal.data) {
        const { isPrimary: _, ...updateBody } = body;
        await businessPartnerFacade.updateContact(id, conModal.data.id, updateBody as UpdateContactBody);
        message.success('Contacto actualizado correctamente.');
      } else {
        await businessPartnerFacade.createContact(id, body as CreateContactBody);
        message.success('Contacto creado correctamente.');
      }
      setConModal(null);
      await reload();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, conForm.setError, (msg) => setActionError(msg));
      if (!applied) setActionError('Error al guardar el contacto.');
    } finally {
      setSaving(false);
    }
  });

  const toggleCon = async (con: BpContactDto) => {
    if (!id) return;
    setSaving(true);
    try {
      if (con.isActive) await businessPartnerFacade.deactivateContact(id, con.id);
      else await businessPartnerFacade.activateContact(id, con.id);
      await reload();
    } catch { setActionError('Error al cambiar estado del contacto.'); }
    finally { setSaving(false); }
  };

  const setPrimaryCon = async (con: BpContactDto) => {
    if (!id) return;
    setSaving(true);
    try { await businessPartnerFacade.setContactPrimary(id, con.id); await reload(); }
    catch { setActionError('Error al marcar como principal.'); }
    finally { setSaving(false); }
  };

  // ── Role revoke ────────────────────────────────────────────────────────────

  const revokeRole = async (role: BusinessPartnerRoleDto) => {
    if (!id) return;
    setSaving(true);
    try { await businessPartnerFacade.revokeRole(id, role.id); await reload(); }
    catch { setActionError('Error al revocar el rol.'); }
    finally { setSaving(false); }
  };

  // ── Derived ────────────────────────────────────────────────────────────────

  const activeRoles  = bp?.roles.filter(r => r.isActive)  ?? [];
  const revokedRoles = bp?.roles.filter(r => !r.isActive) ?? [];
  const activeLocs   = locations.filter(l => l.isActive);
  const activeCons   = contacts.filter(c => c.isActive);

  const TABS: { id: TabId; label: string; count?: number }[] = [
    { id: 'identity',  label: 'Identidad' },
    { id: 'roles',     label: 'Roles',      count: activeRoles.length },
    { id: 'locations', label: 'Ubicaciones', count: activeLocs.length },
    { id: 'contacts',  label: 'Contactos',  count: activeCons.length },
  ];

  return (
    <ErpPageTemplate kicker="MasterData"
      title={bp ? (bp.tradeName?.trim() || bp.legalName) : 'BusinessPartner'}
      subtitle={bp ? `${bp.identificationType} ${bp.identificationNumber}` : ''}
      action={<ZHBtn variant="ghost" onClick={() => navigate(-1)}>← Volver</ZHBtn>}>

      {loading && <p>Cargando…</p>}
      {error && <ZHPageNotice variant="error" message={error} />}
      {actionError && <ZHPageNotice variant="error" message={actionError} />}

      {bp && (
        <>
          <div className="md-detail-status-row">
            <span className={`md-badge ${bp.isActive ? 'md-badge--ok' : 'md-badge--warn'}`}>
              {bp.isActive ? 'Activo' : 'Inactivo'}
            </span>
            {activeRoles.map(r => <span key={r.id} className="md-badge md-badge--ok">{r.roleLabel}</span>)}
          </div>

          <nav className="md-detail-tabs">
            {TABS.map(tab => (
              <button key={tab.id} type="button"
                className={`md-detail-tab ${activeTab === tab.id ? 'md-detail-tab--active' : ''}`}
                onClick={() => setActiveTab(tab.id)}>
                {tab.label}
                {tab.count !== undefined && <span className="md-detail-tab__count">{tab.count}</span>}
              </button>
            ))}
          </nav>

          {/* ── Identity ──────────────────────────────────────────── */}
          {activeTab === 'identity' && (
            <section className="md-detail-section">
              <Row label="Tipo de identificación" value={`${bp.identificationType} — ${getSriIdTypeName(bp.identificationType, idTypes)}`} />
              <Row label="Número" value={bp.identificationNumber} />
              <Row label="Tipo de persona" value={bp.personType} />
              <Row label="Razón social" value={bp.legalName} />
              <Row label="Nombre comercial" value={bp.tradeName} />
              <Row label="País" value={bp.countryCode ?? 'EC'} />
              <Row label="Creado" value={formatDate(bp.createdAt)} />
            </section>
          )}

          {/* ── Roles ─────────────────────────────────────────────── */}
          {activeTab === 'roles' && (
            <section className="md-detail-section">
              {bp.roles.length === 0 ? (
                <p className="md-detail-empty-hint">Este BP no tiene roles asignados.</p>
              ) : (
                <>
                  {activeRoles.length > 0 && (
                    <div className="md-roles-grid">
                      {activeRoles.map(r => (
                        <div key={r.id} className="md-role-card">
                          <div className="md-role-card__header">
                            <span className="md-badge md-badge--ok">{r.roleLabel}</span>
                            {canDisable && (
                              <ZHBtn variant="ghost" size="sm" disabled={saving} onClick={() => void revokeRole(r)}>
                                Revocar
                              </ZHBtn>
                            )}
                          </div>
                          {r.notes && <p className="md-role-card__notes">{r.notes}</p>}
                          {r.customerConfig && (
                            <div className="md-role-card__config md-role-card__config--crm">
                              {r.customerConfig.customerCategory && <small>Categoría: {r.customerConfig.customerCategory}</small>}
                              {r.customerConfig.customerSegment && <small>Segmento: {r.customerConfig.customerSegment}</small>}
                              {r.customerConfig.salesZone && <small>Zona: {r.customerConfig.salesZone}</small>}
                              {r.customerConfig.creditRating && <small>Rating: {r.customerConfig.creditRating}</small>}
                              {r.customerConfig.loyaltyTier && <small>Fidelización: {r.customerConfig.loyaltyTier}</small>}
                            </div>
                          )}
                          {r.supplierConfig && (
                            <div className="md-role-card__config">
                              <small>Sustento: {r.supplierConfig.defaultTaxSupportCode ?? '—'}</small>
                              <small>Ret. IVA: {r.supplierConfig.defaultRetentionVatCode ?? '—'}</small>
                              <small>Ret. Renta: {r.supplierConfig.defaultRetentionIncomeCode ?? '—'}</small>
                            </div>
                          )}
                          {r.carrierConfig?.transportAuthorizationNumber && (
                            <div className="md-role-card__config">
                              <small>Autorización: {r.carrierConfig.transportAuthorizationNumber}</small>
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                  {revokedRoles.length > 0 && (
                    <>
                      <h4 className="md-detail-subheader">Roles revocados</h4>
                      <div className="md-roles-grid md-roles-grid--inactive">
                        {revokedRoles.map(r => (
                          <div key={r.id} className="md-role-card md-role-card--inactive">
                            <div className="md-role-card__header">
                              <span className="md-badge md-badge--warn">{r.roleLabel}</span>
                              <span className="md-badge md-badge--warn">Revocado</span>
                            </div>
                          </div>
                        ))}
                      </div>
                    </>
                  )}
                </>
              )}
            </section>
          )}

          {/* ── Locations ─────────────────────────────────────────── */}
          {activeTab === 'locations' && (() => {
            const filteredLocs = locFilter === 'all' ? locations
              : locFilter === 'active' ? locations.filter(l => l.isActive)
              : locations.filter(l => !l.isActive);
            return (
            <section className="md-detail-section">
              <div className="md-detail-section__header">
                <h3>Ubicaciones</h3>
                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                  <select style={{ width: 'auto', fontSize: '0.85rem' }}
                    value={locFilter} onChange={e => setLocFilter(e.target.value as 'all' | 'active' | 'inactive')}>
                    <option value="active">Activas</option>
                    <option value="inactive">Inactivas</option>
                    <option value="all">Todas</option>
                  </select>
                  <ZHBtn variant="primary" size="sm" onClick={openLocCreate} disabled={!canUpdate}>
                    <span className="material-symbols-outlined">add</span> Nueva ubicación
                  </ZHBtn>
                </div>
              </div>
              {filteredLocs.length === 0 ? (
                <p className="md-detail-empty-hint">
                  {locations.length === 0 ? 'No hay ubicaciones registradas.' : `No hay ubicaciones ${locFilter === 'active' ? 'activas' : 'inactivas'}.`}
                </p>
              ) : (
                <div className="md-locations-grid">
                  {filteredLocs.map(l => (
                    <div key={l.id} className={`md-location-card ${!l.isActive ? 'md-location-card--inactive' : ''}`}>
                      <div className="md-location-card__header">
                        <span className="md-location-card__name">{l.name}</span>
                        {l.isPrimary && <span className="md-badge md-badge--ok">Principal</span>}
                        <span className="md-badge">{l.typeLabel}</span>
                      </div>
                      <p className="md-location-card__address">{l.addressLine}</p>
                      {l.purposes.length > 0 && (
                        <div className="md-location-card__purposes">
                          {l.purposes.map(p => <span key={p} className="md-badge">{p}</span>)}
                        </div>
                      )}
                      {(l.phone || l.email) && (
                        <p className="md-location-card__contact">
                          {l.phone && <span>📞 {l.phone}</span>}
                          {l.email && <span>✉ {l.email}</span>}
                        </p>
                      )}
                      {(canUpdate || canDisable) && (
                        <div className="md-location-card__actions">
                          {canUpdate && l.isActive && (
                            <ZHBtn variant="ghost" size="sm" onClick={() => openLocEdit(l)}>Editar</ZHBtn>
                          )}
                          {canUpdate && l.isActive && !l.isPrimary && (
                            <ZHBtn variant="ghost" size="sm" disabled={saving} onClick={() => void setPrimaryLoc(l)}>Principal</ZHBtn>
                          )}
                          {(l.isActive ? canDisable : canUpdate) && (
                            <ZHBtn variant="ghost" size="sm" disabled={saving} onClick={() => void toggleLoc(l)}>
                              {l.isActive ? 'Desactivar' : 'Activar'}
                            </ZHBtn>
                          )}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </section>
            );
          })()}

          {/* ── Contacts ──────────────────────────────────────────── */}
          {activeTab === 'contacts' && (
            <section className="md-detail-section">
              <div className="md-detail-section__header">
                <h3>Contactos</h3>
                <ZHBtn variant="primary" size="sm" onClick={openConCreate} disabled={!canUpdate}>
                  <span className="material-symbols-outlined">add</span> Nuevo contacto
                </ZHBtn>
              </div>
              {contacts.length === 0 ? (
                <p className="md-detail-empty-hint">No hay contactos registrados.</p>
              ) : (
                <div className="md-contacts-grid">
                  {contacts.map(c => (
                    <div key={c.id} className={`md-contact-card ${!c.isActive ? 'md-contact-card--inactive' : ''}`}>
                      <div className="md-contact-card__header">
                        <span className="md-contact-card__name">{c.fullName}</span>
                        {c.isPrimary && <span className="md-badge md-badge--ok">Principal</span>}
                        <span className="md-badge">{c.roleLabel}</span>
                      </div>
                      {c.position && <p className="md-contact-card__position">{c.position}</p>}
                      <div className="md-contact-card__info">
                        {c.phone && <span>📞 {c.phone}</span>}
                        {c.mobile && <span>📱 {c.mobile}</span>}
                        {c.email && <span>✉ {c.email}</span>}
                      </div>
                      {(canUpdate || canDisable) && (
                        <div className="md-contact-card__actions">
                          {canUpdate && c.isActive && (
                            <ZHBtn variant="ghost" size="sm" onClick={() => openConEdit(c)}>Editar</ZHBtn>
                          )}
                          {canUpdate && c.isActive && !c.isPrimary && (
                            <ZHBtn variant="ghost" size="sm" disabled={saving} onClick={() => void setPrimaryCon(c)}>Principal</ZHBtn>
                          )}
                          {(c.isActive ? canDisable : canUpdate) && (
                            <ZHBtn variant="ghost" size="sm" disabled={saving} onClick={() => void toggleCon(c)}>
                              {c.isActive ? 'Desactivar' : 'Activar'}
                            </ZHBtn>
                          )}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </section>
          )}
        </>
      )}

      {/* ═══════════════════════════════════════════════════════════════════ */}
      {/* LOCATION MODAL                                                     */}
      {/* ═══════════════════════════════════════════════════════════════════ */}
      <ZHModal
        open={locModal !== null}
        onClose={() => setLocModal(null)}
        title={locModal?.mode === 'edit' ? 'Editar ubicación' : 'Nueva ubicación'}
        subtitle="Configure la dirección y propósito de la ubicación."
        footer={
          <ZHFormActions onCancel={() => setLocModal(null)} onSave={() => void saveLoc()} hideDraft disableSave={saving}
            labels={{ cancel: 'Cancelar', save: saving ? 'Guardando...' : locModal?.mode === 'edit' ? 'Guardar cambios' : 'Crear ubicación' }} />
        }
      >
              {actionError && <ZHPageNotice variant="error" message={actionError} />}
              <div className="pg-section-body">
                <ZHGrid cols={2}>
                  <ZHField label="Nombre *" required error={locForm.formState.errors.name?.message}>
                    <input className="zh-input" disabled={saving} {...locForm.register('name')} />
                  </ZHField>
                  <ZHField label="Tipo *" required error={locForm.formState.errors.type?.message}>
                    <select disabled={saving} {...locForm.register('type', { valueAsNumber: true })}>
                      {LOCATION_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
                    </select>
                  </ZHField>
                </ZHGrid>
                <ZHField label="Dirección *" required error={locForm.formState.errors.addressLine?.message}>
                  <textarea className="zh-input" rows={2} disabled={saving} {...locForm.register('addressLine')} />
                </ZHField>
                <ZHGrid cols={3}>
                  <ZHField label="Provincia" error={locForm.formState.errors.provinceCode?.message}>
                    <select disabled={saving} {...locForm.register('provinceCode')}
                      onChange={e => { locForm.setValue('provinceCode', e.target.value); locForm.setValue('cantonCode', ''); locForm.setValue('parishCode', ''); void loadCantons(e.target.value); }}>
                      <option value="">— Seleccionar —</option>
                      {geoProvinces.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                    </select>
                  </ZHField>
                  <ZHField label="Cantón" error={locForm.formState.errors.cantonCode?.message}>
                    <select disabled={saving || geoCantons.length === 0} {...locForm.register('cantonCode')}
                      onChange={e => { locForm.setValue('cantonCode', e.target.value); locForm.setValue('parishCode', ''); void loadParishes(e.target.value); }}>
                      <option value="">— Seleccionar —</option>
                      {geoCantons.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                    </select>
                  </ZHField>
                  <ZHField label="Parroquia" error={locForm.formState.errors.parishCode?.message}>
                    <select disabled={saving || geoParishes.length === 0} {...locForm.register('parishCode')}>
                      <option value="">— Seleccionar —</option>
                      {geoParishes.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                    </select>
                  </ZHField>
                </ZHGrid>
                <ZHGrid cols={2}>
                  <ZHField label="Teléfono">
                    <input className="zh-input" disabled={saving} {...locForm.register('phone')} />
                  </ZHField>
                  <ZHField label="Email">
                    <input className="zh-input" disabled={saving} {...locForm.register('email')} />
                  </ZHField>
                </ZHGrid>
                <ZHField label="Propósitos">
                  <div style={{ display: 'flex', gap: '16px', flexWrap: 'wrap' }}>
                    {PURPOSE_FLAGS.map(pf => (
                      <label key={pf.flag} className="zh-checkbox-label">
                        <input type="checkbox"
                          checked={(locForm.watch('purpose') & pf.flag) !== 0}
                          onChange={e => {
                            const cur = locForm.getValues('purpose');
                            locForm.setValue('purpose', e.target.checked ? cur | pf.flag : cur & ~pf.flag);
                          }}
                          disabled={saving} />
                        <span>{pf.label}</span>
                      </label>
                    ))}
                  </div>
                </ZHField>
                {locModal?.mode === 'create' && (
                  <label className="zh-checkbox-label" style={{ marginTop: 8 }}>
                    <input type="checkbox" {...locForm.register('isPrimary')} disabled={saving} />
                    <span>Marcar como ubicación principal</span>
                  </label>
                )}
              </div>
      </ZHModal>

      {/* ═══════════════════════════════════════════════════════════════════ */}
      {/* CONTACT MODAL                                                      */}
      {/* ═══════════════════════════════════════════════════════════════════ */}
      <ZHModal
        open={conModal !== null}
        onClose={() => setConModal(null)}
        title={conModal?.mode === 'edit' ? 'Editar contacto' : 'Nuevo contacto'}
        subtitle="Configure los datos del contacto."
        footer={
          <ZHFormActions onCancel={() => setConModal(null)} onSave={() => void saveCon()} hideDraft disableSave={saving}
            labels={{ cancel: 'Cancelar', save: saving ? 'Guardando...' : conModal?.mode === 'edit' ? 'Guardar cambios' : 'Crear contacto' }} />
        }
      >
              {actionError && <ZHPageNotice variant="error" message={actionError} />}
              <div className="pg-section-body">
                <ZHGrid cols={2}>
                  <ZHField label="Nombre *" required error={conForm.formState.errors.firstName?.message}>
                    <input className="zh-input" disabled={saving} {...conForm.register('firstName')} />
                  </ZHField>
                  <ZHField label="Apellido">
                    <input className="zh-input" disabled={saving} {...conForm.register('lastName')} />
                  </ZHField>
                </ZHGrid>
                <ZHGrid cols={2}>
                  <ZHField label="Rol *" required error={conForm.formState.errors.role?.message}>
                    <select disabled={saving} {...conForm.register('role', { valueAsNumber: true })}>
                      {CONTACT_ROLES.map(r => <option key={r.value} value={r.value}>{r.label}</option>)}
                    </select>
                  </ZHField>
                  <ZHField label="Cargo">
                    <input className="zh-input" disabled={saving} {...conForm.register('position')} />
                  </ZHField>
                </ZHGrid>
                <ZHGrid cols={3}>
                  <ZHField label="Teléfono">
                    <input className="zh-input" disabled={saving} {...conForm.register('phone')} />
                  </ZHField>
                  <ZHField label="Celular">
                    <input className="zh-input" disabled={saving} {...conForm.register('mobile')} />
                  </ZHField>
                  <ZHField label="Email">
                    <input className="zh-input" disabled={saving} {...conForm.register('email')} />
                  </ZHField>
                </ZHGrid>
                <ZHField label="Ubicación asociada">
                  <select disabled={saving} {...conForm.register('locationId')}>
                    <option value="">— Ninguna —</option>
                    {activeLocs.map(l => <option key={l.id} value={l.id}>{l.name} ({l.typeLabel})</option>)}
                  </select>
                </ZHField>
                <ZHField label="Notas">
                  <textarea className="zh-input" rows={2} disabled={saving} {...conForm.register('notes')} />
                </ZHField>
                {conModal?.mode === 'create' && (
                  <label className="zh-checkbox-label" style={{ marginTop: 8 }}>
                    <input type="checkbox" {...conForm.register('isPrimary')} disabled={saving} />
                    <span>Marcar como contacto principal</span>
                  </label>
                )}
              </div>
      </ZHModal>
    </ErpPageTemplate>
  );
}
