import { NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { useNavigate } from 'react-router-dom';
import { useMasterDataCustomersPage } from './useMasterDataCustomersPage';
import { MasterDataBpFormModal } from './MasterDataBpFormModal';
import { MasterDataCompanySettingsModal } from './MasterDataCompanySettingsModal';
import { MasterDataCustomerNotesModal } from './MasterDataCustomerNotesModal';
import './masterdata-pages.css';

export function MasterDataCustomersPage() {
  const page = useMasterDataCustomersPage();
  const navigate = useNavigate();

  if (!page.canView) {
    return <NoAccessPage title="Clientes (MasterData)" />;
  }

  return (
    <ErpPageTemplate
      kicker="MasterData"
      title="Clientes"
      subtitle="Gestión de clientes — fuente canónica BusinessPartner."
      action={
        page.canCreate ? (
          <ZHBtn variant="primary" onClick={page.openCreate}>
            Nuevo cliente MD
          </ZHBtn>
        ) : undefined
      }
    >
      {/* Errores de carga de lista y acciones inline (disable/activate) */}
      {page.listError   && <ZHPageNotice variant="error" message="Error al cargar"   detail={page.listError}   />}
      {page.inlineError && <ZHPageNotice variant="error" message="Error en la acción" detail={page.inlineError} />}

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
              <tr><td colSpan={5}>Cargando…</td></tr>
            )}
            {!page.loading && page.customers.length === 0 && (
              <tr><td colSpan={5}>Sin registros.</td></tr>
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
                  <ZHBtn variant="ghost" size="sm" onClick={() => navigate(`/masterdata/business-partners/${bp.id}`)}>
                    Ver
                  </ZHBtn>
                  {page.canUpdate && (
                    <ZHBtn variant="ghost" size="sm" onClick={() => page.openEdit(bp)}>
                      Editar
                    </ZHBtn>
                  )}
                  {page.canUpdate && (
                    <ZHBtn variant="ghost" size="sm" onClick={() => page.openNotes(bp)}>
                      Notas
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
                  {page.canUpdate && !bp.isSupplier && (
                    <ZHBtn
                      variant="ghost"
                      size="sm"
                      disabled={page.saving}
                      onClick={() => void page.addAsSupplier(bp.id)}
                      title="Agregar también como proveedor"
                    >
                      + Proveedor
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
          title="Nuevo cliente"
          saving={page.saving}
          error={page.modalError}
          roleToAssign="customer"
          onClose={page.closeCreate}
          onSubmit={(body) => void page.createCustomer(body)}
          onAssignRole={(id) => page.assignAsCustomer(id)}
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
          currentIsCustomer={page.editBp.isCustomer}
          currentIsSupplier={page.editBp.isSupplier}
          onClose={page.closeEdit}
          onUpdate={(body) => void page.updateCustomer(page.editBp!.id, body)}
        />
      )}

      {page.notesBp && (
        <MasterDataCustomerNotesModal
          partner={page.notesBp}
          saving={page.saving}
          error={page.modalError}
          onClose={page.closeNotes}
          onSave={(notes) => void page.saveNotes(page.notesBp!.id, notes)}
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
    </ErpPageTemplate>
  );
}
