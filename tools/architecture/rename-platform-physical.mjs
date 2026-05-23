#!/usr/bin/env node
/**
 * Physical rename: SuperAdmin* files/folders → Platform* (control plane UI).
 * Keeps URL /superadmin/* and JWT role 'SuperAdmin' unchanged.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(__dirname, '../..');
const FRONTEND_SRC = path.join(REPO, 'frontend/src');

function walkFiles(dir, acc = []) {
  if (!fs.existsSync(dir)) return acc;
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    const st = fs.statSync(full);
    if (st.isDirectory()) walkFiles(full, acc);
    else acc.push(full);
  }
  return acc;
}

function renamePath(oldPath, newPath) {
  if (!fs.existsSync(oldPath)) return false;
  if (oldPath === newPath) return false;
  fs.mkdirSync(path.dirname(newPath), { recursive: true });
  fs.renameSync(oldPath, newPath);
  console.log(`  ${path.relative(REPO, oldPath)} → ${path.relative(REPO, newPath)}`);
  return true;
}

function renameBasename(filePath) {
  const dir = path.dirname(filePath);
  const base = path.basename(filePath);
  let next = base;
  if (base.startsWith('SuperAdmin')) {
    next = 'Platform' + base.slice('SuperAdmin'.length);
  } else if (base.startsWith('superAdmin')) {
    next = 'platform' + base.slice('superAdmin'.length);
  } else if (base === 'superAdminShellRoutes.tsx') {
    return null; // delete stale
  }
  if (next === base) return filePath;
  return path.join(dir, next);
}

/** Deepest paths first so parent renames do not break children. */
function renameDirectory(oldDir, newDir) {
  if (!fs.existsSync(oldDir)) return;
  const parent = path.dirname(newDir);
  fs.mkdirSync(parent, { recursive: true });
  fs.renameSync(oldDir, newDir);
  console.log(`DIR ${path.relative(REPO, oldDir)} → ${path.relative(REPO, newDir)}`);
}

