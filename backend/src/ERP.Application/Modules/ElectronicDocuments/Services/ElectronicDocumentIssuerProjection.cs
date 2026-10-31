using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.SriCatalogs.Entities;
using CompanyEntity = ERP.Domain.Modules.Company.Entities.Company;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// ADR-038 D5 — única proyección del perfil fiscal del emisor (<see cref="CompanyEntity"/>) al
/// modelo común <see cref="ElectronicDocumentIssuerData"/>. La invocan los providers de Factura,
/// Nota de crédito y Retención con la empresa y la matriz que ya cargaron y validaron; ningún
/// provider vuelve a armar el emisor ni a repetir reglas del perfil.
///
/// Las designaciones por resolución (Anexos 21 y 24) se evalúan contra la fecha de emisión de
/// negocio del comprobante (ADR-034): una designación que rige después de esa fecha no aplica. El
/// perfil se proyecta en cada generación de XML; los comprobantes firmados o autorizados nunca se
/// regeneran (ADR-038 D11).
/// </summary>
public static class ElectronicDocumentIssuerProjection
{
    /// <summary>Anexo 22 — leyenda única que admiten los XSD oficiales embebidos (tipo
    /// <c>contribuyenteRimpe</c>). RIMPE Negocio Popular no llega a generar XML: lo bloquea
    /// Readiness (R20).</summary>
    public const string RimpeLegend = "CONTRIBUYENTE RÉGIMEN RIMPE";

    public static ElectronicDocumentIssuerData From(
        CompanyEntity company,
        string matrixAddress,
        DateOnly issueDate
    ) =>
        new(
            TaxId: company.TaxIdentificationNumber,
            LegalName: company.LegalName,
            TradeName: company.TradeName,
            MatrixAddress: matrixAddress,
            TaxRegime: ResolveRimpeLegend(company.TaxRegime),
            IsAccountingRequired: company.IsAccountingReq,
            SpecialTaxpayerNumber: company.SpecialTaxpayerNo,
            RetentionAgentResolutionNumber: company.RetentionAgentResolutionFor(issueDate),
            LargeTaxpayerResolutionNumber: company.LargeTaxpayerResolutionFor(issueDate)
        );

    /// <summary>
    /// El elemento <c>contribuyenteRimpe</c> solo existe para contribuyentes RIMPE, determinado
    /// desde el catálogo oficial <c>sri_tax_regime</c> (columna <c>Abbrev</c>: "RIMPE_ME"/"RIMPE_NP"),
    /// nunca comparando códigos numéricos. Régimen General devuelve <c>null</c> y el elemento se omite.
    /// </summary>
    private static string? ResolveRimpeLegend(SriTaxRegime? taxRegime) =>
        taxRegime?.Abbrev?.StartsWith("RIMPE", StringComparison.OrdinalIgnoreCase) == true
            ? RimpeLegend
            : null;
}
