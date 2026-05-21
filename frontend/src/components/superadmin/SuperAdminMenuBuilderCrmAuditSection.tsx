import { ZHBtn } from '../zh/ZHForm';
import { auditActionBadge, parseAuditLine } from './superAdminMenuBuilderUtils';

export type SuperAdminMenuBuilderCrmAuditSectionProps = {
  auditLines: string[];
  setAuditLines: (lines: string[]) => void;
  importWorkspaceInputRef: React.RefObject<HTMLInputElement | null>;
  onExportAudit: () => void;
  onExportWorkspace: () => void;
  onTriggerImport: () => void;
  onImportFile: (file: File) => void;
};

export function SuperAdminMenuBuilderCrmAuditSection({
  auditLines,
  setAuditLines,
  importWorkspaceInputRef,
  onExportAudit,
  onExportWorkspace,
  onTriggerImport,
  onImportFile,
}: SuperAdminMenuBuilderCrmAuditSectionProps) {
  return (
    <section className="pg-section" aria-labelledby="menu-plan-audit-heading">
      <div className="pg-section-header">
        <div className="pg-section-header-left">
          <span className="material-symbols-outlined pg-section-icon">history_edu</span>
          <h3 id="menu-plan-audit-heading" className="pg-section-label">
            Auditoría
          </h3>
        </div>
        <div className="smb-audit-actions">
          <ZHBtn variant="ghost" size="sm" type="button" onClick={onExportAudit} disabled={!auditLines.length} aria-label="Exportar auditoría">
            Exportar auditoría
          </ZHBtn>
          <ZHBtn variant="ghost" size="sm" type="button" onClick={onExportWorkspace} aria-label="Exportar workspace completo">
            Exportar workspace
          </ZHBtn>
          <ZHBtn variant="ghost" size="sm" type="button" onClick={onTriggerImport} aria-label="Importar workspace completo">
            Importar workspace
          </ZHBtn>
          <ZHBtn variant="ghost" size="sm" type="button" onClick={() => setAuditLines([])} disabled={!auditLines.length} aria-label="Limpiar historial de auditoría">
            Limpiar
          </ZHBtn>
        </div>
      </div>
      <input
        ref={importWorkspaceInputRef}
        type="file"
        accept="application/json,.json"
        className="menu-plan-composer__hiddenFileInput"
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) void onImportFile(file);
          e.currentTarget.value = '';
        }}
      />
      {auditLines.length === 0 ? (
        <p className="subtle smb-audit-empty">Las acciones (guardado automático, recargas, simulación…) aparecerán aquí.</p>
      ) : (
        <div className="smb-audit-scroll">
          <table className="table">
            <thead>
              <tr>
                <th className="pg-th-w-110">Timestamp</th>
                <th className="pg-th-w-110">Usuario</th>
                <th className="pg-th-w-100">Acción</th>
                <th>Detalles</th>
              </tr>
            </thead>
            <tbody>
              {auditLines.map((line, i) => {
                const { timestamp, user, action, details } = parseAuditLine(line);
                return (
                  <tr key={`${i}-${line.slice(0, 16)}`}>
                    <td>
                      <span className="subtle mono smb-audit-ts">{timestamp}</span>
                    </td>
                    <td>
                      <span className="smb-audit-user">{user}</span>
                    </td>
                    <td>
                      <span className={`badge badge--${auditActionBadge(action)} badge--upper smb-audit-badge`}>{action}</span>
                    </td>
                    <td className="smb-audit-details">{details}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
