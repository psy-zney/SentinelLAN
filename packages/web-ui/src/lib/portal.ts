import type { Role } from "@/types/api";

export const portal = process.env.NEXT_PUBLIC_PORTAL === "employee" ? "employee" : "company";
export const portalTitle = portal === "employee" ? "Nhân viên" : "Quản trị công ty";
export function portalAllows(role: Role) {
  return portal === "employee" ? role === "Employee" : role === "Admin";
}
export function portalHomeUrl(role: Role) {
  return `${portalBaseUrl(role)}${role === "Employee" ? "/my-device" : "/dashboard"}`;
}
export function portalBaseUrl(role: Role) {
  return role === "Employee" ? process.env.NEXT_PUBLIC_EMPLOYEE_URL ?? "/employee" : process.env.NEXT_PUBLIC_COMPANY_URL ?? "/company";
}
export function crossPortalQrRoute(nextRoute: string): string | null {
  if (/^\/devices\/[0-9a-f-]{36}$/i.test(nextRoute) && portal !== "company") return `${portalBaseUrl("Admin")}${nextRoute}`;
  if (nextRoute === "/my-device" && portal !== "employee") return `${portalBaseUrl("Employee")}${nextRoute}`;
  return null;
}
