import { useRef, useState, type ReactElement } from "react";

import { ZHLineAction } from "../../../components/zh/ZHLineAction";
import { ZHLineActionItem } from "../../../components/zh/ZHLineActionItem";
import { useReturnFocusAfterDialog } from "../../../hooks/useReturnFocusAfterDialog";
import { useI18n } from "../../../i18n/i18n";
import type { SalesReceivableDto } from "../api/receivableService";

type Props = {
  receivable: SalesReceivableDto;
  canReverse: boolean;
  canSeeAdvances: boolean;
  onCollect: (receivable: SalesReceivableDto) => void;
  onReverse: (receivable: SalesReceivableDto) => void;
  onAdvances: (receivable: SalesReceivableDto) => void;
};

/**
 * Menú de acciones de una fila de CxC (menú oficial `ZHLineAction` en portal). Solo agrupa: las
 * condiciones de visibilidad llegan resueltas desde la página. Sin acciones no se muestra el menú.
 */
export function ReceivableRowActions({
  receivable: r,
  canReverse,
  canSeeAdvances,
  onCollect,
  onReverse,
  onAdvances,
}: Props) {
  const [open, setOpen] = useState(false);
  const { t } = useI18n();
  const rootRef = useRef<HTMLDivElement>(null);
  const returnFocus = useReturnFocusAfterDialog(() =>
    rootRef.current?.querySelector<HTMLButtonElement>(
      ".zh-line-action-menu__trigger",
    ),
  );
  const run = (action: (receivable: SalesReceivableDto) => void) => () => {
    returnFocus();
    action(r);
  };
  const menuName = t("finance.receivables.list.actions.menu", {
    number: r.invoiceNumber,
  });

  const items: ReactElement[] = [];
  if (r.balanceDue > 0 && r.status !== "cancelled")
    items.push(
      <ZHLineActionItem
        key="collect"
        label={t("finance.receivables.list.actions.collect")}
        onClick={run(onCollect)}
      />,
    );
  if (canReverse)
    items.push(
      <ZHLineActionItem
        key="reverse"
        label={t("finance.receivables.list.actions.reverse")}
        onClick={run(onReverse)}
      />,
    );
  if (canSeeAdvances)
    items.push(
      <ZHLineActionItem
        key="advances"
        label={t("finance.customerAdvances.receivable.open")}
        onClick={run(onAdvances)}
      />,
    );

  if (items.length === 0) return null;

  return (
    <div ref={rootRef}>
      <ZHLineAction
        kind="menu"
        icon="more_horiz"
        className="fin-receivables__menu"
        label={t("finance.receivables.list.col.actions")}
        ariaLabel={menuName}
        menuLabel={menuName}
        portal
        menuAlign="end"
        closeOnSelect
        showText={false}
        open={open}
        onOpenChange={setOpen}
      >
        {items}
      </ZHLineAction>
    </div>
  );
}
