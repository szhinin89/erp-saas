using System.Security.Cryptography;
using System.Text;
using ERP.Application.Common;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.Deployment;

public sealed class DeploymentFeatureFlags : IDeploymentFeatureFlags
{
    private const string InitialSuperAdminSetupTokenKey = "Deployment:InitialSuperAdminSetupToken";

    private readonly IConfiguration _configuration;
    private readonly InstanceQuotaFileStore _instanceQuotaFile;

    public DeploymentFeatureFlags(IConfiguration configuration, InstanceQuotaFileStore instanceQuotaFile)
    {
        _configuration = configuration;
        _instanceQuotaFile = instanceQuotaFile;
    }

    public bool IsSuperAdminPanelEnabled
    {
        get
        {
            var v = _configuration["Deployment:SuperAdminPanelEnabled"];
            if (string.IsNullOrWhiteSpace(v))
                return true;
            return bool.TryParse(v, out var parsed) && parsed;
        }
    }

    public int? MaxActiveSubscribers => MergePositiveInt(
        _instanceQuotaFile.Read()?.MaxActiveSubscribers,
        ReadPositiveCap("Deployment:MaxActiveSubscribers"));

    public int? MaxIdentityUsers => MergePositiveInt(
        _instanceQuotaFile.Read()?.MaxIdentityUsers,
        ReadPositiveCap("Deployment:MaxIdentityUsers"));

    public bool IsDedicatedSingleClientInstance
    {
        get
        {
            var f = _instanceQuotaFile.Read()?.DedicatedSingleClientInstance;
            if (f.HasValue)
                return f.Value;
            return bool.TryParse(_configuration["Deployment:DedicatedSingleClientInstance"], out var p) && p;
        }
    }

    public int? MaxUsersPerSubscriber => MergePositiveInt(
        _instanceQuotaFile.Read()?.MaxUsersPerSubscriber,
        ReadPositiveCap("Deployment:MaxUsersPerTenant"));

    /// <inheritdoc />
    public bool AuthorizeInitialSuperAdminSetup(string? submittedToken)
    {
        var expected = _configuration[InitialSuperAdminSetupTokenKey];
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(submittedToken))
            return false;

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected.Trim()));
        var submittedHash = SHA256.HashData(Encoding.UTF8.GetBytes(submittedToken.Trim()));
        return CryptographicOperations.FixedTimeEquals(expectedHash, submittedHash);
    }

    private static int? MergePositiveInt(int? fromFile, int? fromConfig)
    {
        if (fromFile is > 0)
            return fromFile;
        return fromConfig;
    }

    /// <summary>
    /// Entero &gt; 0 = tope. Vacío, unlimited, no numérico o ≤ 0 = sin tope (ilimitado).
    /// </summary>
    private int? ReadPositiveCap(string key)
    {
        var v = _configuration[key];
        if (string.IsNullOrWhiteSpace(v))
            return null;
        if (string.Equals(v, "unlimited", StringComparison.OrdinalIgnoreCase))
            return null;
        if (!int.TryParse(v, out var n) || n <= 0)
            return null;
        return n;
    }
}
