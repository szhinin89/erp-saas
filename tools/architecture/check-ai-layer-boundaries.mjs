/**
 * check-ai-layer-boundaries.mjs
 *
 * Validates that AI/ML tooling is not coupled to the ERP core layers:
 *   AI-001 — ERP core .csproj must not reference AI/ML NuGet packages
 *   AI-002 — ERP.Domain and ERP.Application must not contain LLM call patterns
 *   AI-003 — ERP.Domain and ERP.Application must not use AI package namespaces
 *   AI-004 — ERP.AI.* projects should not inject ErpDbContext directly (warning)
 *   AI-005 — Analytics/reporting queries must not appear in ERP.API Controllers
 */

import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT, walkFiles, toRepoRel } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation, addWarning } from './shared/report-utils.mjs';

export const CHECK_NAME = 'ai-layer-boundaries';

// ── AI/ML NuGet packages forbidden in ERP core layers ──────────────────────
const FORBIDDEN_AI_PACKAGES = [
  'OpenAI',
  'Azure.AI',
  'Anthropic',
  'Microsoft.SemanticKernel',
  'LangChain',
  'Pinecone',
  'Qdrant',
  'Weaviate',
  'Milvus',
  'Microsoft.ML',
  'TorchSharp',
  'Betalgo.OpenAI',
  'OllamaSharp',
];

// ── Direct LLM/AI call patterns ────────────────────────────────────────────
const LLM_CALL_PATTERNS = [
  /openAIClient\./i,
  /anthropicClient\./i,
  /semanticKernel\./i,
  /kernelBuilder\./i,
  /\.CreateChatCompletion/i,
  /\.GetEmbeddings/i,
  /client\.Completions\./i,
  /IChatCompletionService/i,
  /ITextEmbeddingGeneration/i,
];

// ── Analytics queries that should NOT appear in controllers ────────────────
const ANALYTICS_QUERY_PATTERNS = [
  /\.GroupBy\(.*\)\.Select\(.*\)\.Sum\(/,          // aggregation pipelines
  /AsNoTracking\(\).*\.Join\(.*\).*\.Join\(/,       // multi-join read queries
];

export function runCheckAiLayerBoundaries() {
  const result = createCheckResult(CHECK_NAME);
  const backendSrc = path.join(REPO_ROOT, 'backend', 'src');

  if (!fs.existsSync(backendSrc)) return result;

  // ── AI-001: Check .csproj files for forbidden AI packages in core layers ──
  const csprojFiles = walkFiles(backendSrc, { extensions: ['.csproj'] });

  for (const file of csprojFiles) {
    const rel = toRepoRel(file);
    const projectName = path.basename(file, '.csproj');

    const isCoreLayer = (
      projectName === 'ERP.Domain' ||
      projectName === 'ERP.Application' ||
      projectName === 'ERP.Infrastructure' ||
      projectName === 'ERP.API'
    );
    if (!isCoreLayer) continue;

    const content = fs.readFileSync(file, 'utf8');

    for (const pkg of FORBIDDEN_AI_PACKAGES) {
      if (content.includes(`"${pkg}`) || content.includes(`Include="${pkg}`)) {
        addViolation(result, {
          rule: 'AI-001',
          file: rel,
          message: `${projectName} references AI/ML package "${pkg}" — AI dependencies must live in ERP.AI.* projects only. See docs/architecture/ai-foundation.md`,
        });
      }
    }
  }

  // ── AI-002 / AI-003: Check .cs files in Domain and Application ────────────
  const coreLayers = [
    path.join(backendSrc, 'ERP.Domain'),
    path.join(backendSrc, 'ERP.Application'),
  ];

  for (const layerDir of coreLayers) {
    if (!fs.existsSync(layerDir)) continue;

    const csFiles = walkFiles(layerDir, { extensions: ['.cs'] });

    for (const file of csFiles) {
      const rel = toRepoRel(file);
      const content = fs.readFileSync(file, 'utf8');

      // AI-002: LLM call patterns
      for (const pattern of LLM_CALL_PATTERNS) {
        if (pattern.test(content)) {
          addViolation(result, {
            rule: 'AI-002',
            file: rel,
            message: `Direct LLM/AI call detected in core layer — move AI logic to ERP.AI.Application or ERP.AI.Infrastructure. See docs/architecture/ai-foundation.md`,
          });
          break;
        }
      }

      // AI-003: AI package namespace imports
      for (const pkg of FORBIDDEN_AI_PACKAGES) {
        if (content.includes(`using ${pkg}`) || content.includes(`using ${pkg}.`)) {
          addViolation(result, {
            rule: 'AI-003',
            file: rel,
            message: `"using ${pkg}" in core layer — AI package namespaces must stay in ERP.AI.* projects`,
          });
          break;
        }
      }
    }
  }

  // ── AI-004: ERP.AI.* must not inject ErpDbContext directly ────────────────
  const aiProjectDirs = fs.existsSync(backendSrc)
    ? fs.readdirSync(backendSrc, { withFileTypes: true })
        .filter(e => e.isDirectory() && e.name.startsWith('ERP.AI.'))
        .map(e => path.join(backendSrc, e.name))
    : [];

  for (const aiDir of aiProjectDirs) {
    const csFiles = walkFiles(aiDir, { extensions: ['.cs'] });

    for (const file of csFiles) {
      const rel = toRepoRel(file);
      const content = fs.readFileSync(file, 'utf8');

      if (content.includes('ErpDbContext') && !content.includes('// allowed')) {
        addWarning(result, {
          rule: 'AI-004',
          file: rel,
          message: `AI layer references ErpDbContext directly — prefer read models, Outbox consumption, or projection queries instead. See docs/architecture/ai-foundation.md (Analytics Foundation)`,
        });
      }
    }
  }

  // ── AI-005: Complex analytics queries in Controllers (warning) ────────────
  const apiControllersDir = path.join(backendSrc, 'ERP.API', 'Controllers');
  if (fs.existsSync(apiControllersDir)) {
    const controllerFiles = walkFiles(apiControllersDir, { extensions: ['.cs'] });

    for (const file of controllerFiles) {
      const rel = toRepoRel(file);
      const content = fs.readFileSync(file, 'utf8');

      for (const pattern of ANALYTICS_QUERY_PATTERNS) {
        if (pattern.test(content)) {
          addWarning(result, {
            rule: 'AI-005',
            file: rel,
            message: `Complex aggregation/analytics query in Controller — move to a CQRS query handler or read model. See docs/architecture/ai-foundation.md (Analytics Foundation)`,
          });
          break;
        }
      }
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-ai-layer-boundaries.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckAiLayerBoundaries()) ? 0 : 1);
}
