import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { useMasterDataCustomersPage } from './useMasterDataCustomersPage';
import { MasterDataBpFormModal } from './MasterDataBpFormModal';
import { MasterDataCompanySettingsModal } from './MasterDataCompanySettingsModal';
import './masterdata-pages.css';

export function MasterDataCustomersPage() {
  const page = useMasterDataCustomersPage();

  if (!page.canView) {
    return <NoAccessPage title="Clientes (MasterData)" />;
  }

  return (
    <ErpPageTemplate
      kicker="MasterData"
      title="Clientes (MasterData)"
      subtitle="Fuente canónica BusinessPartner + CustomerProfile. CRUD operacional legacy sigue en Ventas → Clientes."
      action={
        page.canCreate ? (
          <ZHBtn variant="primary" onClick={page.openCreate}>
            Nuevo cliente MD
          </ZHBtn>
        ) : undefined
      }
    >
      {page.error && <ZHPageNotice variant="error" message="Error" detail={page.error} />}

      <div className="md-page-toolbar">
        <ZHField label="Buscar">
          <input
            className="zh-input"
            value={page.search}
            onChange={(e) => page.setSearch(e.target.value)}
            placeholder="Nombre o identificación…"
          />
        </ZHField>
        <label className="md-page-check">
          <input
            type="checkbox"
            checked={page.showInactive}
            onChange={(e) => page.setShowInactive(e.target.checked)}
          />
          Incluir inactivos
        </label>
      </div>

      <div className="md-table-wrap">
        <table className="md-table">
          <thead>
            <tr>
              <th>Identificación</th>
              <th>Razón social</th>
              <th>Legacy Customer</th>
              <th>Estado</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {page.loading && (
              <tr>
                <td colSpan={5}>Cargando…</td>
              </tr>
            )}
            {!page.loading && page.customers.length === 0 && (
              <tr>
                <td colSpan={5}>Sin registros.</td>
              </tr>
            )}
            {page.customers.map((bp) => (
              <tr key={bp.id}>
                <td className="mono">{bp.identificationNumber}</td>
                <td>{bp.tradeName?.trim() || bp.legalName}</td>
                <td>
                  {bp.legacyCustomerId ? (
                    <span className="md-badge md-badge--ok">Vinculado</span>
                  ) : (
                    <span className="md-badge md-badge--warn" title="No seleccionable en facturas hasta dual-write">
                      Sin vínculo
                    </span>
                  )}
                </td>
                <td>{bp.isActive ? 'Activo' : 'Inactivo'}</td>
                <td className="md-actions">
                  {page.canUpdate && (
                    <ZHBtn variant="ghost" size="sm" onClick={() => page.openEdit(bp)}>
                      Editar
                    </ZHBtn>
                  )}
                  {page.canConfigure && (
                    <ZHBtn variant="ghost" size="sm" onClick={() => void page.openSettings(bp)}>
                      Empresa
                    </ZHBtn>
                  )}
                  {page.canUpdate && !bp.isActive && (
                    <ZHBtn
                      variant="ghost"
                      size="sm"
                      disabled={page.saving}
                      onClick={() => void page.activateCustomer(bp.id)}
                    >
                      Activar
                    </ZHBtn>
                  )}
                  {page.canDisable && bp.isActive && (
                    <ZHBtn
                      variant="ghost"
                      size="sm"
                      disabled={page.saving}
                      onClick={() => void page.disableCustomer(bp.id)}
                    >
                      Desactivar
                    </ZHBtn>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <p className="md-legacy-hint">
        Listado legacy sin cambios: <a href="/sales/customers">/sales/customers</a>
      </p>

      {page.modalOpen && (
        <MasterDataBpFormModal
          title="Nuevo BusinessPartner (cliente)"
          saving={page.saving}
          defaultAsCustomer
          onClose={() => page.setModalOpen(false)}
          onSubmit={(body) => void page.createCustomer(body)}
        />
      )}

      {page.editBp && (
        <MasterDataBpFormModal
          mode="edit"
          title="Editar BusinessPartner"
          saving={page.saving}
          initialValues={{
            identificationType: page.editBp.identificationType,
            identificationNumber: page.editBp.identificationNumber,
            legalName: page.editBp.legalName,
            tradeName: page.editBp.tradeName,
            email: page.editBp.email,
            phone: page.editBp.phone,
          }}
          onClose={() => page.setEditBp(null)}
          onUpdate={(body) => void page.updateCustomer(page.editBp!.id, body)}
        />
      )}

      {page.settingsBp && page.canConfigure && (
        <MasterDataCompanySettingsModal
          partner={page.settingsBp}
          initialSettings={page.settingsData}
          saving={page.saving}
          onClose={page.closeSettings}
          onSave={(payload) => void page.saveCompanySettings(page.settingsBp!.id, payload)}
        />
      )}
    </ErpPageTemplate>
  );
}
