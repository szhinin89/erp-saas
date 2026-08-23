export type HelpKey = string;

export interface HelpContent {
  title: string;
  short: string;
  long?: string;
}

export type HelpVariables = Record<string, string | number>;

export type HelpMode = "compact" | "guided" | "expert";
