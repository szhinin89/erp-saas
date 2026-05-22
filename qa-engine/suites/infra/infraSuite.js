import { request } from '../../core/http.js';

/** @param {import('../../core/runner.js').QaRunner} runner @param {string} api @param {string} frontend @param {import('../../seed/seedEngine.js').SeedContext} _ctx */
export async function runInfraSuite(runner, api, frontend, _ctx) {
  await runner.run(
    'infra.api.health',
    'API health live',
    'infra',
    true,
    async ({ trace }) => {
      trace.push({ step: 'GET /health/live', at: new Date().toISOString() });
      const r = await request('GET', `${api}/health/live`);
      if (!r.ok) throw new Error(`API down: ${r.status}`);
      return { 'health.live.status': r.status };
    },
    { retry: true },
  );

  await runner.run(
    'infra.api.ready',
    'API health ready',
    'infra',
    false,
    async ({ trace }) => {
      trace.push({ step: 'GET /health/ready', at: new Date().toISOString() });
      const r = await request('GET', `${api}/health/ready`);
      if (!r.ok) throw new Error(`API not ready: ${r.status}`);
      return { 'health.ready.status': r.status };
    },
    { retry: true },
  );

  await runner.run(
    'infra.frontend.reachable',
    'Frontend reachable',
    'infra',
    true,
    async ({ trace }) => {
      trace.push({ step: 'GET /', at: new Date().toISOString() });
      const r = await request('GET', `${frontend}/`);
      if (!r.ok) throw new Error(`Frontend down: ${r.status}`);
      return { 'frontend.status': r.status };
    },
    { retry: true },
  );
}
