import { ImportWizardPage } from "./ImportWizardPage";

export function InitialLoadInitialStockPage() {
  return (
    <ImportWizardPage
      importType="InitialStock"
      templateFileName="plantilla-stock-inicial.xlsx"
      title="Carga Inicial — Stock Inicial"
      helpText="Descarga la plantilla, complétala con las existencias iniciales (una fila = un producto en una bodega) y súbela aquí."
      requiredFieldsHint="El producto y la bodega deben existir previamente. SKU o Código de barras, Bodega, Cantidad y Costo unitario son obligatorios."
      resultRoute="/inventory/kardex"
      resultRouteLabel="Ver Kardex"
      resultEntityLabelPlural="existencias"
      primaryColumnKey="SKU"
      primaryColumnLabel="SKU"
      secondaryColumnKey="Bodega"
      secondaryColumnLabel="Bodega"
    />
  );
}
