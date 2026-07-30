using ERP.Domain.Modules.Ride.ValueObjects;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Modules.Ride.Services;

/// <summary>Hash SHA-256 real del XML autorizado — reemplaza a <c>NullRideContentHasher</c> (Fase 4).</summary>
public sealed class RideContentHasher : IRideContentHasher
{
    public RideContentHash Compute(string authorizedXml)
    {
        var bytes = Encoding.UTF8.GetBytes(authorizedXml);
        var hash = SHA256.HashData(bytes);
        return RideContentHash.Create(Convert.ToHexStringLower(hash));
    }
}
