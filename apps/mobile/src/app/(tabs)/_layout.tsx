import React from 'react';
import { Text, View } from 'react-native';
import { Redirect, Tabs } from 'expo-router';
import { useI18n } from '../../lib/i18n';
import { useAppColors } from '../../components/common';
import { useAuth } from '../../features/auth/auth-context';

export default function TabsLayout() {
  const { t } = useI18n();
  const colors = useAppColors();
  const { status } = useAuth();

  if (status !== 'authenticated') return <Redirect href="/" />;

  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.inkSubtle,
        tabBarStyle: {
          backgroundColor: colors.surface,
          borderTopColor: colors.border,
          height: 60,
          paddingBottom: 8,
          paddingTop: 6,
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
          tabBarIcon: ({ focused }) => (
            <Text style={{ fontSize: 20, opacity: focused ? 1 : 0.6 }}>📊</Text>
          ),
        }}
      />
      <Tabs.Screen
        name="scan"
        options={{
          title: t('tabScan'),
          tabBarIcon: ({ focused }) => (
            <Text style={{ fontSize: 20, opacity: focused ? 1 : 0.6 }}>📷</Text>
          ),
        }}
      />
      <Tabs.Screen
        name="my-device"
        options={{
          title: t('tabMyDevice'),
          tabBarIcon: ({ focused }) => (
            <Text style={{ fontSize: 20, opacity: focused ? 1 : 0.6 }}>💻</Text>
          ),
        }}
      />
      <Tabs.Screen
        name="incidents"
        options={{
          title: t('tabIncidents'),
          tabBarIcon: ({ focused }) => (
            <Text style={{ fontSize: 20, opacity: focused ? 1 : 0.6 }}>⚠️</Text>
          ),
        }}
      />
      <Tabs.Screen
        name="account"
        options={{
          title: t('tabAccount'),
          tabBarIcon: ({ focused }) => (
            <Text style={{ fontSize: 20, opacity: focused ? 1 : 0.6 }}>👤</Text>
          ),
        }}
      />
    </Tabs>
  );
}
