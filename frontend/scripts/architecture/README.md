# Architecture validation (frontend)

Los guardrails de arquitectura frontend se ejecutan desde el repo root:

```powershell
./tools/architecture/check-architecture-guardrails.ps1 -FrontendChunkOnly
```

Tras `npm run build`, valida tamaño del chunk `index-*.js` en `frontend/dist/assets/`.

Unit tests: `frontend/src/**/*.test.ts` (Vitest).
