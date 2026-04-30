using System.Reflection;
using Microsoft.OpenApi.Models;

namespace ERP.API.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title       = "ERP SaaS API",
                Version     = "v1",
                Description = """
                    API multi-tenant para el sistema ERP SaaS de ZH Technologies.

                    **Autenticación:** todos los endpoints marcados con 🔒 requieren un JWT Bearer.
                    Obtén el token con `POST /api/auth/login` y pégalo en el botón **Authorize**.

                    **Multi-tenant:** el tenant se identifica mediante el claim `tenant_id` dentro del JWT.
                    Cada request filtra automáticamente los datos del tenant autenticado.
                    """
            });

            // Botón Authorize en Swagger UI para pegar el JWT
            var securityScheme = new OpenApiSecurityScheme
            {
                Name         = "Authorization",
                Type         = SecuritySchemeType.Http,
                Scheme       = "bearer",
                BearerFormat = "JWT",
                In           = ParameterLocation.Header,
                Description  = "Ingresa el token JWT (sin el prefijo 'Bearer')."
            };

            options.AddSecurityDefinition("Bearer", securityScheme);

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id   = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Incluir comentarios XML de los controllers y DTOs
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);
        });

        return services;
    }
}
