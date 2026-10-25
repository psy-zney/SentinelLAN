import type { Language } from "./i18n";
import type { Role } from "@/types/api";

export function userRoleLabel(role: Role, language: Language): string {
  if (role === "Admin") return language === "vi" ? "Quản trị viên" : "Administrator";
  if (role === "Employee") return language === "vi" ? "Nhân viên" : "Employee";
  return "Agent";
}

export function userDisplayName(name: string, role: Role, language: Language): string {
  if (role === "Admin" && name.trim() === "Organization Administrator")
    return language === "vi" ? "Quản trị hệ thống" : "System administrator";
  return name;
}
