import type { Role } from "@/types/api";

export function homePathForRole(role: Role): "/dashboard" | "/my-device" {
  return role === "Employee" ? "/my-device" : "/dashboard";
}
