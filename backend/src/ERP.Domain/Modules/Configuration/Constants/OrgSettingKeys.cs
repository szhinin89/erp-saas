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
    /// CONFIG-FOUNDATION-P1-02: marca de la empresa (colores, eslogan, pie de página de
    /// documentos). Propietario: Empresa (scope=Company). RIDE/PDF/reportes son CONSUMIDORES,
    /// nunca dueños — leen la marca ya resuelta vía <c>ICompanyBrandingResolver</c>, jamás estas
    /// keys directamente (ver <c>backend/src/ERP.Infrastructure/Ride/Branding/CompanyBrandingRideProvider.cs</c>).
    /// Reemplaza el namespace <c>ride.branding.*</c> (ADR-025 §12), que nunca tuvo un flujo de
    /// escritura real en producción y quedaba semánticamente mal ubicado — RIDE no es dueño de la
    /// marca de la empresa. Todos opcionales: ausencia de fila es un estado válido, nunca un error.
    ///
    /// El logo NO vive aquí — vive en <c>MediaFile</c> (Owner=Company, Role="logo"), ya
    /// funcional; org_settings nunca debe guardar binarios/base64/rutas administradas por otro
    /// subsistema (Principio de la arquitectura objetivo).
    /// </summary>
    public static class CompanyBranding
    {
        public const string PrimaryColor = "company.branding.primary_color";
        public const string SecondaryColor = "company.branding.secondary_color";
        public const string Slogan = "company.branding.slogan";
        public const string DocumentFooterText = "company.branding.document_footer_text";
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
    /// <summary>
    /// Configuración transversal de comunicaciones. Propietario: Empresa (scope=Company).
    /// Consumida por el módulo Communications para correo transaccional y futuros canales.
    /// </summary>
    public static class Communications
    {
        public const string EmailEnabled = "communications.email.enabled";
        public const string SmtpHost = "communications.email.smtp_host";
        public const string SmtpPort = "communications.email.smtp_port";
        public const string SmtpUsername = "communications.email.smtp_username";
        public const string SmtpPassword = "communications.email.smtp_password";
        public const string SenderEmail = "communications.email.sender_email";
        public const string SenderName = "communications.email.sender_name";
        public const string UseSsl = "communications.email.use_ssl";
        public const string ReplyToEmail = "communications.email.reply_to_email";
        public const string MaxRetries = "communications.email.max_retries";
        public const string DefaultLanguage = "communications.email.default_language";
    }
}
