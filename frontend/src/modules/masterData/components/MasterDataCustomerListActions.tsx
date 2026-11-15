import { useRef, useState, type ReactElement } from "react";
import { useNavigate } from "react-router-dom";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHLineAction } from "../../../components/zh/ZHLineAction";
import { ZHLineActionItem } from "../../../components/zh/ZHLineActionItem";
import { useI18n } from "../../../i18n/i18n";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";
import { bpDetailPath } from "./bpDetailTabs";
import { useReturnFocusAfterDialog } from "./useReturnFocusAfterDialog";

type Props = {
  partner: BusinessPartnerSummaryDto;
  canUpdate: boolean;
  canDisable: boolean;
  saving: boolean;
  onAddAsSupplier?: (id: string) => void;
  onRevokeRole?: (id: string) => void;
  onActivate: (id: string) => void;
  onDisable: (id: string) => void;
};

/** Customer row: «Administrar cliente» opens the customer-context record (/customers/:id), where edit,
 * commercial classification, sales terms, locations, contacts and roles live. Only status and cross-role
 * actions stay in the «Más acciones» menu (portal); each item keeps the exact permission/state condition
 * and callback, and confirmations remain in the page handlers. Without secondary items there is no menu. */
export function MasterDataCustomerListActions({partner: bp, canUpdate, canDisable,
  saving, onAddAsSupplier, onRevokeRole, onActivate, onDisable}: Props) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const navigate = useNavigate();
  const {t} = useI18n();
  const returnFocus = useReturnFocusAfterDialog(
    () => rootRef.current?.querySelector<HTMLButtonElement>(".zh-line-action-menu__trigger"));
  const identity = `${bp.legalName} (${bp.identificationNumber})`;
  const label = (action: string) => t(`masterdata.customers.listAction.${action}`);
  const run = (action: () => void) => () => { returnFocus(); action(); };

  const items: ReactElement[] = [];
  // canAssignAsSupplier (backend): activo y sin rol Supplier; el POST revalida.
  if (canUpdate && bp.canAssignAsSupplier && onAddAsSupplier)
    items.push(<ZHLineActionItem key="assign" label={label("assign")} disabled={saving} onClick={run(() => onAddAsSupplier(bp.id))} />);
  if (canDisable && bp.isActive && onRevokeRole)
    items.push(<ZHLineActionItem key="revoke" label={label("revoke")} tone="danger" disabled={saving} onClick={run(() => onRevokeRole(bp.id))} />);
  if (canUpdate && !bp.isActive)
    items.push(<ZHLineActionItem key="activate" label={label("activate")} disabled={saving} onClick={run(() => onActivate(bp.id))} />);
  if (canDisable && bp.isActive)
    items.push(<ZHLineActionItem key="deactivate" label={label("deactivate")} tone="danger" disabled={saving} onClick={run(() => onDisable(bp.id))} />);

  return (
    <div className="md-customer-actions" ref={rootRef}>
      <ZHBtn variant="ghost" size="sm" aria-label={`${label("manage")}: ${identity}`}
        onClick={() => navigate(bpDetailPath("customer", bp.id))}>{label("manage")}</ZHBtn>
      {items.length > 0 && (
        <ZHLineAction kind="menu" icon={"⋯"} label={label("more")}
          ariaLabel={`${label("more")}: ${identity}`} menuLabel={`${label("more")}: ${identity}`}
          className="md-customer-actions__more" menuClassName="md-customer-actions-menu"
          portal menuAlign="end" open={open} onOpenChange={setOpen} closeOnSelect>
          <div className="md-customer-actions-menu__identity" role="presentation">{identity}</div>
          {items}
        </ZHLineAction>
      )}
    </div>
  );
}
