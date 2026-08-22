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

        /// <summary>
        /// CONFIG-DYNAMIC-OPERATIONS-01: si se envía el correo de "factura autorizada" al cliente.
        /// Propietario: Empresa (scope=Company). Ausencia de fila → true (comportamiento actual,
        /// sin cambios, ver SalesInvoiceAuthorizedCommunicationHandler).
        /// </summary>
        public const string SalesInvoiceAuthorizedEnabled =
            "communications.sales_invoice_authorized.enabled";

        /// <summary>
        /// CONFIG-DYNAMIC-OPERATIONS-01: si además del cliente, se envía copia del correo de
        /// factura autorizada al correo de la propia empresa. Propietario: Empresa (scope=Company).
        /// </summary>
        public const string SendCopyToCompanyEmail = "communications.send_copy_to_company_email";
    }

    /// <summary>
    /// CONFIG-DYNAMIC-OPERATIONS-01: preferencias operativas de Ventas/POS. Propietario: Empresa
    /// (scope=Company). Ver docs del bloque para el mapeo completo Fase A/Fase B/Fase C.
    /// </summary>
    public static class SalesPos
    {
        public const string RequireOpenCashSession = "sales.pos.require_open_cash_session";
        public const string AllowManualPrice = "sales.pos.allow_manual_price";
        public const string AllowManualDiscount = "sales.pos.allow_manual_discount";
        public const string MaxDiscountPercent = "sales.pos.max_discount_percent";
        public const string RequireCustomerAboveAmount = "sales.pos.require_customer_above_amount";
        public const string AllowSellWithoutStock = "sales.pos.allow_sell_without_stock";
        public const string AskBeforeIssue = "sales.pos.ask_before_issue";
        public const string DefaultPriceListId = "sales.pos.default_price_list_id";
        public const string DefaultCustomerId = "sales.pos.default_customer_id";
    }

    /// <summary>
    /// CONFIG-DYNAMIC-OPERATIONS-01: preferencias operativas de Caja. Propietario: Empresa
    /// (scope=Company).
    /// </summary>
    public static class Cash
    {
        public const string RequireOpeningAmount = "cash.require_opening_amount";
        public const string AllowCloseWithDifference = "cash.allow_close_with_difference";
        public const string MaxAllowedDifference = "cash.max_allowed_difference";
        public const string RequireReasonForDifference = "cash.require_reason_for_difference";
        public const string AllowManualInOutMovements = "cash.allow_manual_in_out_movements";
        public const string RequireReasonForMovements = "cash.require_reason_for_movements";
    }

    /// <summary>
    /// CONFIG-DYNAMIC-OPERATIONS-01: preferencias de impresión de tirilla de venta. Propietario:
    /// Empresa (scope=Company). <see cref="SalesReceiptPaperWidth"/> se guarda pero
    /// deliberadamente NO tiene efecto real — el ancho de papel ya es configurable por impresora
    /// en ZH Print Agent (PrinterInfo.PaperWidthMm, /admin local); duplicar esa fuente de verdad a
    /// nivel de empresa fue evaluado y descartado (ver plan CONFIG-DYNAMIC-OPERATIONS-01).
    /// </summary>
    public static class Printing
    {
        public const string SalesReceiptMode = "printing.sales_receipt.mode";
        public const string SalesReceiptCopies = "printing.sales_receipt.copies";
        public const string SalesReceiptPaperWidth = "printing.sales_receipt.paper_width";
        public const string SalesReceiptIncludeLogo = "printing.sales_receipt.include_logo";
        public const string SalesReceiptIncludeAccessKey =
            "printing.sales_receipt.include_access_key";
        public const string SalesReceiptIncludeCashier = "printing.sales_receipt.include_cashier";
        public const string SalesReceiptOpenCashDrawer = "printing.sales_receipt.open_cash_drawer";
    }

    /// <summary>
    /// CONFIG-DYNAMIC-OPERATIONS-01: preferencias operativas de Compras. Propietario: Empresa
    /// (scope=Company).
    /// </summary>
    public static class Purchases
    {
        public const string DefaultWarehouseId = "purchases.default_warehouse_id";
        public const string AllowConfirmWithoutReceptionXml =
            "purchases.allow_confirm_without_reception_xml";
        public const string UpdateCostOnConfirm = "purchases.update_cost_on_confirm";
        public const string AllowManualCostChange = "purchases.allow_manual_cost_change";
        public const string RequireReasonForCostChange = "purchases.require_reason_for_cost_change";
    }

    /// <summary>
    /// CONFIG-DYNAMIC-OPERATIONS-01: preferencias operativas de Inventario. Propietario: Empresa
    /// (scope=Company).
    /// </summary>
    public static class Inventory
    {
        public const string AllowNegativeStock = "inventory.allow_negative_stock";
        public const string RequireReasonForAdjustment = "inventory.require_reason_for_adjustment";
        public const string RequireApprovalForLargeAdjustment =
            "inventory.require_approval_for_large_adjustment";
        public const string LargeAdjustmentThresholdAmount =
            "inventory.large_adjustment_threshold_amount";
    }

    /// <summary>
    /// CONFIG-DYNAMIC-OPERATIONS-01: preferencias operativas de Documentos Electrónicos.
    /// Propietario: Empresa (scope=Company). No confundir con SriSettings (certificado/ambiente) —
    /// eso permanece fuera de este namespace.
    /// </summary>
    public static class ElectronicDocuments
    {
        public const string AutoRetryEnabled = "electronic_documents.auto_retry_enabled";
        public const string MaxRetryAttempts = "electronic_documents.max_retry_attempts";
        public const string GenerateRideOnAuthorization =
            "electronic_documents.generate_ride_on_authorization";
        public const string EmailOnAuthorization = "electronic_documents.email_on_authorization";
    }
}
