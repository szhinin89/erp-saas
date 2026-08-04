type LauncherIconProps = {
  name: string;
  className?: string;
};

export type NavigationIconKey =
  | "dashboard"
  | "masterData"
  | "inventory"
  | "purchases"
  | "sales"
  | "settings"
  | "administration"
  | "cash"
  | "finance"
  | "accounting"
  | "logistics"
  | "reports"
  | "fallback";

function iconPath(name: string) {
  const key = name.toLowerCase();
  if (key === "close") return <path d="M6 6l12 12M18 6 6 18" />;
  if (key === "chevron") return <path d="m9 6 6 6-6 6" />;
  if (key === "search") return <><circle cx="11" cy="11" r="6" /><path d="m16 16 4 4" /></>;
  if (key === "star")
    return <path d="m12 3 2.78 5.63 6.22.9-4.5 4.38 1.06 6.19L12 17.18 6.44 20.1 7.5 13.91 3 9.53l6.22-.9L12 3Z" />;
  if (key.includes("sale") || key.includes("receipt"))
    return <path d="M6 3h12v18l-3-2-3 2-3-2-3 2V3Zm3 5h6M9 12h6" />;
  if (key.includes("purchase") || key.includes("cart"))
    return <path d="M4 5h2l2 11h9l2-8H7m2 12a1 1 0 1 0 0-2 1 1 0 0 0 0 2Zm8 0a1 1 0 1 0 0-2 1 1 0 0 0 0 2Z" />;
  if (key.includes("inventory") || key.includes("warehouse") || key.includes("box"))
    return <path d="m4 7 8-4 8 4v10l-8 4-8-4V7Zm0 0 8 4 8-4M12 11v10" />;
  if (key.includes("setting") || key.includes("config"))
    return <path d="M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Zm0-5v2m0 14v2m9-9h-2M5 12H3m15.36-6.36-1.42 1.42M7.05 16.95l-1.41 1.41m12.72 0-1.42-1.41M7.05 7.05 5.64 5.64" />;
  if (key.includes("security") || key.includes("admin") || key.includes("shield"))
    return <path d="M12 3 19 6v5c0 4.55-2.9 8.4-7 10-4.1-1.6-7-5.45-7-10V6l7-3Zm-3 9 2 2 4-4" />;
  if (key.includes("user") || key.includes("master") || key.includes("people"))
    return <path d="M16 20v-1a4 4 0 0 0-4-4H7a4 4 0 0 0-4 4v1m6-9a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm9 2v6m3-3h-6" />;
  if (key.includes("dashboard") || key.includes("home"))
    return <path d="M4 4h6v6H4V4Zm10 0h6v6h-6V4ZM4 14h6v6H4v-6Zm10 0h6v6h-6v-6Z" />;
  if (key.includes("wallet") || key.includes("cash") || key.includes("finance"))
    return <path d="M4 7h15a1 1 0 0 1 1 1v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h12v3M16 13h4" />;
  if (key.includes("book") || key.includes("account"))
    return <path d="M4 5a3 3 0 0 1 3-3h12v17H7a3 3 0 0 0-3 3V5Zm3 12h12M8 6h7" />;
  if (key.includes("truck") || key.includes("logistic"))
    return <path d="M3 5h11v11H3V5Zm11 4h4l3 3v4h-7V9Zm-7 10a2 2 0 1 0 0-4 2 2 0 0 0 0 4Zm10 0a2 2 0 1 0 0-4 2 2 0 0 0 0 4Z" />;
  if (key.includes("chart") || key.includes("report"))
    return <path d="M4 20V10m6 10V4m6 16v-7m4 7H2" />;
  return <path d="M4 4h6v6H4V4Zm10 0h6v6h-6V4ZM4 14h6v6H4v-6Zm10 0h6v6h-6v-6Z" />;
}

const MODULE_ICON_BY_ID: Readonly<Record<string, NavigationIconKey>> = {
  home: "dashboard",
  dashboard: "dashboard",
  masterdata: "masterData",
  inventory: "inventory",
  inventario: "inventory",
  purchases: "purchases",
  sales: "sales",
  settings: "settings",
  configuracion: "settings",
  admin: "administration",
  security: "administration",
  caja: "cash",
  cash: "cash",
  finance: "finance",
  accounting: "accounting",
  logistica: "logistics",
  logistics: "logistics",
  reports: "reports",
};

/** SVG local para el drawer: evita ligaduras de texto y dependencias remotas. */
export function LauncherIcon({ name, className }: LauncherIconProps) {
  const isStar = name.toLowerCase() === "star";
  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      fill={isStar ? "none" : "none"}
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {iconPath(name)}
    </svg>
  );
}

/** Iconografía de módulos basada en el id estable del grupo de navegación. */
export function LauncherModuleIcon({
  moduleId,
  className,
}: {
  moduleId: string;
  className?: string;
}) {
  const name = MODULE_ICON_BY_ID[moduleId.toLowerCase()] ?? "fallback";
  return <LauncherIcon name={name} className={className} />;
}
