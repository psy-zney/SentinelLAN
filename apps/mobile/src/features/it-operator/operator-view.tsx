import React from 'react';
import { useAuth } from '../auth/auth-context';
import { ServicePage, ServiceText } from '../self-service/shared';
import { OperatorOverview } from './operator-overview';

export function OperatorView() {
  const { user } = useAuth();
  if (user?.role !== 'Admin') return <ServicePage><ServiceText>Bạn không có quyền quản trị công ty.</ServiceText></ServicePage>;
  const scope = `${user.organizationId}:${user.id}`;
  return <OperatorOverview key={scope} scope={scope} />;
}
