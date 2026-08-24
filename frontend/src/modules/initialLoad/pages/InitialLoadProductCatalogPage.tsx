import { ImportWizardPage } from "./ImportWizardPage";

export function InitialLoadProductCatalogPage() {
  return (
    <ImportWizardPage
      importType="Items"
      templateFileName="plantilla-catalogo-productos.xlsx"
      title="Carga Inicial — Catálogo de Productos"
      helpText="Descarga la plantilla, complétala con tus productos (una fila = un producto) y súbela aquí."
      requiredFieldsHint="Los campos obligatorios son SKU, Nombre, Tipo de Ítem, Unidad Base, Categoría, Marca y al menos un Código de Barra."
      resultRoute="/inventory/items"
      resultRouteLabel="Ver ítems"
      resultEntityLabelPlural="productos"
      primaryColumnKey="SKU"
      primaryColumnLabel="SKU"
      secondaryColumnKey="Nombre"
      secondaryColumnLabel="Nombre"
      showAutoCreateCatalogOption
    />
  );
}
