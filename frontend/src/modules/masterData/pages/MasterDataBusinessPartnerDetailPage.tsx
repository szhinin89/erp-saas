import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { businessPartnerFacade } from '../api/businessPartnerFacade';
import { useSriIdTypes, getSriIdTypeName } from '../api/useSriIdTypes';
import type {
  BusinessPartnerDetailDto,
  BusinessPartnerRoleDto,
  BpLocationDto,
  BpContactDto,
} from '../types/businessPartner.types';
import './masterdata-pages.css';

// ── Shared detail row ─────────────────────────────────────────────────────────
function Row({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="md-detail-row">
      <span className="md-detail-label">{label}</span>
      <span className="md-detail-value">{value ?? <em className="md-detail-empty">—</em>}</span>
    </div>
  );
}

// ── Role badge ────────────────────────────────────────────────────────────────
function RoleBadge({ role }: { role: BusinessPartnerRoleDto }) {
  return (
    <div className={`md-role-card ${role.isActive ? '' : 'md-role-card--inactive'}`}>
      <div className="md-role-card__header">
        <span className="md-badge md-badge--ok">{role.roleLabel}</span>
        {!role.isActive && <span className="md-badge md-badge--warn">Revocado</span>}
      </div>
      {role.notes && <p className="md-role-card__notes">{role.notes}</p>}
      {role.supplierConfig && (
        <div className="md-role-card__config">
          <small>Sustento: {role.supplierConfig.defaultTaxSupportCode ?? '—'}</small>
          <small>Ret. IVA: {role.supplierConfig.defaultRetentionVatCode ?? '—'}</small>
          <small>Ret. Renta: {role.supplierConfig.defaultRetentionIncomeCode ?? '—'}</small>
          {role.supplierConfig.paymentTerms           && <small>Plazo: {role.supplierConfig.paymentTerms}</small>}
          {role.supplierConfig.defaultPaymentMethodCode && <small>Método SRI: {role.supplierConfig.defaultPaymentMethodCode}</small>}
          {role.supplierConfig.isRetentionExempt      && <small className="md-badge md-badge--warn">Exento retención</small>}
        </div>
      )}
      {role.classificationConfig && (
        <div className="md-role-card__config md-role-card__config--crm">
          {role.classificationConfig.supplierCategory  && <small>Categoría: {role.classificationConfig.supplierCategory}</small>}
          {role.classificationConfig.supplierType      && <small>Tipo: {role.classificationConfig.supplierType}</small>}
          {role.classificationConfig.supplierRisk      && <small>Riesgo: {role.classificationConfig.supplierRisk}</small>}
          {role.classificationConfig.supplierRating    && <small>Rating: {role.classificationConfig.supplierRating}</small>}
          {role.classificationConfig.primaryGoodType   && <small>Bien: {role.classificationConfig.primaryGoodType}</small>}
          {role.classificationConfig.supplierSegment   && <small>Segmento: {role.classificationConfig.supplierSegment}</small>}
        </div>
      )}
      {role.carrierConfig?.transportAuthorizationNumber && (
        <div className="md-role-card__config">
          <small>Autorización: {role.carrierConfig.transportAuthorizationNumber}</small>
          {role.carrierConfig.vehicleCapacityTons && (
            <small>Capacidad: {role.carrierConfig.vehicleCapacityTons}t</small>
          )}
        </div>
      )}
      {role.customerConfig && (
        <div className="md-role-card__config md-role-card__config--crm">
          {role.customerConfig.customerCategory       && <small>Categoría: {role.customerConfig.customerCategory}</small>}
          {role.customerConfig.customerSegment        && <small>Segmento: {role.customerConfig.customerSegment}</small>}
          {role.customerConfig.salesZone              && <small>Zona: {role.customerConfig.salesZone}</small>}
          {role.customerConfig.creditRating           && <small>Rating: {role.customerConfig.creditRating}</small>}
          {role.customerConfig.loyaltyTier            && <small>Fidelización: {role.customerConfig.loyaltyTier}</small>}
          {role.customerConfig.preferredInvoiceFormat && <small>Formato factura: {role.customerConfig.preferredInvoiceFormat}</small>}
          {role.customerConfig.customerClassification && <small>Clasificación: {role.customerConfig.customerClassification}</small>}
        </div>
      )}
    </div>
  );
}

