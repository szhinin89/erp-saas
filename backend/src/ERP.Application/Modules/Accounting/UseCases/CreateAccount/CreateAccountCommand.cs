using ERP.Application.Common;
using ERP.Application.Accounting.DTOs;
using ERP.Domain.Accounting.Enums;

namespace ERP.Application.Accounting.UseCases.CreateAccount;

public record CreateAccountCommand(
    string Code,
    string Name,
    AccountType Type,
    AccountNature Nature,
    Guid? ParentId
);
