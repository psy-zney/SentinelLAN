import React, { useState } from 'react';
import Constants from 'expo-constants';
import { Alert } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { AppButton, AppCard, AppInput } from '../../components/common';
import { useAuth } from '../auth/auth-context';
import { ServicePage, ServicePages, ServiceText, ServiceError, useActiveScreen } from '../self-service/shared';
import { useI18n } from '../../lib/i18n';
import { userDisplayName, userRoleLabel } from '../../lib/user-presentation';

export function CompanyUsersView() {
  const {lang}=useI18n();
  const {user,apiClient}=useAuth();const active=useActiveScreen();
  const [offset,setOffset]=useState(0);
  const users=useQuery({queryKey:['company-users',offset],queryFn:()=>apiClient.operatorUsers(offset),enabled:active && user?.role==='Admin'});
  const [email,setEmail]=useState('');const [name,setName]=useState('');const [role,setRole]=useState<'Employee'|'Admin'>('Employee');const [reason,setReason]=useState('');const [invitation,setInvitation]=useState('');const [error,setError]=useState<unknown>();const [busy,setBusy]=useState(false);
  async function perform(action:()=>Promise<unknown>) {if(busy)return;setBusy(true);setError(undefined);try{await action();await users.refetch();}catch(cause){setError(cause);}finally{setBusy(false);}}
  if(user?.role!=='Admin')return <ServicePage><ServiceText>Chỉ quản trị viên được quản lý tài khoản nội bộ.</ServiceText></ServicePage>;
  return <ServicePage><ServiceText heading>{lang==='vi'?'Tài khoản nội bộ':'Internal accounts'}</ServiceText><ServiceError error={error || users.error} retry={()=>{void users.refetch();}}/>
    <AppInput label="Lý do cấp hoặc đổi quyền truy cập" value={reason} onChangeText={setReason}/>
    <AppCard><ServiceText heading>Cấp tài khoản mới</ServiceText><AppInput label="Họ tên" value={name} onChangeText={setName}/><AppInput label="Email" value={email} onChangeText={setEmail} keyboardType="email-address" autoCapitalize="none"/>
      {(['Employee','Admin'] as const).map(value=><AppButton key={value} title={`${role===value?'✓ ':''}${userRoleLabel(value,lang)}`} variant="outline" onPress={()=>setRole(value)}/>)}
      <AppButton title="Tạo link kích hoạt" disabled={busy || reason.trim().length<3 || name.trim().length<2 || !email.includes('@')} onPress={()=>Alert.alert('Cấp tài khoản',`Cấp quyền ${userRoleLabel(role,lang)} cho ${email}?`,[{text:'Hủy',style:'cancel'},{text:'Xác nhận',onPress:()=>{void perform(async()=>{const result=await apiClient.createCompanyUser({email,displayName:name,role,reason,confirmed:true});setInvitation(result.activationUrl ? `${String(Constants.expoConfig?.extra?.webUrl ?? '').replace(/\/$/, '')}${role === 'Employee' ? '/employee' : '/company'}${result.activationUrl}` : '');setEmail('');setName('');});}}])}/>
      {invitation && <><ServiceText>Link dùng một lần — gửi riêng cho người nhận:</ServiceText><AppInput label="Link kích hoạt" value={invitation} editable={false}/><AppButton title="Đã lưu — đóng" onPress={()=>setInvitation('')}/></>}
    </AppCard>
    {users.data?.map(item=><AppCard key={item.id}><ServiceText heading>{userDisplayName(item.displayName,item.role,lang)}</ServiceText><ServiceText>{item.email} · {userRoleLabel(item.role,lang)} · {item.status}</ServiceText>{item.id!==user.id && item.status!=='PendingActivation' && <AppButton title={item.status==='Locked'?'Mở lại tài khoản':'Khóa tài khoản'} disabled={busy || reason.trim().length<3} onPress={()=>Alert.alert('Đổi quyền truy cập',`Xác nhận đổi trạng thái ${item.email}?`,[{text:'Hủy',style:'cancel'},{text:'Xác nhận',onPress:()=>{void perform(()=>apiClient.setCompanyUserStatus(item.id,item.status==='Locked'?'Active':'Locked',reason));}}])}/>}</AppCard>)}
    <ServicePages offset={offset} count={users.data?.length ?? 0} onChange={setOffset}/>
  </ServicePage>;
}
