using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.API.Uploads;
using ERP.Application.Modules.Companies.DTOs;
using ERP.Application.Modules.Companies.UseCases.GetCompanyBranding;
using ERP.Application.Modules.Companies.UseCases.GetCompanyLogoAltContent;
using ERP.Application.Modules.Companies.UseCases.GetCompanyLogoContent;
using ERP.Application.Modules.Companies.UseCases.GetCompanyOperationalReadiness;
using ERP.Application.Modules.Companies.UseCases.GetCompanyProfile;
using ERP.Application.Modules.Companies.UseCases.GetSalesFiscalPolicy;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyBranding;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyDocuments;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyFiscal;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyOperation;
using ERP.Application.Modules.Companies.UseCases.UpdateCompanyProfile;
using ERP.Application.Modules.Companies.UseCases.UpdateConsumerFinalMaxAmount;
using ERP.Application.Modules.Companies.UseCases.UploadCompanyLogo;
using ERP.Application.Modules.Companies.UseCases.UploadCompanyLogoAlt;
using ERP.Domain.Kernel.Permissions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

// ZH-API-THIN-COMPANIES-01 — perfil y configuración de la EMPRESA ACTIVA (perfil, fiscal, operación,
// documentos, branding, logos, política fiscal de ventas, preparación operativa). Consumido por
// configuracion/empresa en el frontend. Crear empresa y la empresa actual siguen en
// CompaniesController; la identidad completa (RUC, estado) solo la edita Admin Global.
// Mismas rutas bajo api/v1/companies, mismo tag OpenAPI "Companies" y sin [AppFeature] propio
// (la fila "Empresa activa" la aporta CompaniesController).
// Comentario no-XML a propósito: un <summary> de clase agregaría un tag nuevo al documento OpenAPI.
[ApiController]
[Route("api/v1/companies")]
[Tags("Companies")]
[Produces("application/json")]
public sealed class CompanyProfileController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyProfileController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Obtiene el perfil completo de la empresa activa (datos generales, fiscales, de operación y de
    /// documentos).
    /// </summary>
    /// <remarks>
    /// La identidad (RUC, razón social, estado) es de solo lectura aquí: solo la edita Administración
    /// Global.
    /// </remarks>
    /// <response code="200">Perfil de la empresa activa.</response>
    /// <response code="400">La empresa del contexto no existe.</response>
    [HttpGet("profile")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCompanyProfileQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza los datos generales de la empresa activa (contacto, moneda, zona horaria y
    /// representante legal).
    /// </summary>
    /// <remarks>
    /// No modifica la identidad fiscal (RUC) ni el estado de la empresa. Moneda: código de 3
    /// caracteres; zona horaria: obligatoria (máx. 64); teléfonos hasta 20 caracteres; el sitio web
    /// debe ser una URL válida.
    /// </remarks>
    /// <param name="command">Datos generales de la empresa.</param>
    /// <response code="200">Perfil actualizado.</response>
    /// <response code="400">La empresa del contexto no existe.</response>
    /// <response code="422">Validación de campos (correos, URL, moneda, zona horaria, longitudes).</response>
    [HttpPut("profile")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateCompanyProfileCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Sube el logo principal de la empresa activa, reemplazando el anterior.
    /// </summary>
    /// <remarks>
    /// Solicitud `multipart/form-data` con el campo `file`. Se aceptan imágenes PNG, JPG o WEBP de
    /// hasta 5 MB y 4000 x 4000 px (el formato se comprueba por el contenido, no por la extensión). El
    /// archivo queda con visibilidad restringida al tenant y se descarga con `GET
    /// profile/logo/content`.
    /// </remarks>
    /// <param name="file">Imagen del logo.</param>
    /// <response code="200">Logo guardado; devuelve el perfil actualizado.</response>
    /// <response code="400">No se adjuntó archivo o la empresa del contexto no existe.</response>
    /// <response code="422">Archivo vacío, sin nombre, demasiado grande, de formato no soportado o con dimensiones excesivas.</response>
    [HttpPost("profile/logo")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadLogo(
        IFormFile? file,
        CancellationToken cancellationToken = default
    )
    {
        if (file is null || file.Length == 0)
            return this.ApiBadRequest("Debe adjuntar un archivo de imagen.");

        await using var upload = await BufferedFormFile.CreateAsync(file, cancellationToken);
        var result = await _mediator.Send(
            new UploadCompanyLogoCommand(upload.Content),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Descarga el contenido binario del logo principal de la empresa activa.
    /// </summary>
    /// <remarks>
    /// La respuesta es la imagen (PNG, JPG o WEBP) con el `Content-Type` guardado, no un envelope.
    /// </remarks>
    /// <response code="200">Imagen del logo.</response>
    /// <response code="404">La empresa no tiene logo configurado o el archivo no está disponible.</response>
    [HttpGet("profile/logo/content")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    // IFormFile solo marca el cuerpo binario (Swagger lo mapea a string/binary); la respuesta real
    // es un File con el Content-Type guardado (PNG, JPG o WEBP).
    [ProducesResponseType(typeof(IFormFile), StatusCodes.Status200OK, "image/png", "image/jpeg", "image/webp")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogoContent(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCompanyLogoContentQuery(), cancellationToken);
        return this.ToFileOrNotFound(
            result,
            content => File(content.Content, content.ContentType, content.FileName)
        );
    }

    /// <summary>
    /// Actualiza la configuración fiscal de la empresa activa (régimen, retenciones y designaciones del
    /// SRI).
    /// </summary>
    /// <remarks>
    /// Se ejecuta en una transacción bajo el mismo bloqueo exclusivo de la política contable que el
    /// cambio de modo contable, para que dos cambios concurrentes no rompan la regla «obligada a llevar
    /// contabilidad requiere contabilidad habilitada». Marcar la empresa como obligada a llevar
    /// contabilidad exige que Contabilidad ya esté habilitada (no se activa sola) y se rechaza con 422.
    /// Las designaciones de agente de retención y de gran contribuyente validan el formato del número
    /// de resolución y exigen su fecha de vigencia (y viceversa); el contribuyente especial valida el
    /// formato de su número.
    /// </remarks>
    /// <param name="command">Datos fiscales de la empresa.</param>
    /// <response code="200">Configuración fiscal actualizada.</response>
    /// <response code="400">La empresa del contexto no existe.</response>
    /// <response code="422">Validación de formato de las designaciones o contabilidad aún no habilitada.</response>
    [HttpPut("profile/fiscal")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateFiscal(
        [FromBody] UpdateCompanyFiscalCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza la configuración de operación de la empresa activa (idioma principal).
    /// </summary>
    /// <remarks>
    /// El idioma es obligatorio y debe pertenecer a los idiomas soportados.
    /// </remarks>
    /// <param name="command">Idioma principal de la empresa.</param>
    /// <response code="200">Configuración de operación actualizada.</response>
    /// <response code="400">La empresa del contexto no existe.</response>
    /// <response code="422">Idioma ausente o no soportado.</response>
    [HttpPut("profile/operation")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateOperation(
        [FromBody] UpdateCompanyOperationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza la configuración de documentos de la empresa activa (leyenda adicional).
    /// </summary>
    /// <remarks>
    /// La leyenda adicional admite hasta 500 caracteres.
    /// </remarks>
    /// <param name="command">Configuración de documentos.</param>
    /// <response code="200">Configuración de documentos actualizada.</response>
    /// <response code="400">La empresa del contexto no existe.</response>
    /// <response code="422">Validación de campos (longitud de la leyenda).</response>
    [HttpPut("profile/documents")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateDocuments(
        [FromBody] UpdateCompanyDocumentsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Obtiene la identidad visual de la empresa activa (colores, eslogan y pie de documentos).
    /// </summary>
    /// <response code="200">Identidad visual de la empresa activa.</response>
    [HttpGet("profile/branding")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBrandingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBranding(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCompanyBrandingQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza la identidad visual de la empresa activa (colores, eslogan y pie de documentos).
    /// </summary>
    /// <remarks>
    /// Los colores deben ser hexadecimales válidos (`#RGB` o `#RRGGBB`). Los valores se guardan como
    /// configuración de la empresa; los logos se suben con `profile/logo` y `profile/logo-alt`.
    /// </remarks>
    /// <param name="command">Colores, eslogan y pie de documentos.</param>
    /// <response code="200">Identidad visual actualizada.</response>
    /// <response code="422">Validación de campos (colores hexadecimales, longitudes).</response>
    [HttpPut("profile/branding")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(typeof(ApiResponse<CompanyBrandingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateBranding(
        [FromBody] UpdateCompanyBrandingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Sube el logo alternativo de la empresa activa, reemplazando el anterior.
    /// </summary>
    /// <remarks>
    /// Mismas reglas que el logo principal: `multipart/form-data` con el campo `file`, imagen PNG, JPG
    /// o WEBP de hasta 5 MB y 4000 x 4000 px. Se descarga con `GET profile/logo-alt/content`.
    /// </remarks>
    /// <param name="file">Imagen del logo alternativo.</param>
    /// <response code="200">Logo alternativo guardado; devuelve el perfil actualizado.</response>
    /// <response code="400">No se adjuntó archivo o la empresa del contexto no existe.</response>
    /// <response code="422">Archivo vacío, sin nombre, demasiado grande, de formato no soportado o con dimensiones excesivas.</response>
    [HttpPost("profile/logo-alt")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<CompanyProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadLogoAlt(
        IFormFile? file,
        CancellationToken cancellationToken = default
    )
    {
        if (file is null || file.Length == 0)
            return this.ApiBadRequest("Debe adjuntar un archivo de imagen.");

        await using var upload = await BufferedFormFile.CreateAsync(file, cancellationToken);
        var result = await _mediator.Send(
            new UploadCompanyLogoAltCommand(upload.Content),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Descarga el contenido binario del logo alternativo de la empresa activa.
    /// </summary>
    /// <remarks>
    /// La respuesta es la imagen (PNG, JPG o WEBP) con el `Content-Type` guardado, no un envelope.
    /// </remarks>
    /// <response code="200">Imagen del logo alternativo.</response>
    /// <response code="404">La empresa no tiene logo alternativo configurado o el archivo no está disponible.</response>
    [HttpGet("profile/logo-alt/content")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    // IFormFile solo marca el cuerpo binario (Swagger lo mapea a string/binary); la respuesta real
    // es un File con el Content-Type guardado (PNG, JPG o WEBP).
    [ProducesResponseType(typeof(IFormFile), StatusCodes.Status200OK, "image/png", "image/jpeg", "image/webp")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogoAltContent(
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(new GetCompanyLogoAltContentQuery(), cancellationToken);
        return this.ToFileOrNotFound(
            result,
            content => File(content.Content, content.ContentType, content.FileName)
        );
    }

    /// <summary>
    /// Obtiene la política fiscal de ventas vigente de la empresa activa (monto máximo para Consumidor
    /// Final).
    /// </summary>
    /// <response code="200">Política fiscal de ventas efectiva.</response>
    [HttpGet("profile/fiscal-policy")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(
        typeof(ApiResponse<ERP.Application.Modules.Sales.DTOs.SalesFiscalPolicyDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetFiscalPolicy(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetSalesFiscalPolicyQuery(), cancellationToken);
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Obtiene el checklist de preparación operativa de la empresa activa y de la sucursal del
    /// contexto.
    /// </summary>
    /// <remarks>
    /// Solo lectura: orquesta las fuentes y resolvers existentes
    /// (`ICompanyOperationalReadinessResolver`) y no persiste nada. Siempre responde 200; los
    /// pendientes viajan dentro del cuerpo.
    /// </remarks>
    /// <response code="200">Checklist de preparación operativa.</response>
    [HttpGet("operational-readiness")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesView}")]
    [ProducesResponseType(
        typeof(ApiResponse<CompanyOperationalReadinessDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetOperationalReadiness(
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(
            new GetCompanyOperationalReadinessQuery(),
            cancellationToken
        );
        return this.ToOkOrBadRequest(result);
    }

    /// <summary>
    /// Actualiza el monto máximo de venta a Consumidor Final de la empresa activa.
    /// </summary>
    /// <remarks>
    /// Se guarda como configuración de la empresa con dos decimales y el cambio queda en el registro de
    /// cambios de configuración. No puede ser negativo. Devuelve la política efectiva resultante.
    /// </remarks>
    /// <param name="command">Nuevo monto máximo para Consumidor Final.</param>
    /// <response code="200">Política fiscal de ventas actualizada.</response>
    /// <response code="422">El monto es negativo.</response>
    [HttpPut("profile/fiscal-policy")]
    [Authorize(Policy = $"perm:{SettingsPermissions.CompaniesUpdate}")]
    [ProducesResponseType(
        typeof(ApiResponse<ERP.Application.Modules.Sales.DTOs.SalesFiscalPolicyDto>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateFiscalPolicy(
        [FromBody] UpdateConsumerFinalMaxAmountCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _mediator.Send(command, cancellationToken);
        return this.ToOkOrBadRequest(result);
    }
}
