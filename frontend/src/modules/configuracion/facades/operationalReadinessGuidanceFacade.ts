/**
 * operationalReadinessGuidanceFacade — superficie pública de presentación de requisitos de
 * Readiness para consumidores externos (caja: habilitación preventiva al abrir la jornada).
 *
 * Expone únicamente la presentación de un requisito (qué falta, por qué importa y dónde se
 * corrige) y la variante de su estado fiscal; nunca calcula ni decide requisitos — esos llegan del
 * backend como códigos. Los módulos externos deben importar desde aquí, nunca directamente de
 * configuracion/empresa/sections.
 */
export {
  ReadinessRequirementRow,
  fiscalBadgeVariant,
} from "../empresa/sections/ReadinessRequirementRow";
