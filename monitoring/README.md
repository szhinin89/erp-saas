# Monitoring (preparación)

Estructura para observabilidad futura. **Stack producto actual:** logs Serilog (consola/archivo), health checks API.

| Carpeta | Uso planificado |
|---------|-----------------|
| `grafana/` | Dashboards |
| `prometheus/` | Métricas scrape config |
| `loki/` | Logs agregados |
| `dashboards/` | JSON exportados |
| `alerts/` | Reglas alerta |

> Prometheus/Datadog no están en stack allowlist hasta aprobación explícita (`docs/DEVELOPMENT.md`).

Relacionado: [`infrastructure/monitoring/`](../infrastructure/monitoring/) (enlace ops).
