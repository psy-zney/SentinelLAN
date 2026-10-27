import type { QueryClient } from '@tanstack/react-query';
import type { SupportRequest } from '../../lib/validation/self-service';

// Publish the server receipt before uploading optional images or navigating away.
export async function cacheCreatedRequest(client: QueryClient, request: SupportRequest) {
  await client.cancelQueries({ queryKey: ['support-requests'] });
  client.setQueryData(['support-request', request.id], request);
  client.setQueryData<SupportRequest[]>(['support-requests', 0], previous =>
    [request, ...(previous ?? []).filter(item => item.id !== request.id)].slice(0, 100));
  await client.invalidateQueries({ queryKey: ['support-requests'] });
}
