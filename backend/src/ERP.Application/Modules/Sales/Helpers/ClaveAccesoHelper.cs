namespace ERP.Application.Sales.Helpers;

public static class ClaveAccesoHelper
{
    public static string Generar(
        string   ruc,
        int      environment,
        string   estabCode,
        string   emPointCode,
        int      emissionType,
        string   sequential,
        DateTime issueDate,
        string   docType = "01")
    {
        var fechaStr       = issueDate.ToString("ddMMyyyy");
        var rucPadded      = ruc.PadLeft(13, '0');
        var estab          = estabCode.PadLeft(3, '0');
        var pto            = emPointCode.PadLeft(3, '0');
        var codigoNumerico = GenerarCodigoNumerico();

        var clave48 = $"{fechaStr}{docType}{rucPadded}{environment}{estab}{pto}{sequential}{codigoNumerico}{emissionType}";

        var digito = CalcularDigitoVerificador(clave48);
        return $"{clave48}{digito}";
    }

    public static int CalcularDigitoVerificador(string clave48Digitos)
    {
        if (clave48Digitos.Length != 48)
            throw new ArgumentException($"La clave debe tener exactamente 48 digitos (recibidos: {clave48Digitos.Length}).", nameof(clave48Digitos));

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

