import React from 'react';
import { RequestCreateView } from '../self-service/request-create-view';
// Existing report links now use the employee-friendly support flow. Legacy reports remain readable.
export function IncidentCreateView() { return <RequestCreateView />; }
