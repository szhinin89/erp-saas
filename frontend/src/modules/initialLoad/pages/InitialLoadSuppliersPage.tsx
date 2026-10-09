import { ImportWizardPage } from "./ImportWizardPage";

export function InitialLoadSuppliersPage() {
  return (
    <ImportWizardPage
      importType="Suppliers"
      templateFileName="plantilla-proveedores.xlsx"
      title="Carga Inicial — Proveedores"
      helpText="Descarga la plantilla, complétala con tus proveedores y súbela aquí."
      requiredFieldsHint="Solo RUC (04) o Exterior (08). Los campos obligatorios son Tipo/Número de Identificación, Razón Social, Condición de Pago, Obligado a llevar contabilidad (SI/NO) y Exento de retención (SI/NO). Tipo Entidad Legal es obligatorio para Exterior."
      resultRoute="/suppliers"
      resultRouteLabel="Ver proveedores"
      resultEntityLabelPlural="proveedores"
    />
  );
}
