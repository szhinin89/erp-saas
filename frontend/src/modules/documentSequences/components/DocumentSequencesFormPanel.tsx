import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHField, ZHGrid, ZHBtn } from "../../../components/zh/ZHForm";
import { ZhNumberInput, ZhTextInput } from "../../../components/zh/inputs";
import type { DocumentSequencesPageContext } from "../hooks/useDocumentSequencesPage";

type Props = Pick<
  DocumentSequencesPageContext,
  | "editingRow"
  | "selectedEmissionPoint"
  | "saving"
  | "saveError"
  | "register"
  | "errors"
  | "closePanel"
  | "save"
>;

export function DocumentSequencesFormPanel({
  editingRow,
  selectedEmissionPoint,
  saving,
  saveError,
  register,
  errors,
  closePanel,
  save,
}: Props) {
  if (!editingRow || !selectedEmissionPoint) return null;

  return (
    <div>
      <div className="cfg-panel-hd">
        <span className="material-symbols-outlined cfg-panel-hd__icon">
          format_list_numbered
        </span>
        <div>
          <p className="cfg-panel-hd__title">
            Configurar secuencia — {editingRow.docTypeName}
          </p>
          <p className="cfg-panel-hd__sub">
            {selectedEmissionPoint.establishmentCode}-{selectedEmissionPoint.code} ·{" "}
            {selectedEmissionPoint.establishmentName}
          </p>
        </div>
      </div>

      {saveError && (
        <div className="cfg-panel-error">
          <ZHPageNotice variant="error" message="Error" detail={saveError} />
        </div>
      )}

      <form
        onSubmit={(e) => {
          e.preventDefault();
          void save();
        }}
        noValidate
      >
        <div className="cfg-panel-body">
          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">
                  info
                </span>
                <span className="pg-section-label">
                  Punto de emisión y tipo de documento
                </span>
              </div>
            </div>
            <div className="pg-section-body">
              <ZHGrid cols={2}>
                <ZHField label="Establecimiento">
                  <ZhTextInput
                    className="zh-input"
                    readOnly
                    value={`${selectedEmissionPoint.establishmentCode} — ${selectedEmissionPoint.establishmentName}`}
                  />
                </ZHField>
                <ZHField label="Punto de emisión">
                  <ZhTextInput
                    className="zh-input mono"
                    readOnly
                    value={selectedEmissionPoint.code}
                  />
                </ZHField>
                <ZHField label="Tipo de documento">
                  <ZhTextInput
                    className="zh-input"
                    readOnly
                    value={editingRow.docTypeName}
                  />
                </ZHField>
                <ZHField label="Código SRI">
                  <ZhTextInput
                    className="zh-input mono"
                    readOnly
                    value={editingRow.docTypeCode}
                  />
                </ZHField>
              </ZHGrid>
            </div>
          </div>

          <div className="pg-section">
            <div className="pg-section-header">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">
                  tag
                </span>
                <span className="pg-section-label">Número inicial</span>
              </div>
            </div>
            <div className="pg-section-body">
              <ZHField
                label="Siguiente secuencial"
                required
                error={errors.nextNumber?.message}
                hint="Entero positivo, máximo 999999999 (9 dígitos)."
              >
                <ZhNumberInput
                  className="zh-input mono"
                  positiveOnly
                  maxLength={9}
                  disabled={saving}
                  {...register("nextNumber")}
                />
              </ZHField>
            </div>
          </div>
        </div>

        <div className="pg-actions-bar">
          <div className="pg-actions-buttons">
            <ZHBtn
              variant="ghost"
              type="button"
              onClick={closePanel}
              disabled={saving}
            >
              Cancelar
            </ZHBtn>
            <ZHBtn variant="primary" type="submit" disabled={saving}>
              {saving ? "Guardando…" : "Guardar"}
            </ZHBtn>
          </div>
        </div>
      </form>
    </div>
  );
}
