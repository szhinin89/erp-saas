import { useI18n } from "../../../i18n/i18n";
import { ImportWizardPage } from "./ImportWizardPage";

export function InitialLoadProductCatalogPage() {
  const { t } = useI18n();
  return (
    <ImportWizardPage
      importType="Items"
      templateFileName="plantilla-catalogo-productos.xlsx"
      title="Carga Inicial — Catálogo de Productos"
      helpText="Descarga la plantilla, complétala con tus productos (una fila = un producto) y súbela aquí."
      requiredFieldsHint="Los campos obligatorios son SKU, Nombre comercial (columna Nombre), Tipo (Tipo de Ítem), Unidad base de inventario (Unidad Base), Tarifa IVA venta (IVA Venta), Tarifa IVA compra (IVA Compra), Categoría, Marca, Disponible POS y al menos un Código de barras. Los encabezados de la plantilla se conservan. Nombre también origina la Descripción completa; esta carga crea naturaleza operativa Producto."
      resultRoute="/products/items"
      resultRouteLabel="Ver ítems"
      resultEntityLabelPlural="productos"
      primaryColumnKey="SKU"
      primaryColumnLabel="SKU"
      secondaryColumnKey="Nombre"
      secondaryColumnLabel={t("items.form.shortName")}
      showAutoCreateCatalogOption
    />
  );
}
