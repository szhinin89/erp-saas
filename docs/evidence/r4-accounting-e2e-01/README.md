# R4 — Contabilidad — PASS 4/4, CLOSED

Fecha: 2026-10-28. **CLOSED por aprobación expresa del propietario: 4/4 PASS**,
limitado a #26, #27, #28 y #42 ejecutados. Empresa B `f74b2da3-83b2-4e26-a273-93efd29b6719`,
únicamente `erp_zh_test`. Clasificación de implementación: **KEEP**; ningún BUG REAL
reproducido, sin DEBT/FALTA que requiera implementación en este alcance.

| Escenario | Resultado | Evidencia |
|---|---|---|
| #26 Manual publicado y reversado | PASS | UI crea/publica asiento 25 por 7.25 (Suministros/Banco), recarga y reversa en asiento 26; original Posted intacto, reverso espejo enlazado. API/SQL corroboran; republicación rechazada |
| #27 Diario, Mayor, Balance y Resultados | PASS | 62 líneas exactas Diario SQL/API, 27 asientos, Debe/Haber 1820.15; saldos de 109 cuentas del Mayor comparados con SQL. Balance 780.63 = 315.70 + 464.93; resultado 96.43 coincide con Estado de Resultados. Los cuatro reportes abiertos en UI |
| #28 Fallo, configuración, reproceso y readiness | PASS | Gasto Confirmed de 2.00 con regla temporalmente desactivada: Failed RULE_NOT_FOUND, sin asiento; readiness POSTINGS_UNRESOLVED=1. Regla restaurada por API, reproceso UI: Posted, asiento 27, dos intentos durables Failed/Posted. Repetir reproceso conserva asiento y dos intentos |
| #42 Ejercicio 2027 | PASS | Generado por UI: 12 meses calendario Open, enero a diciembre sin huecos ni solapes, febrero de 28 días. Recarga, API y SQL confirman; regenerar por API devuelve createdCount=0 |

## Evidencia y conciliación

- [Manual publicado](manual-posted.png), [reverso](manual-reversed.png).
- [Fallo durable](posting-failed.png), [reproceso persistido](posting-reprocessed.png).
- [Diario](report-journal.png), [Mayor](report-ledger.png), [Balance](report-balance.png),
  [Resultados](report-income.png), [readiness](readiness.png), [períodos 2027](periods-2027.png).
- [API final](verification.json), [fallo y readiness previo](failure.json),
  [PostgreSQL](postgres.json), [comparaciones focales](checks.json),
  [aislamiento del Diario](isolation-report.json).

Baseline conservada: 24 asientos de R1–R3, cada uno balanceado, y 20 obligaciones
comunes Posted; apertura con su modelo propio. Resultado anterior 98.43; el manual y
su reverso netean cero, el gasto R4 de 2.00 produce resultado final 96.43 y aumenta
CxP de 311.72 a 313.72. Ingresos 115.70, costos 12.27, gastos 7.00.
Activo permanece 780.63; pasivo total 315.70; capital 368.50 + resultado 96.43.
Caja 51.18, banco 89.25, CxC 205.50, inventario 424.83, IVA crédito 9.87.
Se conserva la anomalía histórica de R3: depósito 100.00 (asiento 20) y sobrante de
arqueo (21); no se interpretan como reversa bancaria ni se corrigen.
La UI consulta octubre hasta 28/10; la comparación API usa 01/01–28/10. Todos los
asientos del alcance están en octubre, por lo que ambos rangos contienen el mismo libro.

Readiness final: Open, hasEnded=false, canClose=false; desaparece
POSTINGS_UNRESOLVED y permanece YEAR_END_CLOSING_MISSING. Es el bloqueo esperado:
2026 termina el 31/12 y aún no tiene cierre anual. No se cerró ni bloqueó período,
no se generó asiento de cierre anual y no se afirmó que 2026 esté listo para cerrar.

Aislamiento API: desde A, detalle del manual, obligación y readiness del período de B
responden 404; Diario A no contiene IDs de asientos B; A conserva solo su período
2026, 32 asientos y sus 32 obligaciones Posted más un Failed histórico intacto.
No se acredita un segundo tenant, segundo usuario ni prueba de carreras concurrentes.

## Entorno, cambios y verificaciones

API Release del repositorio en :5004, Development, seed deshabilitado, ambas conexiones
(EF/Hangfire) apuntando a erp_zh_test. Vite :5181 con proxy a :5004. Pruebas focales
con Chromium/Playwright existente, peticiones HTTP reales y PostgreSQL 16 real.
El acceso se desbloqueó con autorización expresa del propietario: UPDATE exactamente
una fila de identity_users, e2e.admin, hash BCrypt factor 12 y renovación de security_stamp.
No se crearon usuarios, no se usó first-run y no se ejecutó seed.

Quedan en B los asientos 25–27, el gasto 004-004-000000028
(808b01d5-f126-4ebe-afb5-ef353d4e3ded), su obligación
bedfbdc6-4762-4c65-8d2d-6e711641740b Posted con dos intentos y los 12 períodos 2027.
La regla Expenses/DocumentConfirmed queda activa como antes. Ninguna otra base tocada.
No se repitieron R1–R3 ni mora. Sin BUG REAL, cambios de código, suites completas,
commit ni push. Los fallos de selección/espera de los scripts UI se corrigieron en el
harness local y no se clasifican como defectos del producto.

Archivos versionables modificados: STATUS.md, docs/agent/WORK_QUEUE.md y esta carpeta
de evidencia. FEATURES/SSOT sin cambios porque no cambiaron capacidades ni reglas.
Harness y archivos temporales en .local/r4 (ignorado). git diff --check PASS.
API/Vite propios detenidos; archivos temporales de credencial y tokens eliminados.

## Gate focal y aprobación del propietario

Las comprobaciones del agente no sustituyen el gate del propietario
(enforcement.md, flujo por fase, puntos 6–10). Gate focal de solo lectura:

```powershell
docker exec postgreszh psql -U postgres -d erp_zh_test -c "select j.entry_number,sum(l.debit) debe,sum(l.credit) haber from journal_entries j join journal_entry_lines l on l.journal_entry_id=j.id where j.company_id='f74b2da3-83b2-4e26-a273-93efd29b6719' and j.entry_number between 25 and 27 group by 1 order by 1; select status,attempts from accounting_postings where id='bedfbdc6-4762-4c65-8d2d-6e711641740b'; select count(*) meses from accounting_periods where company_id='f74b2da3-83b2-4e26-a273-93efd29b6719' and fiscal_year=2027;"
git diff --check
```

Esperado: 25=7.25/7.25, 26=7.25/7.25, 27=2.00/2.00; Posted/2; meses=12;
diff sin errores. El propietario aprueba R4: **4/4 PASS** y ordena registrar CLOSED,
conservar esta evidencia y preparar staging exclusivo de R4. Este cierre documental
no repite pruebas ni amplía el alcance; no acredita ejecución adicional del gate.
Sin commit ni push.
