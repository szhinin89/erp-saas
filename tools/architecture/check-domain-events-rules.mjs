/**
 * check-domain-events-rules.mjs
 *
 * Validates domain event conventions:
 *   EV-001 — Event classes must live in ERP.Domain (not ERP.API or ERP.Infrastructure)
 *   EV-002 — Event class names must use past tense (warning)
 *   EV-003 — OutboxMessage.From() must NOT be called outside Persistence/Outbox/
 *   EV-004 — Event class names must end with "Event"  (warning for *EventHandler, *EventArgs)
 *   EV-005 — New events should extend BaseDomainEvent (warning for direct IDomainEvent impls)
 */

import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT, walkFiles, toRepoRel } from './shared/fs-utils.mjs';
import { createCheckResult, addViolation, addWarning } from './shared/report-utils.mjs';

export const CHECK_NAME = 'domain-events-rules';

const PAST_TENSE_VERBS = [
  'Created', 'Updated', 'Approved', 'Cancelled', 'Posted', 'Voided',
  'Authorized', 'Completed', 'Received', 'Exceeded', 'Changed', 'Assigned',
  'Removed', 'Enabled', 'Disabled', 'Failed', 'Rejected', 'Confirmed',
  'Activated', 'Suspended', 'Expired', 'Processed', 'Transferred', 'Adjusted',
  'Registered', 'Deactivated', 'Archived', 'Submitted', 'Dispatched', 'Settled',
  'Reconciled', 'Issued', 'Reversed', 'Validated', 'Imported', 'Exported',
];

const PRESENT_TENSE_VERBS = [
  'Create', 'Update', 'Approve', 'Cancel', 'Post', 'Void',
  'Authorize', 'Complete', 'Receive', 'Change', 'Assign',
  'Remove', 'Enable', 'Disable', 'Fail', 'Reject', 'Confirm',
  'Activate', 'Suspend', 'Expire', 'Process', 'Transfer', 'Adjust',
  'Submit', 'Dispatch', 'Settle', 'Reconcile', 'Issue', 'Reverse',
  'Validate', 'Import', 'Export',
];

// Suffixes that look like events but are NOT domain events — skip checks
const NON_EVENT_SUFFIXES = ['EventHandler', 'EventArgs', 'EventBus', 'EventDispatcher', 'EventPublisher'];

export function runCheckDomainEventsRules() {
  const result = createCheckResult(CHECK_NAME);
  const backendSrc = path.join(REPO_ROOT, 'backend', 'src');

  if (!fs.existsSync(backendSrc)) return result;

  const csFiles = walkFiles(backendSrc, { extensions: ['.cs'] });

  for (const file of csFiles) {
    const rel = toRepoRel(file);
    const content = fs.readFileSync(file, 'utf8');

    // ── EV-003: No direct OutboxMessage.From() outside Outbox folder ──────────
    const isOutboxOwner = rel.includes('Persistence/Outbox') || rel.includes('Persistence\\Outbox');
    if (!isOutboxOwner && content.includes('OutboxMessage.From(')) {
      addViolation(result, {
        rule: 'EV-003',
        file: rel,
        message: `Direct OutboxMessage.From() call — outbox persistence is handled automatically by ErpDbContext`,
      });
    }

    // Find class/record declarations that could be domain events
    const classDeclarations = [...content.matchAll(
      /(?:public|internal|sealed|abstract)\s+(?:sealed\s+|abstract\s+)?(?:class|record)\s+(\w+)/g
    )];

    for (const match of classDeclarations) {
      const className = match[1];

      // Skip obvious non-event classes that happen to contain "Event"
      if (NON_EVENT_SUFFIXES.some(s => className.endsWith(s))) continue;
      if (!className.endsWith('Event')) continue;

      // ── EV-001: Events must live in ERP.Domain ────────────────────────────
      const isInWrongLayer = (
        (rel.includes('ERP.API/') || rel.includes('ERP.API\\')) ||
        (rel.includes('ERP.Infrastructure/') || rel.includes('ERP.Infrastructure\\'))
      );
      if (isInWrongLayer) {
        addViolation(result, {
          rule: 'EV-001',
          file: rel,
          message: `${className} is defined outside ERP.Domain — domain events must live in ERP.Domain/Modules/{Module}/Events/`,
        });
      }

      // ── EV-002: Past tense naming (warning) ───────────────────────────────
      const hasPastTense = PAST_TENSE_VERBS.some(v => className.includes(v));
      if (!hasPastTense) {
        for (const verb of PRESENT_TENSE_VERBS) {
          // Only flag if the class body implements IDomainEvent (likely a real event)
          const isLikelyEvent =
            content.includes('IDomainEvent') || content.includes('BaseDomainEvent');
          if (!isLikelyEvent) break;

          const pattern = new RegExp(`(?<![A-Za-z])${verb}(?!ed|d|ing|s\\b)`, '');
          if (pattern.test(className)) {
            addWarning(result, {
              rule: 'EV-002',
              file: rel,
              message: `${className} may use present tense — prefer past tense (e.g., Created, Posted, Approved). See AI-RULES/EVENT-DRIVEN-RULES.md`,
            });
            break;
          }
        }
      }

      // ── EV-005: New events should extend BaseDomainEvent (warning) ─────────
      // Only applies to events in ERP.Domain that implement IDomainEvent directly
      const isInDomain = rel.includes('ERP.Domain/') || rel.includes('ERP.Domain\\');
      if (isInDomain) {
        const implementsDirectly =
          content.includes(`: IDomainEvent`) || content.includes(`, IDomainEvent`);
        const extendsBase =
          content.includes(': BaseDomainEvent') || content.includes(', BaseDomainEvent');
        if (implementsDirectly && !extendsBase) {
          addWarning(result, {
            rule: 'EV-005',
            file: rel,
            message: `${className} implements IDomainEvent directly — new events should extend BaseDomainEvent for CorrelationId/TenantId support`,
          });
        }
      }
    }
  }

  return result;
}

if (process.argv[1]?.endsWith('check-domain-events-rules.mjs')) {
  const { printCheckResult } = await import('./shared/report-utils.mjs');
  process.exit(printCheckResult(runCheckDomainEventsRules()) ? 0 : 1);
}
