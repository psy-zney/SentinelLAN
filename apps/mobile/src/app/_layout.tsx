import React from 'react';
import { Stack, Redirect, useSegments } from 'expo-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { StatusBar } from 'expo-status-bar';
import { I18nProvider } from '../lib/i18n';
import { AuthProvider, useAuth } from '../features/auth/auth-context';
import { useAppColors } from '../components/common';
import { EmployeePushObserver } from '../features/self-service/push-observer';
import { BackNavigation } from '../components/back-navigation';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 1000 * 30, // 30 seconds
    },
  },
});

function RootNavigationLayout() {
  const colors = useAppColors();
  const {user,status} = useAuth();
  const segments = useSegments() as string[];
  const employeeOnly = ['my-device','incidents','incident-create','incident-detail','request-create','app-catalog','self-service-help'];
  if (status === 'authenticated' && user?.role !== 'Employee' && segments.some(part=>employeeOnly.includes(part))) return <Redirect href="/(tabs)"/>;
  if (status === 'authenticated' && user?.role !== 'Admin' && segments.some(part => ['company-users', 'vps-status', 'device-detail'].includes(part))) return <Redirect href="/(tabs)"/>;

  return (
    <>
      <StatusBar style="auto" />
      <EmployeePushObserver />
      <Stack
        screenOptions={{
          headerLeft: () => <BackNavigation />,
          headerStyle: {
            backgroundColor: colors.surface,
          },
          headerTintColor: colors.primary,
          headerTitleStyle: {
            fontWeight: '700',
          },
          contentStyle: {
            backgroundColor: colors.background,
          },
        }}
      >
        <Stack.Screen name="index" options={{ headerShown: false }} />
        <Stack.Screen name="login" options={{ title: 'Đăng nhập' }} />
        <Stack.Screen name="activate" options={{ title: 'Kích hoạt tài khoản' }} />
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="incident-create" options={{ title: 'Báo sự cố mới' }} />
        <Stack.Screen name="incident-detail" options={{ title: 'Chi tiết sự cố' }} />
        <Stack.Screen name="request-create" options={{ title: 'Nhờ IT hỗ trợ' }} />
        <Stack.Screen name="request-detail" options={{ title: 'Tiến độ và trao đổi' }} />
        <Stack.Screen name="company-users" options={{ title: 'Tài khoản công ty' }} />
        <Stack.Screen name="vps-status" options={{ title: 'Theo dõi VPS' }} />
        <Stack.Screen name="device-detail" options={{ title: 'Chi tiết máy' }} />
        <Stack.Screen name="support-requests" options={{ title: 'Yêu cầu của tôi' }} />
        <Stack.Screen name="app-catalog" options={{ title: 'Phần mềm công ty' }} />
        <Stack.Screen name="self-service-help" options={{ title: 'Hướng dẫn dễ làm' }} />
        <Stack.Screen name="employee-notifications" options={{ title: 'Thông báo' }} />
        <Stack.Screen name="privacy-manifest" options={{ title: 'Cam kết minh bạch' }} />
      </Stack>
    </>
  );
}

export default function RootLayout() {
  return (
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <AuthProvider>
          <RootNavigationLayout />
        </AuthProvider>
      </I18nProvider>
    </QueryClientProvider>
  );
}
