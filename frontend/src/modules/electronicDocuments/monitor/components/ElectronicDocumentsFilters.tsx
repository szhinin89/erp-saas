import { ZHField } from "../../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import {
  ZhTextInput,
  ZhDateInput,
  ZhSelect,
} from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
import {
  ELECTRONIC_DOCUMENT_STATES,
  ELECTRONIC_DOCUMENT_TYPES,
  ELECTRONIC_DOCUMENT_ENVIRONMENTS,
} from "../utils/enumOptions";
import "./electronic-documents-monitor.css";

type Props = {
  dateFrom: string;
  dateTo: string;
  state: string;
  documentType: string;
  environment: string;
  search: string;
  onDateFromChange: (v: string) => void;
  onDateToChange: (v: string) => void;
  onStateChange: (v: string) => void;
  onDocumentTypeChange: (v: string) => void;
  onEnvironmentChange: (v: string) => void;
  onSearchChange: (v: string) => void;
  onClear: () => void;
  disabled: boolean;
};

export function ElectronicDocumentsFilters({
  dateFrom,
  dateTo,
  state,
  documentType,
  environment,
  search,
  onDateFromChange,
  onDateToChange,
  onStateChange,
  onDocumentTypeChange,
  onEnvironmentChange,
  onSearchChange,
  onClear,
  disabled,
}: Props) {
  const { t } = useI18n();

  return (
    <ZHFilterBar
      onClear={onClear}
      clearLabel={t("electronicDocuments.monitor.filters.clear")}
      disabled={disabled}
    >
      <div className="zh-filterbar__field">
        <ZHField
          label={t("electronicDocuments.monitor.filters.dateFrom")}
          density="compact"
        >
          <ZhDateInput
            className="zh-input"
            value={dateFrom}
            disabled={disabled}
            onChange={(e) => onDateFromChange(e.target.value)}
          />
        </ZHField>
      </div>
      <div className="zh-filterbar__field">
        <ZHField
          label={t("electronicDocuments.monitor.filters.dateTo")}
          density="compact"
        >
          <ZhDateInput
            className="zh-input"
            value={dateTo}
            disabled={disabled}
            onChange={(e) => onDateToChange(e.target.value)}
          />
        </ZHField>
      </div>
      <div className="zh-filterbar__field">
        <ZHField
          label={t("electronicDocuments.monitor.filters.state")}
          density="compact"
        >
          <ZhSelect
            className="zh-input"
            value={state}
            disabled={disabled}
            onChange={(e) => onStateChange(e.target.value)}
          >
            <option value="">
              {t("electronicDocuments.monitor.filters.allStates")}
            </option>
            {ELECTRONIC_DOCUMENT_STATES.map((s) => (
              <option key={s} value={s}>
                {t(`electronicDocuments.monitor.state.${s}`)}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </div>
      <div className="zh-filterbar__field">
        <ZHField
          label={t("electronicDocuments.monitor.filters.documentType")}
          density="compact"
        >
          <ZhSelect
            className="zh-input"
            value={documentType}
            disabled={disabled}
            onChange={(e) => onDocumentTypeChange(e.target.value)}
          >
            <option value="">
              {t("electronicDocuments.monitor.filters.allDocumentTypes")}
            </option>
            {ELECTRONIC_DOCUMENT_TYPES.map((dt) => (
              <option key={dt} value={dt}>
                {t(`electronicDocuments.monitor.documentType.${dt}`)}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </div>
      <div className="zh-filterbar__field">
        <ZHField
          label={t("electronicDocuments.monitor.filters.environment")}
          density="compact"
        >
          <ZhSelect
            className="zh-input"
            value={environment}
            disabled={disabled}
            onChange={(e) => onEnvironmentChange(e.target.value)}
          >
            <option value="">
              {t("electronicDocuments.monitor.filters.allEnvironments")}
            </option>
            {ELECTRONIC_DOCUMENT_ENVIRONMENTS.map((env) => (
              <option key={env} value={env}>
                {t(
                  env === "2"
                    ? "settings.electronicInvoicing.sri.environmentProd"
                    : "settings.electronicInvoicing.sri.environmentTest",
                )}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </div>
      <div className="zh-filterbar__field zh-filterbar__field--grow">
        <ZHField
          label={t("electronicDocuments.monitor.filters.search")}
          density="compact"
        >
          <ZhTextInput
            className="zh-input"
            placeholder={t(
              "electronicDocuments.monitor.filters.searchPlaceholder",
            )}
            value={search}
            disabled={disabled}
            onChange={(e) => onSearchChange(e.target.value)}
          />
        </ZHField>
      </div>
    </ZHFilterBar>
  );
}
