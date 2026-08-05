using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ERP.API.Extensions;

public static class JwtExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var secretKey =
            configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException(
                "Falta 'Jwt:SecretKey' en la configuración. "
                    + "Usar User Secrets o variable de entorno JWT__SECRETKEY."
            );

        var issuer =
            configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Falta 'Jwt:Issuer' en la configuración.");

        var audience =
            configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Falta 'Jwt:Audience' en la configuración.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                };
            });

        return services;
    }
}
