namespace ERP.Application.Sales.Helpers;

/// <summary>
/// Genera la clave de acceso de 49 dígitos exigida por el SRI Ecuador (XSD v1.1.0).
/// Formato: ddMMyyyy(8) + TipoDoc(2) + RUC(13) + Ambiente(1) + Estab(3) + PtoEmi(3) + Secuencial(9) + CodNum(8) + TipoEmision(1) + Verificador(1)
/// </summary>
public static class ClaveAccesoHelper
{
    public static string Generar(
        string   rucEmpresa,
        int      ambiente,
        string   establecimiento,
        string   puntoEmision,
        int      tipoEmision,
        string   secuencial,
        DateTime fechaEmision,
        string   tipoDocumento = "01")
    {
        var fechaStr       = fechaEmision.ToString("ddMMyyyy");
        var ruc            = rucEmpresa.PadLeft(13, '0');
        var estab          = establecimiento.PadLeft(3, '0');
        var pto            = puntoEmision.PadLeft(3, '0');
        var codigoNumerico = GenerarCodigoNumerico();

        var clave48 = $"{fechaStr}{tipoDocumento}{ruc}{ambiente}{estab}{pto}{secuencial}{codigoNumerico}{tipoEmision}";

        var digito = CalcularDigitoVerificador(clave48);
        return $"{clave48}{digito}";
    }

    /// <summary>Módulo 11 según ficha técnica SRI Ecuador.</summary>
    public static int CalcularDigitoVerificador(string clave48Digitos)
    {
        if (clave48Digitos.Length != 48)
            throw new ArgumentException($"La clave debe tener exactamente 48 dígitos (recibidos: {clave48Digitos.Length}).", nameof(clave48Digitos));

        var pesos = new[] { 2, 3, 4, 5, 6, 7 };
        var suma  = 0;
        for (var i = 0; i < 48; i++)
            suma += (clave48Digitos[i] - '0') * pesos[i % 6];

        var residuo = suma % 11;
        return residuo == 0 ? 0 : residuo == 1 ? 1 : 11 - residuo;
    }

    private static string GenerarCodigoNumerico()
        => Random.Shared.Next(10_000_000, 99_999_999).ToString();
}
