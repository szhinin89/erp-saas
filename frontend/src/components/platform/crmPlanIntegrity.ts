export type CrmPlanLayout = 'horizontal' | 'vertical';

export type CrmPlanModel = {
  id: string;
  code: string;
  name: string;
  priceMonthly: number;
  priceYearly: number;
  description: string;
  layout: CrmPlanLayout;
  highlight: boolean;
};

export type NewPlanDraft = {
  name: string;
  monthly: string;
  yearly: string;
  description: string;
};

export type CrmWorkspaceExportPayload<TNode = unknown> = {
  version: number;
  exportedAt: string;
  plans: CrmPlanModel[];
  planActiveById: Record<string, string[]>;
  tree: TNode[];
  auditLines: string[];
};

function normalizeCode(input: string): string {
  const base = input
    .trim()
    .toUpperCase()
    .replace(/[^A-Z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '');
  return base || 'PLAN';
}

function ensureUnique(value: string, used: Set<string>): string {
  if (!used.has(value)) {
    used.add(value);
    return value;
  }
  let i = 2;
  while (used.has(`${value}_${i}`)) i += 1;
  const unique = `${value}_${i}`;
  used.add(unique);
  return unique;
}

function parseMoney(input: unknown): number | null {
  if (typeof input === 'number' && Number.isFinite(input)) return input;
  if (typeof input !== 'string') return null;
  const parsed = Number.parseFloat(input.trim());
  return Number.isFinite(parsed) ? parsed : null;
}

export function normalizeCrmPlans(rawPlans: unknown, fallbackPlans: CrmPlanModel[]): CrmPlanModel[] {
  if (!Array.isArray(rawPlans) || rawPlans.length === 0) return fallbackPlans;
  const usedIds = new Set<string>();
  const usedCodes = new Set<string>();
  const normalized: CrmPlanModel[] = [];

  for (const row of rawPlans) {
    if (!row || typeof row !== 'object') continue;
    const data = row as Partial<CrmPlanModel>;
    const nameBase = typeof data.name === 'string' ? data.name.trim() : '';
    const idBase = typeof data.id === 'string' ? data.id.trim() : '';
    const codeBase = typeof data.code === 'string' ? data.code.trim() : nameBase;
    const monthly = parseMoney(data.priceMonthly);
    const yearly = parseMoney(data.priceYearly);

    if (!idBase && !nameBase && !codeBase) continue;

    const code = ensureUnique(normalizeCode(codeBase), usedCodes);
    const id = ensureUnique(idBase || `plan_${code.toLowerCase()}`, usedIds);
    const monthlySafe = monthly !== null && monthly >= 0 ? monthly : 0;
    const yearlySafe =
      yearly !== null && yearly >= 0 ? yearly : Math.round(monthlySafe * 12 * 0.8);
    const layout: CrmPlanLayout = data.layout === 'vertical' ? 'vertical' : 'horizontal';

    normalized.push({
      id,
      code,
      name: (nameBase || code).toUpperCase(),
      priceMonthly: monthlySafe,
      priceYearly: yearlySafe,
      description:
        typeof data.description === 'string' && data.description.trim()
          ? data.description.trim()
          : 'Plan personalizado',
      layout,
      highlight: Boolean(data.highlight),
    });
  }

  return normalized.length > 0 ? normalized : fallbackPlans;
}

export function normalizePlanActiveById(
  rawMap: unknown,
  validPlanIds: Set<string>,
  validNodeIds: Set<string>,
): Record<string, string[]> {
  if (!rawMap || typeof rawMap !== 'object') return {};
  const input = rawMap as Record<string, unknown>;
  const out: Record<string, string[]> = {};

  for (const [planId, nodeIds] of Object.entries(input)) {
    if (!validPlanIds.has(planId)) continue;
    if (!Array.isArray(nodeIds)) continue;
    const seen = new Set<string>();
    const clean: string[] = [];
    for (const nodeId of nodeIds) {
      if (typeof nodeId !== 'string') continue;
      if (!validNodeIds.has(nodeId) || seen.has(nodeId)) continue;
      seen.add(nodeId);
      clean.push(nodeId);
    }
    out[planId] = clean;
  }

  return out;
}

export function collectTreeNodeIds(tree: unknown): Set<string> {
  const ids = new Set<string>();
  const walk = (nodes: unknown) => {
    if (!Array.isArray(nodes)) return;
    for (const row of nodes) {
      if (!row || typeof row !== 'object') continue;
      const node = row as { uid?: unknown; children?: unknown };
      if (typeof node.uid === 'string' && node.uid.trim()) ids.add(node.uid);
      walk(node.children);
    }
  };
  walk(tree);
  return ids;
}

export function normalizeAuditLines(rawAuditLines: unknown, maxLines: number = 100): string[] {
  if (!Array.isArray(rawAuditLines)) return [];
  return rawAuditLines
    .filter((line): line is string => typeof line === 'string')
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .slice(0, Math.max(0, maxLines));
}

export function parseImportedCrmWorkspace(
  rawInput: unknown,
  fallbackPlans: CrmPlanModel[],
): CrmWorkspaceExportPayload {
  const input = rawInput && typeof rawInput === 'object' ? (rawInput as Record<string, unknown>) : {};
  const tree = Array.isArray(input.tree) ? input.tree : [];
  const plans = normalizeCrmPlans(input.plans, fallbackPlans);
  const validPlanIds = new Set(plans.map((p) => p.id));
  const validNodeIds = collectTreeNodeIds(tree);
  const planActiveById = normalizePlanActiveById(input.planActiveById, validPlanIds, validNodeIds);
  const auditLines = normalizeAuditLines(input.auditLines, 100);

  return {
    version: typeof input.version === 'number' ? input.version : 1,
    exportedAt: typeof input.exportedAt === 'string' ? input.exportedAt : new Date().toISOString(),
    plans,
    planActiveById,
    tree,
    auditLines,
  };
}

export function resolveInheritedActiveNodeIds(
  planActiveById: Record<string, string[]>,
  sourcePlanId: string,
  validNodeIds: Set<string>,
): string[] {
  if (!sourcePlanId) return [];
  const source = planActiveById[sourcePlanId] ?? [];
  const seen = new Set<string>();
  const out: string[] = [];
  for (const nodeId of source) {
    if (!validNodeIds.has(nodeId) || seen.has(nodeId)) continue;
    seen.add(nodeId);
    out.push(nodeId);
  }
  return out;
}

export function buildPlanFromDraft(
  draft: NewPlanDraft,
  existingPlans: CrmPlanModel[],
  now: number = Date.now(),
): { plan: CrmPlanModel } | { error: string } {
  const name = draft.name.trim();
  if (!name) return { error: 'Completa nombre y precio mensual válidos' };

  const monthly = parseMoney(draft.monthly);
  if (monthly === null || monthly <= 0) return { error: 'Completa nombre y precio mensual válidos' };

  const yearlyRaw = parseMoney(draft.yearly);
  const yearly = yearlyRaw !== null && yearlyRaw > 0 ? yearlyRaw : Math.round(monthly * 12 * 0.8);

  const usedIds = new Set(existingPlans.map((p) => p.id));
  const usedCodes = new Set(existingPlans.map((p) => p.code));
  const code = ensureUnique(normalizeCode(name), usedCodes);
  const id = ensureUnique(`plan_${now}`, usedIds);

  return {
    plan: {
      id,
      code,
      name: name.toUpperCase(),
      priceMonthly: monthly,
      priceYearly: yearly,
      description: draft.description.trim() || 'Plan personalizado',
      layout: 'horizontal',
      highlight: false,
    },
  };
}
