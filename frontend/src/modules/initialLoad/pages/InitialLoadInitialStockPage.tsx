import { ImportWizardPage } from "./ImportWizardPage";

export function InitialLoadInitialStockPage() {
  return (
    <ImportWizardPage
      importType="InitialStock"
      templateFileName="plantilla-stock-inicial.xlsx"
      title="Carga Inicial — Stock Inicial"
      helpText="Descarga la plantilla, complétala con las existencias iniciales (una fila = un producto en una bodega) y súbela aquí."
      requiredFieldsHint="Saldo inicial al corte de la sucursal activa. El producto y la bodega deben existir y no tener stock ni movimientos previos. Obligatorios: SKU o Código de barras, Código Bodega, Cantidad, Costo unitario (punto decimal) y Fecha de corte (AAAA-MM-DD)."
      resultRoute="/inventory/kardex"
      resultRouteLabel="Ver Kardex"
      resultEntityLabelPlural="existencias"
      primaryColumnKey="SKU"
      primaryColumnLabel="SKU"
      secondaryColumnKey="Código Bodega"
      secondaryColumnLabel="Bodega"
    />
  );
}
