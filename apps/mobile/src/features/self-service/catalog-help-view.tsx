import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { AppButton, AppCard } from '../../components/common';
import { useAuth } from '../auth/auth-context';
import { ServicePage, ServiceText, ServiceError, useActiveScreen } from './shared';

export function CatalogView() {
  const { apiClient } = useAuth();
  const active = useActiveScreen();
  const router = useRouter();
  const apps = useQuery({ queryKey: ['app-catalog'], queryFn: () => apiClient.appCatalog(), enabled: active });
  return <ServicePage>
    <ServiceText heading>Phần mềm được công ty cho phép</ServiceText>
    <ServiceText>Chọn ứng dụng bạn cần. IT có thể cần duyệt trước khi máy nhận lệnh cài đặt.</ServiceText>
    <ServiceError error={apps.error} retry={() => { void apps.refetch(); }} />
    {apps.isPending ? <ServiceText>Đang tải…</ServiceText> : apps.data?.filter(app => app.isActive).length === 0 ? <ServiceText>IT chưa công bố ứng dụng. Bạn có thể gửi nhu cầu bên dưới.</ServiceText> : null}
    {apps.data?.filter(app => app.isActive).map(app => <AppCard key={app.id}>
      <ServiceText heading>{app.name}</ServiceText><ServiceText>{app.description}</ServiceText><ServiceText>Phiên bản {app.version} · {app.requiresApproval ? 'Cần IT duyệt' : 'Được phép yêu cầu cài'}</ServiceText>
      <AppButton title={`Yêu cầu cài ${app.name}`} onPress={() => router.push({ pathname: '/request-create', params: { kind: 'InstallApp', catalogAppId: app.id, category: 'Application', title: `Xin cài ${app.name}` } })} />
      <AppButton title="Nhờ IT duyệt cài ứng dụng cần quyền cao" variant="outline" onPress={() => router.push({ pathname: '/request-create', params: { kind: 'Privilege', catalogAppId: app.id, category: 'Application', title: `Xin IT duyệt cài ${app.name}` } })} />
    </AppCard>)}
    <AppButton title="Không có phần mềm tôi cần — gửi nhu cầu" variant="outline" onPress={() => router.push({ pathname: '/request-create', params: { category: 'Application', title: 'Tôi cần phần mềm chưa có trong danh sách' } })} />
  </ServicePage>;
}
export function HelpView() {
  const { apiClient } = useAuth();
  const active = useActiveScreen();
  const router = useRouter();
  const articles = useQuery({ queryKey: ['self-service-help'], queryFn: () => apiClient.helpArticles(), enabled: active });
  return <ServicePage>
    <ServiceText heading>Hướng dẫn lỗi thường gặp</ServiceText>
    <ServiceText>Bạn có thể thử từng bước đơn giản hoặc nhờ IT ngay.</ServiceText>
    <ServiceError error={articles.error} retry={() => { void articles.refetch(); }} />
    {articles.isPending ? <ServiceText>Đang tải…</ServiceText> : articles.data?.length === 0 ? <ServiceText>Chưa có hướng dẫn.</ServiceText> : null}
    {articles.data?.map(article => <AppCard key={article.id}>
      <ServiceText heading>{article.title}</ServiceText>
      {article.steps.map((step, index) => <ServiceText key={index}>{index + 1}. {step}</ServiceText>)}
      <AppButton title="Vẫn chưa được — nhờ IT" variant="outline" onPress={() => router.push({ pathname: '/request-create', params: { category: article.category, title: article.title } })} />
    </AppCard>)}
    <AppButton title="Gửi yêu cầu cho IT" onPress={() => router.push('/request-create')} />
  </ServicePage>;
}
