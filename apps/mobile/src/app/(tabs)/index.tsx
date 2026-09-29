import React from 'react';
import { SelfServiceHomeView } from '../../features/self-service/home-view';
import { OperatorView } from '../../features/it-operator/operator-view';
import { useAuth } from '../../features/auth/auth-context';
export default function OverviewScreen() { const {user}=useAuth(); return user?.role === 'Employee' ? <SelfServiceHomeView /> : <OperatorView />; }
