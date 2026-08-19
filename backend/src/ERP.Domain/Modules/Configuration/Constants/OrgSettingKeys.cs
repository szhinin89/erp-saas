namespace ERP.Domain.Configuration.Constants;

/// <summary>
/// Claves bien conocidas para OrgSettings.
/// Organizadas por namespace de módulo (punto como separador).
/// Ningún módulo debe crear claves fuera de este archivo sin revisión.
/// </summary>
public static class OrgSettingKeys
{
    /// <summary>
    /// Valores por defecto para nuevas facturas de venta.
    /// Propietarios: DocTypeCode/PaymentMethodCode/PaymentTermId → Empresa.
    ///               DefaultWarehouseId → Sucursal.
    ///               DefaultEmissionPointId → eliminado; usar EmissionPoint.IsDefault.
    /// </summary>
    public static class Invoice
    {
        public const string DefaultDocTypeCode = "invoice.default_doc_type_code";
        public const string DefaultPaymentMethodCode = "invoice.default_payment_method_code";
        public const string DefaultWarehouseId = "invoice.default_warehouse_id";
        public const string DefaultPaymentTermId = "invoice.default_payment_term_id";
    }

    /// <summary>
    /// Configuración del catálogo de ítems. Propietario: Empresa (scope=Company).
    /// </summary>
    public static class Catalog
    {
        /// <summary>
        /// Profundidad máxima permitida del árbol de categorías (ItemCategoryNode).
        /// Sin fila configurada → default 3 (ver <c>CreateCategoryNodeCommandHandler</c>).
        /// </summary>
        public const string MaxCategoryDepth = "catalog.max_category_depth";
    }

    /// <summary>
    /// Branding del RIDE (ADR-025 §12, Fase 8 del plan de implementación de Ride). Propietario:
    /// Empresa (scope=Company) en v1.0 — la jerarquía Sucursal/Punto de Emisión es diseño futuro
    /// (ADR-025 §11), sin nuevas claves todavía. Todos opcionales: ausencia de fila es un estado
    /// válido (tenant sin branding configurado), nunca un error.
    /// </summary>
    public static class Ride
    {
        public const string LogoStoragePath = "ride.branding.logo_storage_path";
        public const string PrimaryColorHex = "ride.branding.primary_color_hex";
        public const string SecondaryColorHex = "ride.branding.secondary_color_hex";
        public const string FooterText = "ride.branding.footer_text";
    }

    /// <summary>
    /// Política fiscal de Consumidor Final. Propietario: Empresa (scope=Company), editada desde
    /// Fiscal/Tributario en Configuración de Empresa. Ausencia de fila → se calcula default por
    /// régimen tributario (ver ConsumerFinalPolicyDefaults, dominio Sales) — nunca hardcodear el
    /// default en Application/API/frontend.
    /// </summary>
    public static class Sales
    {
        /// <summary>
        /// numeric(18,2). "0" es un valor manual válido y distinto de "sin configurar": bloquea
        /// toda venta a Consumidor Final. Ausencia de fila (no confundir con "0") activa el
        /// default por régimen.
        /// </summary>
        public const string ConsumerFinalMaxAmount = "sales.consumer_final.max_amount";
    }

    /// <summary>
    /// CONFIG-FOUNDATION-P1-01: precisión decimal de PRESENTACIÓN — cuántos decimales se
    /// muestran/almacenan para cantidades, precios, costos, porcentajes y totales en pantalla.
    /// Propietario: Empresa (scope=Company). Reemplaza el mecanismo paralelo GeneralParameter
    /// (keys <c>decimal.*</c>), eliminado en esta entrega.
    ///
    /// NUNCA debe usarse para redondeo fiscal, tributario o de documentos autorizados — eso es
    /// <see cref="global::ERP.Domain.Common.FiscalPrecision"/> (constante System, no
    /// configurable, sin relación con este namespace). Mezclar ambos es exactamente el error que
    /// esta migración corrige: antes de esta entrega existían dos sistemas de decimales sin
    /// frontera documentada; ahora la frontera es "Presentation" = UI, "FiscalPrecision" = legal.
    /// </summary>
    public static class Presentation
    {
        public const string DecimalSalesUnitPrice = "presentation.decimal.sales_unit_price";
        public const string DecimalPurchaseUnitPrice = "presentation.decimal.purchase_unit_price";
        public const string DecimalQuantity = "presentation.decimal.quantity";
        public const string DecimalPercentage = "presentation.decimal.percentage";
        public const string DecimalTotalAmount = "presentation.decimal.total_amount";
    }
}
