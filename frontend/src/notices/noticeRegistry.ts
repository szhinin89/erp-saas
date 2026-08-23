import type { NoticeTemplate } from "./noticeTypes";

/** Único punto de datos para avisos estáticos reutilizables (equivalente a helpRegistry.ts).
 * Tipado por `string` (no por `NoticeKeyId`) porque NOTICE_KEYS está vacío hasta que exista un
 * primer aviso genérico reutilizable — `Record<never, T>` rompería la indexación en build. El
 * primer caso de uso (Compras) es contenido de dominio, no estático; ver `buildNotice()` en
 * noticeResolver.ts. */
export const NOTICE_REGISTRY: Record<string, NoticeTemplate> = {};
