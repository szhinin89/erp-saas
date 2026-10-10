import { ImportWizardPage } from "./ImportWizardPage";

export function InitialLoadInitialReceivablesPage() {
  return (
    <ImportWizardPage
      importType="InitialReceivables"
      templateFileName="plantilla-cxc-inicial.xlsx"
      title="Carga Inicial — Cuentas por Cobrar"
      helpText="Descarga la plantilla, complétala con los saldos pendientes de tus clientes (una fila = un documento pendiente) y súbela aquí."
      requiredFieldsHint="Saldo pendiente al corte en la sucursal activa; no se registran ventas ni cobros históricos. El cliente debe existir con rol Cliente. Obligatorios: Tipo y Número Identificación, Número Documento, Fecha Emisión, Fecha Vencimiento, Saldo Pendiente (punto decimal, máx. 2 decimales), Moneda (USD) y Fecha de corte (AAAA-MM-DD), que debe ser la fecha de apertura de saldos de la empresa."
      resultRoute="/finance/receivables"
      resultRouteLabel="Ver Cuentas por Cobrar"
      resultEntityLabelPlural="saldos"
      resultEntityLabelSingular="saldo"
      primaryColumnKey="Número Identificación"
      primaryColumnLabel="Cliente"
      secondaryColumnKey="Número Documento"
      secondaryColumnLabel="Documento"
    />
  );
}
