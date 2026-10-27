// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { useSelfServiceQuery } from '../src/hooks/use-self-service-query';

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: Error) => void;
  const promise = new Promise<T>((ok, no) => { resolve = ok; reject = no; });
  return { promise, resolve, reject };
}
beforeEach(() => { vi.useFakeTimers(); Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' }); });
afterEach(() => { cleanup(); vi.useRealTimers(); });

it('refreshes immediately after submission even when an older poll is pending', async () => {
  const old = deferred<string[]>();
  const load = vi.fn().mockReturnValueOnce(old.promise).mockResolvedValue(['new report']);
  const { result } = renderHook(() => useSelfServiceQuery(load));
  await act(async () => { await vi.advanceTimersByTimeAsync(1); });
  expect(load).toHaveBeenCalledTimes(1);
  await act(async () => { await result.current.refresh(); });
  expect(result.current.data).toEqual(['new report']);
  await act(async () => { old.resolve([]); });
  expect(result.current.data).toEqual(['new report']);
});

it('polls every five seconds, skips hidden tabs and refreshes on focus or reconnection', async () => {
  const load = vi.fn().mockResolvedValue([]);
  renderHook(() => useSelfServiceQuery(load));
  await act(async () => { await vi.advanceTimersByTimeAsync(5001); });
  expect(load).toHaveBeenCalledTimes(2);
  Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'hidden' });
  await act(async () => { await vi.advanceTimersByTimeAsync(15000); });
  expect(load).toHaveBeenCalledTimes(2);
  Object.defineProperty(document, 'visibilityState', { configurable: true, value: 'visible' });
  await act(async () => { document.dispatchEvent(new Event('visibilitychange')); });
  await act(async () => { window.dispatchEvent(new Event('focus')); });
  await act(async () => { window.dispatchEvent(new Event('online')); });
  expect(load).toHaveBeenCalledTimes(5);
});

it('does not allow a disposed request to overwrite a newly selected page', async () => {
  const old = deferred<string>();
  const first = () => old.promise;
  const next = vi.fn().mockResolvedValue('page 2');
  const { result, rerender, unmount } = renderHook(({ load }) => useSelfServiceQuery(load), { initialProps: { load: first } });
  await act(async () => { await vi.advanceTimersByTimeAsync(1); });
  rerender({ load: next });
  await act(async () => { await vi.advanceTimersByTimeAsync(1); });
  await act(async () => { old.reject(new Error('stale failure')); });
  expect(result.current.data).toBe('page 2');
  expect(result.current.error).toBe('');
  unmount();
  await act(async () => { await vi.advanceTimersByTimeAsync(10000); window.dispatchEvent(new Event('focus')); });
  expect(next).toHaveBeenCalledTimes(1);
});
