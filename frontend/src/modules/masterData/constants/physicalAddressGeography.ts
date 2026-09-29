/**
 * País del catálogo geográfico de las direcciones de socios de negocio (BP locations).
 *
 * Restricción deliberada del dominio, no un default configurable: `PhysicalAddress`
 * (backend `ERP.Domain/MasterData/ValueObjects/PhysicalAddress`) es "dirección física con
 * codificación geográfica INEC Ecuador" — no tiene campo país y guarda solo códigos INEC
 * (provincia 2 / cantón 4 / parroquia 6 dígitos) con FK a `global.geo_*`, catálogo sembrado
 * únicamente con `country_id = 'EC'`. Es el `countryId` (ISO-3166 alpha-2) que recibe
 * `geographyLookupFacade.provinces()`.
 *
 * No confundir con `BusinessPartner.countryCode` (nacionalidad/residencia fiscal del socio):
 * un socio extranjero puede tener una ubicación con dirección INEC.
 * Si el ERP admite direcciones fuera de Ecuador, esto deja de ser constante: `PhysicalAddress`
 * necesitaría un país propio y este valor debe salir de la dirección.
 */
export const PHYSICAL_ADDRESS_GEO_COUNTRY_ID = "EC";
