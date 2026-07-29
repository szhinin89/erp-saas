using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace ERP.Infrastructure.Security;

public sealed class DataProtectionSecretProtector : ISecretProtector
{
    public const string ProtectedPrefix = "dp1:";

    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("ERP.SaaS.Secrets.v1");
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        return ProtectedPrefix + _protector.Protect(plaintext);
    }

    public string UnprotectOrPlaintext(string storedValue)
    {
        if (string.IsNullOrEmpty(storedValue))
            return storedValue;

        if (!IsProtected(storedValue))
            return storedValue;

        var payload = storedValue[ProtectedPrefix.Length..];
        return _protector.Unprotect(payload);
    }

    public bool IsProtected(string storedValue) =>
        !string.IsNullOrEmpty(storedValue)
        && storedValue.StartsWith(ProtectedPrefix, StringComparison.Ordinal);
}
