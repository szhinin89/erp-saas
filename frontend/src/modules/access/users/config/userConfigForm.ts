import { MEMBERSHIP_ROLES, type MembershipRole } from "../api/membershipService";
import { type UserConfigFormValues } from "../../../../schemas/access/userConfigSchema";
import { userConfigTabs, type UserConfigTabId } from "./userConfigTabs";

/** Valores y helpers puros del formulario único de UserConfigPage. */

const VALID_TAB_IDS = userConfigTabs.map((tab) => tab.id);
const DEFAULT_TAB: UserConfigTabId = "general";

export const EMPTY_FORM_VALUES: UserConfigFormValues = {
  stage: "pending",
  username: "",
  firstName: "",
  lastName: "",
  email: "",
  password: "",
  role: "User",
  profileId: null,
  authorizedBranchIds: [],
  loginMode: "AskBranch",
  defaultBranchId: null,
};

export function parseTab(raw: string | null): UserConfigTabId {
  if (raw && (VALID_TAB_IDS as string[]).includes(raw))
    return raw as UserConfigTabId;
  return DEFAULT_TAB;
}

/** Backend persiste Role como string libre (SecurityRoles) — defensivo ante valores inesperados. */
export function asMembershipRole(role: string): MembershipRole {
  return (MEMBERSHIP_ROLES as readonly string[]).includes(role)
    ? (role as MembershipRole)
    : "User";
}

export type BlockErrors = { general: string; branches: string; preferences: string };
export const NO_BLOCK_ERRORS: BlockErrors = {
  general: "",
  branches: "",
  preferences: "",
};
