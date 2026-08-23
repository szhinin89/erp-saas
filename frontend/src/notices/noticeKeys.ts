/** SSOT de claves de avisos compactos reutilizables (contenido estático propiedad de Notice).
 * Los avisos calculados por lógica de dominio (p. ej. readiness de líneas de Compras) no pasan
 * por aquí — usan `buildNotice()` con su propio texto ya resuelto (`source: "domain-status"`). */
export const NOTICE_KEYS = {} as const;

export type NoticeKeyId = (typeof NOTICE_KEYS)[keyof typeof NOTICE_KEYS];
