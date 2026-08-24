import { ImportWizardPage } from "./ImportWizardPage";

export function InitialLoadCustomersPage() {
  return (
    <ImportWizardPage
      importType="Customers"
      templateFileName="plantilla-clientes.xlsx"
      title="Carga Inicial — Clientes"
      helpText="Descarga la plantilla, complétala con tus clientes y súbela aquí."
      requiredFieldsHint="Los campos obligatorios son Tipo/Número de Identificación y Razón Social."
      resultRoute="/masterdata/customers"
      resultRouteLabel="Ver clientes"
      resultEntityLabelPlural="clientes"
    />
  );
}
