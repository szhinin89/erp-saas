using System.Reflection;
using ERP.API.Swagger;
using Microsoft.OpenApi.Models;

namespace ERP.API.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            // API-DOC-V1 / Fase 2A: ids de esquema portables (sin backticks ni PublicKeyToken); solo los
            // tipos ERP con nombre repetido se califican con su namespace.
            var schemaIds = new OpenApiSchemaIds(
                [
                    Assembly.GetExecutingAssembly(), // ERP.API
                    typeof(ERP.Application.DependencyInjection).Assembly, // ERP.Application
                    typeof(ERP.Domain.Common.BaseEntity).Assembly, // ERP.Domain
                ]
            );
            var schemaTypes = new OpenApiSchemaTypeRegistry();
            options.CustomSchemaIds(schemaIds.For);
            options.CustomOperationIds(OpenApiOperationIds.Base);
            options.SupportNonNullableReferenceTypes();
            options.SchemaFilter<OpenApiSchemaTypeRegistryFilter>(schemaTypes);
            options.DocumentFilter<OpenApiOperationIdsDocumentFilter>();
            options.DocumentFilter<OpenApiSchemaContractDocumentFilter>(schemaTypes);

            options.MapType<IFormFile>(() =>
                new OpenApiSchema { Type = "string", Format = "binary" }
            );

            options.SwaggerDoc(
                "v1",
                new OpenApiInfo
                {
                    Title = "ERP SaaS API",
                    Version = "v1",
                    Description = """
                    API multi-tenant del ERP SaaS de ZH Technologies.

                    **Autenticación:** los endpoints con candado exigen un JWT Bearer; los marcados como
                    públicos (`AllowAnonymous`) no. Obtén el token con `POST /api/v1/auth/login` y pégalo
                    en **Authorize**. Cada operación indica en su descripción los permisos `perm:*`
                    que exige.

                    **Multi-tenant:** el tenant se identifica por el claim `tenant_id` del JWT; cada
                    solicitud opera solo sobre los datos del tenant autenticado.

                    **Respuestas:** el éxito y los errores de negocio viajan en un envelope común
                    (`code`, `severity`, `message`, `data`, `meta`). Los rechazos del middleware de
                    autenticación/autorización (401/403) no llevan cuerpo.
                    """,
                }
            );

            // Botón Authorize en Swagger UI para pegar el JWT
            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Ingresa el token JWT (sin el prefijo 'Bearer').",
            };

            options.AddSecurityDefinition(OpenApiAuthorizationOperationFilter.BearerSchemeId, securityScheme);

            // La exigencia de Bearer se declara por operación (OpenApiAuthorizationOperationFilter):
            // un requisito global haría que los endpoints AllowAnonymous parecieran protegidos.

            // Incluir comentarios XML (controllers + modelos/DTOs)
            // note: requiere <GenerateDocumentationFile>true</GenerateDocumentationFile> en los .csproj.
            var assembliesToDocument = new[]
            {
                Assembly.GetExecutingAssembly(), // ERP.API
                typeof(ERP.Application.DependencyInjection).Assembly, // ERP.Application
                typeof(ERP.Domain.Common.BaseEntity).Assembly, // ERP.Domain
            };

            foreach (var asm in assembliesToDocument.Distinct())
            {
                var xmlFile = $"{asm.GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                    options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }

            // Deben registrarse DESPUÉS de los comentarios XML: el filtro XML asigna
            // operation.Description (<remarks>) y sobrescribiría la sección «Autorización». Aquí se
            // anexa a lo que ya escribió el XML y el contrato de errores completa lo declarado.
            options.OperationFilter<OpenApiAuthorizationOperationFilter>();
            options.OperationFilter<OpenApiErrorResponsesOperationFilter>(schemaIds);

            // API-DOC-V1 / Fase 2D: solo metadato (ejemplos y descripción de cuerpos multipart); va al
            // final porque el 422 común lo añade el filtro de errores anterior.
            options.OperationFilter<OpenApiMultipartBodyDescriptionFilter>();
            options.OperationFilter<OpenApiExamplesOperationFilter>();
        });

        return services;
    }
}
