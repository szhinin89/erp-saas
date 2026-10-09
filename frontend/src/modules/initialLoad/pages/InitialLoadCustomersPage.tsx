import { ImportWizardPage } from "./ImportWizardPage";

export function InitialLoadCustomersPage() {
  return (
    <ImportWizardPage
      importType="Customers"
      templateFileName="plantilla-clientes.xlsx"
      title="Carga Inicial — Clientes"
      helpText="Descarga la plantilla, complétala con tus clientes y súbela aquí."
      requiredFieldsHint="Los campos obligatorios son Tipo/Número de Identificación, Razón Social y Condición de Pago. Tipo Entidad Legal es obligatorio para Pasaporte, Exterior y Placa."
      resultRoute="/customers"
      resultRouteLabel="Ver clientes"
      resultEntityLabelPlural="clientes"
    />
  );
}
