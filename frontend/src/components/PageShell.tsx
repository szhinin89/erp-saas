import { useI18n } from "../i18n/i18n";
import { ZHScreenHeading } from "./zh/ZHLayout";
import { ZHCard as Card } from "./zh/ZHCard";

interface Props {
  title: string;
  /** Línea superior (módulo / contexto), opcional. */
  kicker?: string;
  subtitle?: string;
  action?: React.ReactNode;
  children: React.ReactNode;
}

/** Contenedor de página con encabezado compacto unificado (ZHScreenHeading). */
export function PageShell({
  title,
  kicker,
  subtitle,
  action,
  children,
}: Props) {
  return (
    <div className="page-shell page-shell--compactHeading">
      <div className="page-shell-heading">
        <ZHScreenHeading
          kicker={kicker}
          title={title}
          subtitle={subtitle}
          right={action}
        />
      </div>
      {children}
    </div>
  );
}

export function TableCard({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  const tableCardClassName = className
    ? `table-card ${className}`
    : "table-card";
  return (
    <Card className={tableCardClassName} bodyClassName="table-card-body">
      {children}
    </Card>
  );
}

export function EmptyState({ message }: { message: string }) {
  return <div className="empty-state">{message}</div>;
}

export function ErrorState({ message }: { message: string }) {
  const { t } = useI18n();
  return (
    <div className="error-state">
      {t("common.errorPrefix")} {message}
    </div>
  );
}

export function LoadingState() {
  const { t } = useI18n();
  return <div className="loading-state">{t("common.loading")}</div>;
}

export function NoAccessPage({ title }: { title: string }) {
  const { t } = useI18n();
  return (
    <PageShell title={title} subtitle={t("common.noAccess")}>
      <Card>
        <EmptyState message={t("common.noAccess")} />
      </Card>
    </PageShell>
  );
}

export function PageToolbar(props: { children: React.ReactNode }) {
  return <div className="zh-page-toolbar">{props.children}</div>;
}

export type BadgeVariant =
  | "success"
  | "warning"
  | "error"
  | "neutral"
  | "info"
  | "green"
  | "orange"
  | "red"
  | "gray"
  | "blue";

export function Badge(
  props: React.HTMLAttributes<HTMLSpanElement> & {
    label: React.ReactNode;
    variant: BadgeVariant;
    /** `md` aplica `.badge--md` (radius menos redondeado); por defecto usa el radius pill. */
    size?: "md";
    /** Aplica `.badge--upper` (mayúsculas + letter-spacing). */
    upper?: boolean;
    /** Aplica `.badge--code` (font-family monoespaciada) — para códigos técnicos
     * (SKU, código de proveedor/auxiliar, secuenciales). Único modificador del
     * Badge con tipografía monoespaciada; combina con cualquier `variant`. */
    code?: boolean;
  },
) {
  const { label, variant, size, upper, code, className, ...rest } = props;
  const variantClassMap: Record<BadgeVariant, string> = {
  success: "badge--success",
  warning: "badge--warning",
  error: "badge--error",
  neutral: "badge--neutral",
  info: "badge--info",

  green: "badge--success",
  orange: "badge--warning",
  red: "badge--error",
  gray: "badge--neutral",
  blue: "badge--info",
};

const cls = [
    "badge",
    variantClassMap[variant],
    size ? `badge--${size}` : "",
    upper ? "badge--upper" : "",
    code ? "badge--code" : "",
    className,
  ]
    .filter(Boolean)
    .join(" ");
  return (
    <span {...rest} className={cls}>
      {label}
    </span>
  );
}

