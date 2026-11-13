using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ERP.API.Swagger;

/// <summary>
/// Descripción del cuerpo <c>multipart/form-data</c> de las cinco operaciones de carga de archivo
/// (API-DOC-V1 / Fase 2D). Swashbuckle no genera <c>requestBody.description</c> para un
/// <c>IFormFile</c> suelto, y los controladores no se tocan: la descripción es solo metadato del
/// documento. Cada texto repite lo que la operación ya declara y verifica en su <c>remarks</c>
/// (extensiones y tamaños comprobados contra el handler en las fases 2B-3 y 2B-5).
/// </summary>
public sealed class OpenApiMultipartBodyDescriptionFilter : IOperationFilter
{
    public const string FileField = "file";

    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>(
        StringComparer.Ordinal
    )
    {
        ["CompanyProfile_UploadLogo"] =
            "Formulario `multipart/form-data` con el campo `file`: imagen del logo principal (PNG, JPG o WEBP, hasta 5 MB y 4000 x 4000 px).",
        ["CompanyProfile_UploadLogoAlt"] =
            "Formulario `multipart/form-data` con el campo `file`: imagen del logo alterno (PNG, JPG o WEBP, hasta 5 MB y 4000 x 4000 px).",
        ["ElectronicInvoicing_UploadCertificate"] =
            "Formulario `multipart/form-data` con el campo `file`: certificado de firma electrónica (extensión `.p12` o `.pfx`, hasta 5 MB).",
        ["InitialLoad_Upload"] =
            "Formulario `multipart/form-data` con el campo `file`: libro de importación `.xlsx` (hasta 10 MB) del lote indicado en la ruta.",
        ["PurchaseReception_Import"] =
            "Formulario `multipart/form-data` con el campo `file`: TXT de comprobantes recibidos descargado del SRI.",
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var id = OpenApiOperationIds.Base(context.ApiDescription);
        if (id is null || !Descriptions.TryGetValue(id, out var description))
            return;
        if (operation.RequestBody is null)
            return;

        if (string.IsNullOrWhiteSpace(operation.RequestBody.Description))
            operation.RequestBody.Description = description;

        // El esquema multipart es inline (no un componente): describir el campo no altera ningún contrato.
        if (
            operation.RequestBody.Content.TryGetValue("multipart/form-data", out var media)
            && media.Schema?.Properties.TryGetValue(FileField, out var file) == true
            && string.IsNullOrWhiteSpace(file.Description)
        )
            file.Description = description;
    }
}