function applyTextReplacements(rootDirs) {
  const replacements = [
    ['modules/platform', 'modules/platform'],
    ['components/platform', 'components/platform'],
    ['pages/Platform', 'pages/Platform'],
    ['layouts/PlatformLayout.css', 'layouts/PlatformLayout.css'],
    ['layouts/PlatformLayout', 'layouts/PlatformLayout'],
    ['templates/PlatformCrudTemplate', 'templates/PlatformCrudTemplate'],
    ['templates/PlatformCrudTemplate.tsx', 'templates/PlatformCrudTemplate.tsx'],
    ['hooks/usePlatformGate', 'hooks/usePlatformGate'],
    ['components/PlatformImpersonationBanner', 'components/PlatformImpersonationBanner'],
    ['PlatformCrudTemplateProps', 'PlatformCrudTemplateProps'],
    ['PlatformPageTemplateProps', 'PlatformPageTemplateProps'],
    ['PlatformCrudTemplate', 'PlatformCrudTemplate'],
    ['PlatformPageTemplate', 'PlatformPageTemplate'],
    ['PlatformImpersonationBanner', 'PlatformImpersonationBanner'],
    ['usePlatformGate', 'usePlatformGate'],
    ['platformPanelUtils', 'platformPanelUtils'],
    ['PlatformHomeTab', 'PlatformHomeTab'],
    ['usePlatformPanelPage', 'usePlatformPanelPage'],
    ['PlatformPanelPageState', 'PlatformPanelPageState'],
    ['PlatformPanelPageProps', 'PlatformPanelPageProps'],
    ['PlatformPanelOverviewTab', 'PlatformPanelOverviewTab'],
    ['PlatformPanelCompaniesTab', 'PlatformPanelCompaniesTab'],
    ['PlatformPanelCreateSubscriberModal', 'PlatformPanelCreateSubscriberModal'],
    ['PlatformPanelSubscriptionModal', 'PlatformPanelSubscriptionModal'],
    ['PlatformMenuBuilderCrmWorkspaceHandlersParams', 'PlatformMenuBuilderCrmWorkspaceHandlersParams'],
    ['PlatformMenuBuilderLegacyPanelProps', 'PlatformMenuBuilderLegacyPanelProps'],
    ['PlatformMenuBuilderCrmWorkspaceProps', 'PlatformMenuBuilderCrmWorkspaceProps'],
    ['PlatformMenuBuilderSectionProps', 'PlatformMenuBuilderSectionProps'],
    ['UsePlatformMenuBuilderReturn', 'UsePlatformMenuBuilderReturn'],
    ['usePlatformMenuBuilderCrmWorkspaceHandlers', 'usePlatformMenuBuilderCrmWorkspaceHandlers'],
    ['usePlatformMenuBuilderSharedEffects', 'usePlatformMenuBuilderSharedEffects'],
    ['usePlatformMenuBuilderCrmEffects', 'usePlatformMenuBuilderCrmEffects'],
    ['usePlatformMenuBuilderActions', 'usePlatformMenuBuilderActions'],
    ['usePlatformMenuBuilder', 'usePlatformMenuBuilder'],
    ['usePlatformPlansSection', 'usePlatformPlansSection'],
    ['platformMenuBuilderPersist', 'platformMenuBuilderPersist'],
    ['platformMenuBuilderUtils', 'platformMenuBuilderUtils'],
    ['platformPlansSectionUtils', 'platformPlansSectionUtils'],
    ['PlatformMenuBuilderCrmComposerPanels', 'PlatformMenuBuilderCrmComposerPanels'],
    ['PlatformMenuBuilderCrmPreviewSection', 'PlatformMenuBuilderCrmPreviewSection'],
    ['PlatformMenuBuilderCrmAuditSection', 'PlatformMenuBuilderCrmAuditSection'],
    ['PlatformMenuBuilderCrmWorkspace', 'PlatformMenuBuilderCrmWorkspace'],
    ['PlatformMenuBuilderCrmModals', 'PlatformMenuBuilderCrmModals'],
    ['PlatformMenuBuilderLegacyPanel', 'PlatformMenuBuilderLegacyPanel'],
    ['PlatformMenuBuilderSection', 'PlatformMenuBuilderSection'],
    ['PlatformGrowthSection', 'PlatformGrowthSection'],
    ['PlatformCompaniesShellPage', 'PlatformCompaniesShellPage'],
    ['PlatformMenuPlansHubPage', 'PlatformMenuPlansHubPage'],
    ['PlatformPlaceholderPage', 'PlatformPlaceholderPage'],
    ['PlatformObservabilityPage', 'PlatformObservabilityPage'],
    ['PlatformOverviewPage', 'PlatformOverviewPage'],
    ['PlatformSubscriberDetailPage', 'PlatformSubscriberDetailPage'],
    ['PlatformSubscribersPage', 'PlatformSubscribersPage'],
    ['PlatformBillingPage', 'PlatformBillingPage'],
    ['PlatformAuditPage', 'PlatformAuditPage'],
    ['PlatformUsersPage', 'PlatformUsersPage'],
    ['PlatformPanelPage', 'PlatformPanelPage'],
    ['PlatformLayout', 'PlatformLayout'],
    ['PlatformNavMenuPage.css', 'PlatformNavMenuPage.css'],
    ['PlatformPanelPage.css', 'PlatformPanelPage.css'],
    ['PlatformOverviewPage.css', 'PlatformOverviewPage.css'],
    ['PlatformSubscribersPage.css', 'PlatformSubscribersPage.css'],
    ['PlatformSubscriberDetailPage.css', 'PlatformSubscriberDetailPage.css'],
    ['PlatformMenuBuilderSection.css', 'PlatformMenuBuilderSection.css'],
    ['PlatformGrowthSection.css', 'PlatformGrowthSection.css'],
    ['PlatformPlansSection.css', 'PlatformPlansSection.css'],
    ['modules/platform', 'modules/platform'], // noop anchor
  ];

  const exts = new Set(['.ts', '.tsx', '.css', '.md', '.json', '.mjs']);
  for (const root of rootDirs) {
    if (!fs.existsSync(root)) continue;
    for (const file of walkFiles(root)) {
      if (!exts.has(path.extname(file))) continue;
      let text = fs.readFileSync(file, 'utf8');
      let changed = false;
      for (const [from, to] of replacements) {
        if (from === to) continue;
        if (text.includes(from)) {
          text = text.split(from).join(to);
          changed = true;
        }
      }
      if (changed) {
        fs.writeFileSync(file, text, 'utf8');
        console.log(`  text ${path.relative(REPO, file)}`);
      }
    }
  }
}

