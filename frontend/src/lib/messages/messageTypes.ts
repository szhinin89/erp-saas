import type { ReactNode } from "react";

export type MessageType = "success" | "error" | "warning" | "info";

export interface MessageItem {
  id: string;
  type: MessageType;
  message: string;
  persistent: boolean;
  createdAt: number;
}

export interface ConfirmOptions {
  title: string;
  message: string | ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: "danger" | "warning" | "default";
}

export interface PromptOptions extends ConfirmOptions {
  label: string;
  placeholder?: string;
  inputType?: "text" | "number" | "date" | "datetime-local";
  defaultValue?: string;
  required?: boolean;
}
