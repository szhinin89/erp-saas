import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHLineAction } from "../../../components/zh/ZHLineAction";
import { ZHLineActionItem } from "../../../components/zh/ZHLineActionItem";
import { useI18n } from "../../../i18n/i18n";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";

type Props = {
  partner: BusinessPartnerSummaryDto;
  canUpdate: boolean;
  canDisable: boolean;
  saving: boolean;
  onEdit: (bp: BusinessPartnerSummaryDto) => void;
  onSupplierProfile?: (bp: BusinessPartnerSummaryDto) => void;
  onAddAsCustomer?: (id: string) => void;
  onActivate: (id: string) => void;
  onDisable: (id: string) => void;
};

/** Supplier composition of the official DS controls; existing callbacks retain confirmations. */
export function MasterDataSupplierListActions({partner: bp, canUpdate, canDisable, saving,
  onEdit, onSupplierProfile, onAddAsCustomer, onActivate, onDisable}: Props) {
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();
  const {t} = useI18n();
  const identity = `${bp.legalName} (${bp.identificationNumber})`;
  const label = (action: string) => t(`masterdata.suppliers.listAction.${action}`);
  return (
    <div className="md-supplier-actions">
      <ZHBtn variant="ghost" size="sm" aria-label={`${t("common.view")}: ${identity}`}
        onClick={() => navigate(`/masterdata/business-partners/${bp.id}`)}>{t("common.view")}</ZHBtn>
      {(canUpdate || (canDisable && bp.isActive)) && (
        <ZHLineAction kind="menu" icon={"\u22ef"} label={label("more")}
          ariaLabel={`${label("more")}: ${identity}`} menuLabel={`${label("more")}: ${identity}`}
          className="md-supplier-actions__more" menuClassName="md-supplier-actions-menu"
          portal menuAlign="end" open={open} onOpenChange={setOpen} closeOnSelect>
          <div className="md-supplier-actions-menu__identity" role="presentation">{identity}</div>
          {canUpdate && <ZHLineActionItem label={label("edit")} onClick={() => onEdit(bp)} />}
          {canUpdate && onSupplierProfile && <ZHLineActionItem label={label("sri")} onClick={() => onSupplierProfile(bp)} />}
          {canUpdate && onAddAsCustomer && <ZHLineActionItem label={label("assign")} disabled={saving} onClick={() => onAddAsCustomer(bp.id)} />}
          {canUpdate && !bp.isActive && <ZHLineActionItem label={t("masterdata.suppliers.action.activate")} disabled={saving} onClick={() => onActivate(bp.id)} />}
          {canDisable && bp.isActive && <ZHLineActionItem label={t("masterdata.suppliers.action.deactivate")} tone="danger" disabled={saving} onClick={() => onDisable(bp.id)} />}
        </ZHLineAction>
      )}
    </div>
  );
}
