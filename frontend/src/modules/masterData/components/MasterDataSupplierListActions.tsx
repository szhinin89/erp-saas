import { useState, type ReactElement } from "react";
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
  onAddAsCustomer?: (id: string) => void;
  onActivate: (id: string) => void;
  onDisable: (id: string) => void;
};

/**
 * Supplier row: «Gestionar» opens the unified record (/masterdata/business-partners/:id), where edit,
 * SRI configuration, locations, contacts and roles live. Only status and cross-role actions stay here;
 * confirmations remain in the page handlers. Without secondary items there is no menu.
 */
export function MasterDataSupplierListActions({partner: bp, canUpdate, canDisable, saving,
  onAddAsCustomer, onActivate, onDisable}: Props) {
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();
  const {t} = useI18n();
  const identity = `${bp.legalName} (${bp.identificationNumber})`;
  const label = (action: string) => t(`masterdata.suppliers.listAction.${action}`);

  const items: ReactElement[] = [];
  if (canUpdate && bp.canAssignAsCustomer && onAddAsCustomer)
    items.push(<ZHLineActionItem key="assign" label={label("assign")} disabled={saving} onClick={() => onAddAsCustomer(bp.id)} />);
  if (canUpdate && !bp.isActive)
    items.push(<ZHLineActionItem key="activate" label={t("masterdata.suppliers.action.activate")} disabled={saving} onClick={() => onActivate(bp.id)} />);
  if (canDisable && bp.isActive)
    items.push(<ZHLineActionItem key="deactivate" label={t("masterdata.suppliers.action.deactivate")} tone="danger" disabled={saving} onClick={() => onDisable(bp.id)} />);

  return (
    <div className="md-supplier-actions">
      <ZHBtn variant="ghost" size="sm" aria-label={`${label("manage")}: ${identity}`}
        onClick={() => navigate(`/masterdata/business-partners/${bp.id}`)}>{label("manage")}</ZHBtn>
      {items.length > 0 && (
        <ZHLineAction kind="menu" icon={"⋯"} label={label("more")}
          ariaLabel={`${label("more")}: ${identity}`} menuLabel={`${label("more")}: ${identity}`}
          className="md-supplier-actions__more" menuClassName="md-supplier-actions-menu"
          portal menuAlign="end" open={open} onOpenChange={setOpen} closeOnSelect>
          <div className="md-supplier-actions-menu__identity" role="presentation">{identity}</div>
          {items}
        </ZHLineAction>
      )}
    </div>
  );
}