function main() {
  console.log('=== Platform physical rename ===\n');

  // Remove stale route file if present
  const staleShell = path.join(FRONTEND_SRC, 'routes/superAdminShellRoutes.tsx');
  if (fs.existsSync(staleShell)) {
    fs.unlinkSync(staleShell);
    console.log(`DEL ${path.relative(REPO, staleShell)}`);
  }

  const dirRenames = [
    [path.join(FRONTEND_SRC, 'modules/platform'), path.join(FRONTEND_SRC, 'modules/platform')],
    [path.join(FRONTEND_SRC, 'components/platform'), path.join(FRONTEND_SRC, 'components/platform')],
    [path.join(FRONTEND_SRC, 'pages/Platform'), path.join(FRONTEND_SRC, 'pages/Platform')],
  ];

  for (const [from, to] of dirRenames) renameDirectory(from, to);

  const rootRenames = [
    ['layouts/PlatformLayout.tsx', 'layouts/PlatformLayout.tsx'],
    ['layouts/PlatformLayout.css', 'layouts/PlatformLayout.css'],
    ['templates/PlatformCrudTemplate.tsx', 'templates/PlatformCrudTemplate.tsx'],
    ['hooks/usePlatformGate.ts', 'hooks/usePlatformGate.ts'],
    ['components/PlatformImpersonationBanner.tsx', 'components/PlatformImpersonationBanner.tsx'],
    ['pages/PlatformPanelPage.tsx', 'pages/PlatformPanelPage.tsx'],
    ['pages/PlatformNavMenuPage.css', 'pages/PlatformNavMenuPage.css'],
  ];

  for (const [from, to] of rootRenames) {
    renamePath(path.join(FRONTEND_SRC, from), path.join(FRONTEND_SRC, to));
  }

  // Rename files inside frontend/src (deepest first)
  let files = walkFiles(FRONTEND_SRC);
  files.sort((a, b) => b.split(path.sep).length - a.split(path.sep).length);

  for (const file of files) {
    const next = renameBasename(file);
    if (next === null) {
      fs.unlinkSync(file);
      console.log(`DEL ${path.relative(REPO, file)}`);
      continue;
    }
    if (next !== file) renamePath(file, next);
  }

  const textRoots = [
    FRONTEND_SRC,
    path.join(REPO, 'docs/platform'),
    path.join(REPO, 'tools/architecture'),
    path.join(REPO, 'docs/frontend-layout-conventions.md'),
    path.join(REPO, 'docs/FRONTEND_ARCHITECTURE_BASELINE.md'),
  ].filter((p) => fs.existsSync(p));

  console.log('\n=== Text replacements ===');
  applyTextReplacements(
    textRoots.filter((p) => fs.existsSync(p) && fs.statSync(p).isDirectory()),
  );

  // architecture config
  const cssPrefixes = path.join(REPO, 'tools/architecture/config/css-prefixes.json');
  if (fs.existsSync(cssPrefixes)) {
    let j = fs.readFileSync(cssPrefixes, 'utf8');
    j = j.replace('**/superadmin/**', '**/platform/**');
    fs.writeFileSync(cssPrefixes, j);
  }

  const archRules = path.join(REPO, 'tools/architecture/config/architecture-rules.json');
  if (fs.existsSync(archRules)) {
    let j = fs.readFileSync(archRules, 'utf8');
    j = j.replaceAll('"superadmin"', '"platform"');
    fs.writeFileSync(archRules, j);
  }

  console.log('\nDone.');
}

main();
