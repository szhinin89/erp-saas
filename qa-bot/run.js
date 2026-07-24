/**
 * QA Regression Bot — thin wrapper around qa-engine (backward compatible entry point).
 */
import { loadConfig } from '../qa-engine/core/config.js';
import { runQaEngine } from '../qa-engine/run.js';

const config = loadConfig();
const report = await runQaEngine(config);

if (report.status === 'FAILED') {
  console.error('QA REGRESSION FAILED');
  process.exit(1);
}

console.log('QA REGRESSION PASSED');
