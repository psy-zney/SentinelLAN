import { act, renderHook, waitFor } from '@testing-library/react-native';
import { AccessibilityInfo } from 'react-native';
import { useReducedMotion } from '../src/components/common';

afterEach(() => jest.restoreAllMocks());

it('enables motion only after the OS preference is known and unsubscribes on unmount', async () => {
  let resolveInitial!: (value: boolean) => void;
  jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockImplementation(() => new Promise(resolve => { resolveInitial = resolve; }));
  const remove = jest.fn();
  // The hook consumes only remove(), not the legacy emitter's internal fields.
  jest.spyOn(AccessibilityInfo, 'addEventListener').mockReturnValue({ remove } as unknown as ReturnType<typeof AccessibilityInfo.addEventListener>);
  const { result, unmount } = await renderHook(() => useReducedMotion());
  expect(result.current).toBe(true);
  await act(() => resolveInitial(false));
  await waitFor(() => expect(result.current).toBe(false));
  await unmount();
  expect(remove).toHaveBeenCalledTimes(1);
});

it('keeps a newer live preference when an older initial read completes later', async () => {
  let resolveInitial!: (value: boolean) => void;
  let changePreference!: (value: boolean) => void;
  jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockImplementation(() => new Promise(resolve => { resolveInitial = resolve; }));
  jest.spyOn(AccessibilityInfo, 'addEventListener').mockImplementation((_event, listener) => {
    // Jest exposes the last overload; the hook subscribes to the boolean event.
    changePreference = listener as unknown as (value: boolean) => void;
    return { remove: jest.fn() } as unknown as ReturnType<typeof AccessibilityInfo.addEventListener>;
  });
  const { result } = await renderHook(() => useReducedMotion());
  await act(async () => {
    changePreference(true);
    resolveInitial(false);
  });
  expect(result.current).toBe(true);
  await act(() => changePreference(false));
  expect(result.current).toBe(false);
});

it('keeps motion disabled if the OS preference cannot be read', async () => {
  jest.spyOn(AccessibilityInfo, 'isReduceMotionEnabled').mockRejectedValue(new Error('Unavailable'));
  const { result } = await renderHook(() => useReducedMotion());
  await act(async () => {});
  expect(result.current).toBe(true);
});
