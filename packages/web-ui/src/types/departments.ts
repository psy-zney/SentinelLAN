export type Department = { id: string; name: string; policyId: string | null; revision: string; userIds: string[]; deviceIds: string[] };
export type DepartmentMember = { id: string; name: string; departmentId: string | null };
export type DepartmentDirectory = { departments: Department[]; users: DepartmentMember[]; devices: DepartmentMember[] };
