using System.Reflection;
using Microsoft.OpenApi.Models;

namespace ERP.API.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            // Evita colisiones cuando existen tipos con el mismo nombre en distintos namespaces.
            options.CustomSchemaIds(t => t.FullName);

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
                    API multi-tenant para el sistema ERP SaaS de ZH Technologies.

                    **Autenticación:** todos los endpoints marcados con 🔒 requieren un JWT Bearer.
                    Obtén el token con `POST /api/auth/login` y pégalo en el botón **Authorize**.

                    **Multi-tenant:** el tenant se identifica mediante el claim `tenant_id` dentro del JWT.
                    Cada request filtra automáticamente los datos del tenant autenticado.

                    **Base de datos (producción):** aplicar migraciones de EF Core solo en ventana controlada,
                    con backup previo: `dotnet ef database update --project src/ERP.Infrastructure/ERP.Infrastructure.csproj
                    --startup-project src/ERP.API/ERP.API.csproj --connection "<cadena de producción>"`.
                    No ejecutar contra producción desde entornos no auditados.
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

            options.AddSecurityDefinition("Bearer", securityScheme);

            options.AddSecurityRequirement(
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer",
                            },
                        },
                        Array.Empty<string>()
                    },
                }
            );

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
        });

        return services;
    }
}
