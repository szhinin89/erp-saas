namespace ERP.Domain.Subscriptions;

/// <summary>
/// Tipo de feature comercializable. Todo módulo o pantalla del producto debe mapearse a un <see cref="Entities.SaasFeatureDefinition"/>
/// con el <c>ResourceRef</c> adecuado (p. ej. clave de permiso o código de formulario) antes de habilitarlo en planes.
/// </summary>
public enum SaasFeatureKind : byte
{
    Module = 0,
    Form = 1,
    Quota = 2,
    Integration = 3,
}
