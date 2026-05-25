import { useNavigate } from 'react-router-dom';
import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { useMasterDataSuppliersPage } from './useMasterDataSuppliersPage';
import { MasterDataBpFormModal } from './MasterDataBpFormModal';
import { MasterDataCompanySettingsModal } from './MasterDataCompanySettingsModal';
import { MasterDataSupplierProfileModal } from './MasterDataSupplierProfileModal';
import './masterdata-pages.css';

export function MasterDataSuppliersPage() {
  const page = useMasterDataSuppliersPage();
  const navigate = useNavigate();

  if (!page.canView) {
    return <NoAccessPage title="Proveedores (MasterData)" />;
  }

  return (
    <ErpPageTemplate
      kicker="MasterData"
      title="Proveedores"
      subtitle="Gestión de proveedores — fuente canónica BusinessPartner."
      action={
        page.canCreate ? (
          <ZHBtn variant="primary" onClick={page.openCreate}>
            Nuevo proveedor MD
          </ZHBtn>
        ) : undefined
      }
    >
      {page.listError   && <ZHPageNotice variant="error" message="Error al cargar"    detail={page.listError}   />}
      {page.inlineError && <ZHPageNotice variant="error" message="Error en la acción" detail={page.inlineError} />}

      <div className="md-page-toolbar">
        <ZHField label="Buscar">
          <input
            className="zh-input"
            value={page.search}
            onChange={(e) => page.setSearch(e.target.value)}
            placeholder="Nombre o RUC…"
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
              <th>RUC / ID</th>
              <th>Razón social</th>
              <th>Legacy Supplier</th>
              <th>Estado</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {page.loading && (
              <tr><td colSpan={5}>Cargando…</td></tr>
            )}
            {!page.loading && page.suppliers.length === 0 && (
              <tr><td colSpan={5}>Sin registros.</td></tr>
            )}
            {page.suppliers.map((bp) => (
              <tr key={bp.id}>
                <td className="mono">{bp.identificationNumber}</td>
                <td>{bp.tradeName?.trim() || bp.legalName}</td>
                <td>
                  {bp.legacySupplierId ? (
                    <span className="md-badge md-badge--ok">Vinculado</span>
                  ) : (
                    <span className="md-badge md-badge--warn">Sin vínculo</span>
                  )}
                </td>
                <td>{bp.isActive ? 'Activo' : 'Inactivo'}</td>
                <td className="md-actions">
                  <ZHBtn variant="ghost" size="sm" onClick={() => navigate(`/masterdata/business-partners/${bp.id}`)}>
                    Ver
                  </ZHBtn>
                  {page.canUpdate && (
                    <ZHBtn variant="ghost" size="sm" onClick={() => page.openEdit(bp)}>
                      Editar
                    </ZHBtn>
                  )}
                  {page.canUpdate && bp.isSupplier && (
                    <ZHBtn variant="ghost" size="sm" onClick={() => page.openSupplierProfile(bp)}>
                      SRI
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
                      onClick={() => void page.activateSupplier(bp.id)}
                    >
                      Activar
                    </ZHBtn>
                  )}
                  {page.canUpdate && !bp.isCustomer && (
                    <ZHBtn
                      variant="ghost"
                      size="sm"
                      disabled={page.saving}
                      onClick={() => void page.addAsCustomer(bp.id)}
                      title="Agregar también como cliente"
                    >
                      + Cliente
                    </ZHBtn>
                  )}
                  {page.canDisable && bp.isActive && (
                    <ZHBtn
                      variant="ghost"
                      size="sm"
                      disabled={page.saving}
                      onClick={() => void page.disableSupplier(bp.id)}
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

      {page.totalPages > 1 && (
        <div className="md-pagination">
          <ZHBtn variant="ghost" size="sm" disabled={page.page <= 1} onClick={() => page.setPage(page.page - 1)}>
            ‹ Anterior
          </ZHBtn>
          <span className="md-pagination-info">
            Pág. {page.page} / {page.totalPages} ({page.totalCount} registros)
          </span>
          <ZHBtn variant="ghost" size="sm" disabled={page.page >= page.totalPages} onClick={() => page.setPage(page.page + 1)}>
            Siguiente ›
          </ZHBtn>
        </div>
      )}


      {page.modalOpen && (
        <MasterDataBpFormModal
          title="Nuevo BusinessPartner (proveedor)"
          saving={page.saving}
          error={page.modalError}
          defaultAsSupplier
          onClose={page.closeCreate}
          onSubmit={(body) => void page.createSupplier(body)}
        />
      )}

      {page.editBp && (
        <MasterDataBpFormModal
          mode="edit"
          title="Editar BusinessPartner"
          saving={page.saving}
          error={page.modalError}
          initialValues={{
            identificationType:   page.editBp.identificationType,
            identificationNumber: page.editBp.identificationNumber,
            legalName:            page.editBp.legalName,
            tradeName:            page.editBp.tradeName,
            email:                page.editBp.email,
            phone:                page.editBp.phone,
          }}
          onClose={page.closeEdit}
          onUpdate={(body) => void page.updateSupplier(page.editBp!.id, body)}
        />
      )}

      {page.settingsBp && page.canConfigure && (
        <MasterDataCompanySettingsModal
          partner={page.settingsBp}
          initialSettings={page.settingsData}
          saving={page.saving}
          error={page.modalError}
          onClose={page.closeSettings}
          onSave={(payload) => void page.saveCompanySettings(page.settingsBp!.id, payload)}
        />
      )}

      {page.supplierProfileBp && (
        <MasterDataSupplierProfileModal
          partner={page.supplierProfileBp}
          saving={page.saving}
          error={page.modalError}
          onClose={page.closeSupplierProfile}
          onSave={(body) => void page.saveSupplierProfile(page.supplierProfileBp!.id, body)}
        />
      )}
    </ErpPageTemplate>
  );
}