// ── Location card ─────────────────────────────────────────────────────────────
function LocationCard({ loc }: { loc: BpLocationDto }) {
  return (
    <div className={`md-location-card ${!loc.isActive ? 'md-location-card--inactive' : ''}`}>
      <div className="md-location-card__header">
        <span className="md-location-card__name">{loc.name}</span>
        {loc.isPrimary && <span className="md-badge md-badge--ok">Principal</span>}
        <span className="md-badge">{loc.typeLabel}</span>
      </div>
      <p className="md-location-card__address">{loc.addressLine}</p>
      {(loc.provinceCode || loc.cantonCode) && (
        <p className="md-location-card__geo">
          {[loc.provinceCode, loc.cantonCode, loc.parishCode].filter(Boolean).join(' · ')}
        </p>
      )}
      {loc.purposes.length > 0 && (
        <div className="md-location-card__purposes">
          {loc.purposes.map((p) => <span key={p} className="md-badge">{p}</span>)}
        </div>
      )}
      {(loc.phone || loc.email) && (
        <p className="md-location-card__contact">
          {loc.phone && <span>📞 {loc.phone}</span>}
          {loc.email && <span>✉ {loc.email}</span>}
        </p>
      )}
    </div>
  );
}

// ── Contact card ──────────────────────────────────────────────────────────────
function ContactCard({ contact }: { contact: BpContactDto }) {
  return (
    <div className={`md-contact-card ${!contact.isActive ? 'md-contact-card--inactive' : ''}`}>
      <div className="md-contact-card__header">
        <span className="md-contact-card__name">{contact.fullName}</span>
        {contact.isPrimary && <span className="md-badge md-badge--ok">Principal</span>}
        <span className="md-badge">{contact.roleLabel}</span>
      </div>
      {contact.position && <p className="md-contact-card__position">{contact.position}</p>}
      <div className="md-contact-card__info">
        {contact.phone  && <span>📞 {contact.phone}</span>}
        {contact.mobile && <span>📱 {contact.mobile}</span>}
        {contact.email  && <span>✉ {contact.email}</span>}
      </div>
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────
type TabId = 'identity' | 'roles' | 'locations' | 'contacts';

export function MasterDataBusinessPartnerDetailPage() {
  const { id }       = useParams<{ id: string }>();
  const navigate     = useNavigate();
  const { options: idTypes } = useSriIdTypes();

  const [bp,        setBp]       = useState<BusinessPartnerDetailDto | null>(null);
  const [locations, setLocations] = useState<BpLocationDto[]>([]);
  const [contacts,  setContacts]  = useState<BpContactDto[]>([]);
  const [loading,   setLoading]  = useState(true);
  const [error,     setError]    = useState<string | null>(null);
  const [activeTab, setActiveTab]= useState<TabId>('identity');

  useEffect(() => {
    if (!id) return;
    setLoading(true);
    Promise.all([
      businessPartnerFacade.getBusinessPartner(id),
      businessPartnerFacade.getLocations(id, false),
      businessPartnerFacade.getContacts(id, false),
    ])
      .then(([bpData, locs, cons]) => {
        setBp(bpData);
        setLocations(locs);
        setContacts(cons);
      })
      .catch(() => setError('No se pudo cargar el BusinessPartner.'))
      .finally(() => setLoading(false));
  }, [id]);

  const activeRoles   = bp?.roles.filter((r) => r.isActive)  ?? [];
  const revokedRoles  = bp?.roles.filter((r) => !r.isActive) ?? [];
  const activeLocs    = locations.filter((l) => l.isActive);
  const activeCons    = contacts.filter((c) => c.isActive);

  const TABS: { id: TabId; label: string; count?: number }[] = [
    { id: 'identity',  label: 'Identidad' },
    { id: 'roles',     label: 'Roles',      count: activeRoles.length },
    { id: 'locations', label: 'Ubicaciones', count: activeLocs.length },
    { id: 'contacts',  label: 'Contactos',  count: activeCons.length },
  ];

  return (
    <ErpPageTemplate
      kicker="MasterData"
      title={bp ? (bp.tradeName?.trim() || bp.legalName) : 'BusinessPartner'}
      subtitle={bp ? `${bp.identificationType} ${bp.identificationNumber}` : ''}
      action={
        <ZHBtn variant="ghost" onClick={() => navigate(-1)}>← Volver</ZHBtn>
      }
    >
      {loading && <p>Cargando…</p>}
      {error   && <ZHPageNotice variant="error" message={error} />}

      {bp && (
        <>
          {/* ── Status badge ────────────────────────────────────────── */}
          <div className="md-detail-status-row">
            <span className={`md-badge ${bp.isActive ? 'md-badge--ok' : 'md-badge--warn'}`}>
              {bp.isActive ? 'Activo' : 'Inactivo'}
            </span>
            {activeRoles.map((r) => (
              <span key={r.id} className="md-badge md-badge--ok">{r.roleLabel}</span>
            ))}
          </div>

          {/* ── Tabs ────────────────────────────────────────────────── */}
          <nav className="md-detail-tabs">
            {TABS.map((tab) => (
              <button key={tab.id} type="button"
                className={`md-detail-tab ${activeTab === tab.id ? 'md-detail-tab--active' : ''}`}
                onClick={() => setActiveTab(tab.id)}>
                {tab.label}
                {tab.count !== undefined && (
                  <span className="md-detail-tab__count">{tab.count}</span>
                )}
              </button>
            ))}
          </nav>

          {/* ── Identity ────────────────────────────────────────────── */}
          {activeTab === 'identity' && (
            <section className="md-detail-section">
              <Row label="Tipo de identificación"
                value={`${bp.identificationType} — ${getSriIdTypeName(bp.identificationType, idTypes)}`} />
              <Row label="Número"        value={bp.identificationNumber} />
              <Row label="Tipo de persona" value={bp.personType} />
              <Row label="Razón social"  value={bp.legalName} />
              <Row label="Nombre comercial" value={bp.tradeName} />
              <Row label="País"          value={bp.countryCode ?? 'EC'} />
              <Row label="Creado"        value={new Date(bp.createdAt).toLocaleDateString()} />
            </section>
          )}

          {/* ── Roles ───────────────────────────────────────────────── */}
          {activeTab === 'roles' && (
            <section className="md-detail-section">
              {bp.roles.length === 0 ? (
                <p className="md-detail-empty-hint">Este BP no tiene roles asignados.</p>
              ) : (
                <>
                  {activeRoles.length > 0 && (
                    <div className="md-roles-grid">
                      {activeRoles.map((r) => <RoleBadge key={r.id} role={r} />)}
                    </div>
                  )}
                  {revokedRoles.length > 0 && (
                    <>
                      <h4 className="md-detail-subheader">Roles revocados</h4>
                      <div className="md-roles-grid md-roles-grid--inactive">
                        {revokedRoles.map((r) => <RoleBadge key={r.id} role={r} />)}
                      </div>
                    </>
                  )}
                </>
              )}
            </section>
          )}

          {/* ── Locations ───────────────────────────────────────────── */}
          {activeTab === 'locations' && (
            <section className="md-detail-section">
              {locations.length === 0 ? (
                <p className="md-detail-empty-hint">No hay ubicaciones registradas.</p>
              ) : (
                <div className="md-locations-grid">
                  {locations.map((l) => <LocationCard key={l.id} loc={l} />)}
                </div>
              )}
            </section>
          )}

          {/* ── Contacts ────────────────────────────────────────────── */}
          {activeTab === 'contacts' && (
            <section className="md-detail-section">
              {contacts.length === 0 ? (
                <p className="md-detail-empty-hint">No hay contactos registrados.</p>
              ) : (
                <div className="md-contacts-grid">
                  {contacts.map((c) => <ContactCard key={c.id} contact={c} />)}
                </div>
              )}
            </section>
          )}
        </>
      )}
    </ErpPageTemplate>
  );
}
