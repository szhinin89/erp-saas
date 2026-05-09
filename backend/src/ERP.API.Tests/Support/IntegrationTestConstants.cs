namespace ERP.API.Tests.Support;

internal static class IntegrationTestConstants
{
    /// <summary>Clave simétrica ≥ 256 bits para pruebas (debe coincidir con <see cref="TestJwtFactory"/>).</summary>
    public const string JwtSecretKey =
        "ZH-Technologies-ERP-IntegrationTests-SecretKey-32chars-min!!";
}
