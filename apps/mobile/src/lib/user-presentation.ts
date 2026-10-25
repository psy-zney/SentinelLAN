import type { Language } from './i18n';

export function userRoleLabel(role: string, language: Language): string {
  if (role === 'Admin') return language === 'vi' ? 'Quản trị viên' : 'Administrator';
  if (role === 'Employee') return language === 'vi' ? 'Nhân viên' : 'Employee';
  return role;
}

export function userDisplayName(name: string, role: string, language: Language): string {
  if (role === 'Admin' && name.trim() === 'Organization Administrator')
    return language === 'vi' ? 'Quản trị hệ thống' : 'System administrator';
  return name;
}
