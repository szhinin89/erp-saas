export const brandConfig = {
  companyName: "ZH Technologies",
  foundationYear: 2026,
  productSubtitle: "Sistema de gestión empresarial",
  secureAccessText: "Conexión segura",
  protectedAccessText: "Acceso protegido",
} as const;

export function getCopyrightText(
  currentYear = new Date().getFullYear(),
): string {
  const startYear = brandConfig.foundationYear;
  const yearText =
    currentYear > startYear ? `${startYear}–${currentYear}` : `${startYear}`;
  return `© ${yearText} ${brandConfig.companyName}`;
}
