import React, { useState } from 'react';
import Constants from 'expo-constants';
import { Alert } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { AppButton, AppCard, AppInput } from '../../components/common';
import { useAuth } from '../auth/auth-context';
import { ServicePage, ServiceText, ServiceError, useActiveScreen } from '../self-service/shared';

export function CompanyUsersView() {
  const {user,apiClient}=useAuth();const active=useActiveScreen();
  const users=useQuery({queryKey:['company-users'],queryFn:()=>apiClient.operatorUsers(),enabled:active && user?.role==='Admin'});
  const [email,setEmail]=useState('');const [name,setName]=useState('');const [role,setRole]=useState<'Employee'|'Technician'|'Admin'>('Employee');const [reason,setReason]=useState('');const [invitation,setInvitation]=useState('');const [error,setError]=useState<unknown>();const [busy,setBusy]=useState(false);
  async function perform(action:()=>Promise<unknown>) {if(busy)return;setBusy(true);setError(undefined);try{await action();await users.refetch();}catch(cause){setError(cause);}finally{setBusy(false);}}
  if(user?.role!=='Admin')return <ServicePage><ServiceText>Chỉ Admin công ty được quản lý tài khoản.</ServiceText></ServicePage>;
  return <ServicePage><ServiceText heading>Tài khoản công ty</ServiceText><ServiceError error={error || users.error} retry={()=>{void users.refetch();}}/>
    <AppInput label="Lý do cấp hoặc đổi quyền truy cập" value={reason} onChangeText={setReason}/>
    <AppCard><ServiceText heading>Cấp tài khoản mới</ServiceText><AppInput label="Họ tên" value={name} onChangeText={setName}/><AppInput label="Email" value={email} onChangeText={setEmail} keyboardType="email-address" autoCapitalize="none"/>
      {(['Employee','Technician','Admin'] as const).map(value=><AppButton key={value} title={`${role===value?'✓ ':''}${value==='Employee'?'Nhân viên':value==='Technician'?'Kỹ thuật viên':'Admin công ty'}`} variant="outline" onPress={()=>setRole(value)}/>)}
      <AppButton title="Tạo link kích hoạt" disabled={busy || reason.trim().length<3 || name.trim().length<2 || !email.includes('@')} onPress={()=>Alert.alert('Cấp tài khoản',`Cấp quyền ${role} cho ${email}?`,[{text:'Hủy',style:'cancel'},{text:'Xác nhận',onPress:()=>{void perform(async()=>{const result=await apiClient.createCompanyUser({email,displayName:name,role,reason,confirmed:true});setInvitation(result.activationUrl ? `${String(Constants.expoConfig?.extra?.webUrl ?? '').replace(/\/$/, '')}${role === 'Employee' ? '/employee' : '/company'}${result.activationUrl}` : '');setEmail('');setName('');});}}])}/>
      {invitation && <><ServiceText>Link dùng một lần — gửi riêng cho người nhận:</ServiceText><AppInput label="Link kích hoạt" value={invitation} editable={false}/><AppButton title="Đã lưu — đóng" onPress={()=>setInvitation('')}/></>}
    </AppCard>
    {users.data?.map(item=><AppCard key={item.id}><ServiceText heading>{item.displayName}</ServiceText><ServiceText>{item.email} · {item.role} · {item.status}</ServiceText>{item.id!==user.id && item.status!=='PendingActivation' && <AppButton title={item.status==='Locked'?'Mở lại tài khoản':'Khóa tài khoản'} disabled={busy || reason.trim().length<3} onPress={()=>Alert.alert('Đổi quyền truy cập',`Xác nhận đổi trạng thái ${item.email}?`,[{text:'Hủy',style:'cancel'},{text:'Xác nhận',onPress:()=>{void perform(()=>apiClient.setCompanyUserStatus(item.id,item.status==='Locked'?'Active':'Locked',reason));}}])}/>}</AppCard>)}
  </ServicePage>;
}
