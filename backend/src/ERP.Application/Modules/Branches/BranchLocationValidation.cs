using ERP.Domain.Geography.Interfaces;

namespace ERP.Application.Modules.Branches;

internal static class BranchLocationValidation
{
    public static async Task<string?> ValidateAsync(
        IGeographyReadRepository geo,
        string? countryId,
        string? provinceId,
        string? cantonId,
        string? parishId,
        CancellationToken cancellationToken)
    {
        var c = string.IsNullOrWhiteSpace(countryId) ? null : countryId.Trim();
        var p = string.IsNullOrWhiteSpace(provinceId) ? null : provinceId.Trim();
        var k = string.IsNullOrWhiteSpace(cantonId) ? null : cantonId.Trim();
        var r = string.IsNullOrWhiteSpace(parishId) ? null : parishId.Trim();

        if (c is null && p is null && k is null && r is null)
            return null;

        if (p is not null && c is null)
            return "Debe indicar el país cuando selecciona provincia.";

        if (k is not null && p is null)
            return "Debe indicar la provincia cuando selecciona cantón.";

        if (r is not null && k is null)
            return "Debe indicar el cantón cuando selecciona parroquia.";

        if (c is not null)
        {
            var countries = await geo.GetCountriesAsync(cancellationToken);
            if (countries.All(x => x.Iso2 != c))
                return "País no válido.";
        }

        if (p is not null)
        {
            var provinces = await geo.GetProvincesByCountryAsync(c!, cancellationToken);
            if (provinces.All(x => x.Id != p))
                return "Provincia no válida para el país seleccionado.";
        }

        if (k is not null)
        {
            var cantons = await geo.GetCantonsByProvinceAsync(p!, cancellationToken);
            if (cantons.All(x => x.Id != k))
                return "Cantón no válido para la provincia seleccionada.";
        }

        if (r is not null)
        {
            var parishes = await geo.GetParishesByCantonAsync(k!, cancellationToken);
            if (parishes.All(x => x.Id != r))
                return "Parroquia no válida para el cantón seleccionado.";
        }

        return null;
    }
}
