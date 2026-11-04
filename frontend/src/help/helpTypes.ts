export type HelpKey = string;

export interface HelpContent {
  /** Optional dictionary prefix for localized field help. */
  i18nPrefix?: string;
  title: string;
  short: string;
  long?: string;
}

export type HelpVariables = Record<string, string | number>;

export type HelpMode = "compact" | "guided" | "expert";
