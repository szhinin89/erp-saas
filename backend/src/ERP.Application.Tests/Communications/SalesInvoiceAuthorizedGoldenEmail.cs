namespace ERP.Application.Tests.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-TEMPLATES-01 — salida EXACTA del correo de factura autorizada, capturada del
/// handler ANTES de migrarlo a templates (asunto + HTML + texto armados en código). El template
/// default SALES_INVOICE_AUTHORIZED v1 debe reproducirla byte a byte. No se edita al cambiar el
/// template: una diferencia aquí es un cambio visible del correo y requiere una versión nueva.
/// </summary>
internal static class SalesInvoiceAuthorizedGoldenEmail
{
    public const string InvoiceNumber = "001-001-000000001";
    public const string AccessKey = "2108202601179214672100110010010000000011234567811";
    public const string Issuer = "ZH Demo";

    public sealed record Golden(string CustomerName, string Subject, string Html, string Text);

    public static readonly Golden Plain = new(
        "Cliente Demo",
        "Factura autorizada 001-001-000000001 - ZH Demo",
        "<p>Estimado/a Cliente Demo,</p><p>Su factura electronica fue autorizada por el SRI.</p><ul>"
            + "<li><strong>Factura:</strong> 001-001-000000001</li>"
            + "<li><strong>Clave de acceso:</strong> 2108202601179214672100110010010000000011234567811</li>"
            + "<li><strong>Cliente:</strong> Cliente Demo</li>"
            + "<li><strong>Total:</strong> USD 100.00</li>"
            + "<li><strong>Emisor:</strong> ZH Demo</li></ul>",
        "Estimado/a Cliente Demo,\n\nSu factura electronica fue autorizada por el SRI.\n"
            + "Factura: 001-001-000000001\n"
            + "Clave de acceso: 2108202601179214672100110010010000000011234567811\n"
            + "Cliente: Cliente Demo\n"
            + "Total: USD 100.00\n"
            + "Emisor: ZH Demo\n"
    );

    /// <summary>Caracteres especiales: el HTML los escapa (incluida la "é" como entidad numérica); asunto y texto no.</summary>
    public static readonly Golden Special = new(
        "José & Hijos <S.A.>",
        "Factura autorizada 001-001-000000001 - ZH Demo",
        "<p>Estimado/a Jos&#233; &amp; Hijos &lt;S.A.&gt;,</p><p>Su factura electronica fue autorizada por el SRI.</p><ul>"
            + "<li><strong>Factura:</strong> 001-001-000000001</li>"
            + "<li><strong>Clave de acceso:</strong> 2108202601179214672100110010010000000011234567811</li>"
            + "<li><strong>Cliente:</strong> Jos&#233; &amp; Hijos &lt;S.A.&gt;</li>"
            + "<li><strong>Total:</strong> USD 100.00</li>"
            + "<li><strong>Emisor:</strong> ZH Demo</li></ul>",
        "Estimado/a José & Hijos <S.A.>,\n\nSu factura electronica fue autorizada por el SRI.\n"
            + "Factura: 001-001-000000001\n"
            + "Clave de acceso: 2108202601179214672100110010010000000011234567811\n"
            + "Cliente: José & Hijos <S.A.>\n"
            + "Total: USD 100.00\n"
            + "Emisor: ZH Demo\n"
    );
}
