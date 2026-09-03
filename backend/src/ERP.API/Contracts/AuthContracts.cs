namespace ERP.API.Contracts;

public sealed record SwitchCompanyRequest(Guid CompanyId);

public sealed record OperateCompanyRequest(Guid CompanyId);
