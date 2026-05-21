namespace ERP.Domain.Exceptions;

/// <summary>RUC/TaxId already registered globally in <c>company</c> (uq_company_ruc).</summary>
public sealed class CompanyRucAlreadyExistsException : Exception
{
    public const string ErrorCode = "company.ruc_already_exists";

    public string TaxId { get; }

    public CompanyRucAlreadyExistsException(string taxId)
        : base($"El RUC '{taxId}' ya está registrado en el sistema.")
    {
        TaxId = taxId;
    }
}
