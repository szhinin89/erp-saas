using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Services;

/// <summary>Calcula el hash determinístico (SHA-256) del XML autorizado — función pura, sin I/O.</summary>
public interface IRideContentHasher
{
    RideContentHash Compute(string authorizedXml);
}
