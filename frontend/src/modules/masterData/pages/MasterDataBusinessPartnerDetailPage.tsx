import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { businessPartnerFacade } from '../api/businessPartnerFacade';
import { useSriIdTypes, getSriIdTypeName } from '../api/useSriIdTypes';
import type { BusinessPartnerDto } from '../types/businessPartner.types';
import './masterdata-pages.css';

function DetailRow({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="md-detail-row">
      <span className="md-detail-label">{label}</span>
      <span className="md-detail-value">{value ?? <em className="md-detail-empty">—</em>}</span>
    </div>
  );
}

export function MasterDataBusinessPartnerDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [bp, setBp]           = useState<BusinessPartnerDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError]     = useState<string | null>(null);
  const { options: idTypes }  = useSriIdTypes();

  useEffect(() => {
    if (!id) return;
    setLoading(true);
    businessPartnerFacade
      .getBusinessPartner(id)
      .then(setBp)
      .catch(() => setError('No se pudo cargar el BusinessPartner.'))
      .finally(() => setLoading(false));
  }, [id]);

  return (
    <ErpPageTemplate
      kicker="MasterData"
      title={bp ? (bp.tradeName?.trim() || bp.legalName) : 'BusinessPartner'}
      subtitle={bp ? `${bp.identificationType} ${bp.identificationNumber}` : ''}
      action={
        <ZHBtn variant="ghost" onClick={() => navigate(-1)}>
          Volver
        </ZHBtn>
      }
    >
      {loading && <p>Cargando…</p>}
      {error   && <ZHPageNotice variant="error" message={error} />}

      {bp && (
        <>
          <section className="md-detail-section">
            <h3 className="md-detail-section-title">Identificación</h3>
            <DetailRow label="Tipo"   value={`${bp.identificationType} — ${getSriIdTypeName(bp.identificationType, idTypes)}`} />
            <DetailRow label="Número" value={bp.identificationNumber} />
            <DetailRow label="Razón social" value={bp.legalName} />
            <DetailRow label="Nombre comercial" value={bp.tradeName} />
            <DetailRow label="País"         value={bp.countryCode ?? 'EC'} />
            <DetailRow label="Email"        value={bp.email} />
            <DetailRow label="Teléfono"     value={bp.phone} />
            <DetailRow label="Estado"       value={bp.isActive ? 'Activo' : 'Inactivo'} />
          </section>

          <section className="md-detail-section">
            <h3 className="md-detail-section-title">Roles</h3>
            <div className="md-detail-roles">
              {bp.isCustomer && <span className="md-badge md-badge--ok">Cliente</span>}
              {bp.isSupplier && <span className="md-badge md-badge--ok">Proveedor</span>}
              {!bp.isCustomer && !bp.isSupplier && (
                <span className="md-badge md-badge--warn">Sin rol asignado</span>
              )}
            </div>
          </section>

          {bp.isCustomer && (
            <section className="md-detail-section">
              <h3 className="md-detail-section-title">Perfil cliente</h3>
              <DetailRow label="Vínculo legacy"  value={bp.legacyCustomerId ?? 'Sin vínculo'} />
              <DetailRow label="Notas internas"  value={bp.customerNotes} />
            </section>
          )}

          {bp.isSupplier && (
            <section className="md-detail-section">
              <h3 className="md-detail-section-title">Perfil proveedor SRI</h3>
              <DetailRow label="Sustento tributario" value={bp.defaultTaxSupportCode} />
              <DetailRow label="Ret. IVA"            value={bp.defaultRetentionVatCode} />
              <DetailRow label="Ret. Renta"          value={bp.defaultRetentionIncomeCode} />
              <DetailRow label="Condición de pago"   value={bp.supplierPaymentTerms} />
              <DetailRow label="Vínculo legacy"      value={bp.legacySupplierId ?? 'Sin vínculo'} />
            </section>
          )}
        </>
      )}
    </ErpPageTemplate>
  );
}
