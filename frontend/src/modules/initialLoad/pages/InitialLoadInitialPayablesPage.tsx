import { ImportWizardPage } from "./ImportWizardPage";

export function InitialLoadInitialPayablesPage() {
  return (
    <ImportWizardPage
      importType="InitialPayables"
      templateFileName="plantilla-cxp-inicial.xlsx"
      title="Carga Inicial — Cuentas por Pagar"
      helpText="Descarga la plantilla, complétala con los saldos pendientes con tus proveedores (una fila = un documento pendiente = una cuota) y súbela aquí."
      requiredFieldsHint="Saldo neto pendiente al corte en la sucursal activa; no se registran compras, gastos, pagos ni retenciones históricas. El proveedor debe existir con rol Proveedor. Obligatorios: Tipo y Número Identificación, Tipo Documento (código SRI real, p. ej. 01), Número Documento, Fecha Emisión, Fecha Vencimiento, Saldo Pendiente (punto decimal, máx. 2 decimales), Moneda (USD) y Fecha de corte (AAAA-MM-DD), que debe ser la fecha de apertura de saldos de la empresa."
      resultRoute="/payables"
      resultRouteLabel="Ver Cuentas por Pagar"
      resultEntityLabelPlural="saldos"
      resultEntityLabelSingular="saldo"
      primaryColumnKey="Número Identificación"
      primaryColumnLabel="Proveedor"
      secondaryColumnKey="Número Documento"
      secondaryColumnLabel="Documento"
      confirmUnavailableReason="La confirmación de CxP inicial todavía no está disponible. Puede validar el archivo y revisar el resultado."
    />
  );
}
