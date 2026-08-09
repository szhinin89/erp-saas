using System.Reflection;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Ride.Enums;
using ERP.Infrastructure.Ride.ElectronicDocumentsAdapter;
using FluentAssertions;
using Xunit;

namespace ERP.Infrastructure.Tests.Ride;

/// <summary>
/// Unit test puro (sin Testcontainers) para el mapper privado
/// <c>ElectronicDocumentRideSourceXmlProvider.MapDocumentType</c> — el único punto que traduce
/// <see cref="ElectronicDocumentType"/> (string, DTO) a <see cref="RideDocumentType"/> (ADR-025 §8).
/// Recorre <see cref="ElectronicDocumentType"/> por reflexión en vez de hardcodear la lista: si se
/// agrega un nuevo tipo documental electrónico sin agregar su espejo en <see cref="RideDocumentType"/>
/// y el switch del mapper, este test falla en vez de dejarlo como un <see cref="InvalidOperationException"/>
/// solo detectable en runtime.
/// </summary>
public sealed class ElectronicDocumentRideSourceXmlProviderMapDocumentTypeTests
{
    private static readonly MethodInfo MapDocumentTypeMethod = typeof(
        ElectronicDocumentRideSourceXmlProvider
    ).GetMethod(
        "MapDocumentType",
        BindingFlags.NonPublic | BindingFlags.Static
    ) ?? throw new InvalidOperationException("MapDocumentType no encontrado por reflexión.");

    public static IEnumerable<object[]> AllElectronicDocumentTypes() =>
        Enum.GetNames<ElectronicDocumentType>().Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(AllElectronicDocumentTypes))]
    public void Every_ElectronicDocumentType_member_maps_to_its_RideDocumentType_mirror(
        string electronicDocumentTypeName
    )
    {
        var expected = Enum.Parse<RideDocumentType>(electronicDocumentTypeName);

        var actual = (RideDocumentType)MapDocumentTypeMethod.Invoke(null, [electronicDocumentTypeName])!;

        actual.Should().Be(expected);
    }

    [Fact]
    public void Unknown_document_type_name_throws_instead_of_silently_defaulting()
    {
        var act = () => MapDocumentTypeMethod.Invoke(null, ["NotARealDocumentType"]);

        act.Should()
            .Throw<TargetInvocationException>()
            .WithInnerException<InvalidOperationException>();
    }
}
