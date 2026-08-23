import { HELP_REGISTRY } from "./helpRegistry";
import { interpolateHelp } from "./helpVariables";
import type { HelpKeyId } from "./helpKeys";
import type { HelpContent, HelpVariables } from "./helpTypes";

/** Fachada pública y pura del módulo de ayuda: resuelve una helpKey a contenido ya interpolado. */
export function resolveHelp(
  key: HelpKeyId,
  vars?: HelpVariables,
): HelpContent | null {
  const entry = HELP_REGISTRY[key];
  if (!entry) return null;
  return {
    title: entry.title,
    short: interpolateHelp(entry.short, vars),
    long: entry.long ? interpolateHelp(entry.long, vars) : undefined,
  };
}
