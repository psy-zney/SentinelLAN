import React from 'react';
import { usePathname } from 'expo-router';
import { ScannerView } from '../../features/scan/scanner-view';

export default function ScanTab() {
  const pathname = usePathname();
  const isCurrentTab = pathname === '/scan' || pathname.endsWith('/scan');

  return <ScannerView isActiveScreen={isCurrentTab} />;
}
