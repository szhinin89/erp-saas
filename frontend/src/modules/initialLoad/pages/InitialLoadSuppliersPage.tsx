import { ImportWizardPage } from "./ImportWizardPage";

export function InitialLoadSuppliersPage() {
  return (
    <ImportWizardPage
      importType="Suppliers"
      templateFileName="plantilla-proveedores.xlsx"
      title="Carga Inicial — Proveedores"
      helpText="Descarga la plantilla, complétala con tus proveedores y súbela aquí."
      requiredFieldsHint="Los campos obligatorios son Tipo/Número de Identificación, Razón Social y Condición de Pago."
      resultRoute="/masterdata/suppliers"
      resultRouteLabel="Ver proveedores"
      resultEntityLabelPlural="proveedores"
    />
  );
}
