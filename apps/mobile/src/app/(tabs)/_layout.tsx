import React from 'react';
import { Redirect, Tabs } from 'expo-router';
import { LayoutDashboard, QrCode, Monitor, Headset, UserRound } from 'lucide-react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useI18n } from '../../lib/i18n';
import { useAppColors, useReducedMotion } from '../../components/common';
import { useAuth } from '../../features/auth/auth-context';
import { BackNavigation } from '../../components/back-navigation';

export default function TabsLayout() {
  const { t } = useI18n();
  const colors = useAppColors();
  const { status, user } = useAuth();
  const insets = useSafeAreaInsets();
  const reducedMotion = useReducedMotion();

  if (status !== 'authenticated') return <Redirect href="/" />;

  return (
    <Tabs
      backBehavior="history"
      screenOptions={{
        headerLeft: () => <BackNavigation />,
        animation: reducedMotion ? 'none' : 'fade',
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.inkMuted,
        tabBarLabelStyle: { fontSize: 12, fontWeight: '600' },
        tabBarStyle: {
          backgroundColor: colors.surface,
          borderTopColor: colors.border,
          height: 64 + insets.bottom,
          paddingBottom: Math.max(insets.bottom, 8),
          paddingTop: 8,
        },
        headerStyle: {
          backgroundColor: colors.surface,
        },
        headerTintColor: colors.primary,
        headerTitleStyle: {
          fontWeight: '700',
        },
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: t('tabOverview'),
          tabBarIcon: ({ focused, size }) => (
            <LayoutDashboard color={focused ? colors.primary : colors.inkMuted} size={size} accessible={false} />
          ),
        }}
      />
      <Tabs.Screen
        name="scan"
        options={{
          title: t('tabScan'),
          tabBarIcon: ({ focused, size }) => (
            <QrCode color={focused ? colors.primary : colors.inkMuted} size={size} accessible={false} />
          ),
        }}
      />
      <Tabs.Screen
        name="my-device"
        options={{
          href: user?.role === 'Employee' ? '/(tabs)/my-device' : null,
          title: t('tabMyDevice'),
          tabBarIcon: ({ focused, size }) => (
            <Monitor color={focused ? colors.primary : colors.inkMuted} size={size} accessible={false} />
          ),
        }}
      />
      <Tabs.Screen
        name="incidents"
        options={{
          href: user?.role === 'Employee' ? '/(tabs)/incidents' : null,
          title: t('tabIncidents'),
          tabBarIcon: ({ focused, size }) => (
            <Headset color={focused ? colors.primary : colors.inkMuted} size={size} accessible={false} />
          ),
        }}
      />
      <Tabs.Screen
        name="account"
        options={{
          title: t('tabAccount'),
          tabBarIcon: ({ focused, size }) => (
            <UserRound color={focused ? colors.primary : colors.inkMuted} size={size} accessible={false} />
          ),
        }}
      />
    </Tabs>
  );
}
