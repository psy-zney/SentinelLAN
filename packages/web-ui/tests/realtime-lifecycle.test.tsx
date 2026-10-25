// @vitest-environment jsdom
import React from "react";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useLiveQuery } from "../src/hooks/use-live-query";
import { useDeviceUpdates } from "../src/hooks/use-device-updates";
import { EndpointObservationPanel } from "../src/components/endpoint-observation-panel";
import { SoftwareCompliancePanel } from "../src/components/software-compliance-panel";
import { fakeApi, renderProviders, resetApi } from "./helpers";

vi.mock("@/lib/api-client", async () => {
  const { fakeApi } = await import("./helpers");
  return { ApiClient: class { constructor() { return fakeApi; } } };
});

const hub = vi.hoisted(() => ({
  start: vi.fn(), stop: vi.fn(), on: vi.fn(), onclose: vi.fn(), onreconnected: vi.fn(), withUrl: vi.fn()
}));
vi.mock("@microsoft/signalr", () => ({ HubConnectionBuilder: class {
  withUrl(...args: unknown[]) { hub.withUrl(...args); return this; }
  withAutomaticReconnect() { return this; }
  build() { return hub; }
} }));

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: Error) => void;
  const promise = new Promise<T>((ok, no) => { resolve = ok; reject = no; });
  return { promise, resolve, reject };
}

beforeEach(() => {
  vi.clearAllMocks();
  hub.start.mockImplementation(() => new Promise<void>(() => {}));
  hub.stop.mockResolvedValue(undefined);
});
afterEach(() => { cleanup(); vi.useRealTimers(); vi.unstubAllEnvs(); });

describe("live query consistency", () => {
  it("loads and manually refreshes employee data without opening the administrator hub", async () => {
    const load = vi.fn().mockResolvedValueOnce("initial").mockResolvedValue("refreshed");
    const { result } = renderHook(() => useLiveQuery(load, false));
    await waitFor(() => expect(result.current.data).toBe("initial"));
    act(() => result.current.refresh());
    await waitFor(() => expect(result.current.data).toBe("refreshed"));
    expect(hub.withUrl).not.toHaveBeenCalled();
    expect(hub.start).not.toHaveBeenCalled();
  });
  it("ignores a slower response from an older request", async () => {
    const first = deferred<string>();
    const load = vi.fn().mockReturnValueOnce(first.promise).mockResolvedValue("new data");
    const { result } = renderHook(() => useLiveQuery(load));
    act(() => result.current.refresh());
    await waitFor(() => expect(result.current.data).toBe("new data"));
    await act(async () => first.resolve("stale data"));
    expect(result.current.data).toBe("new data");
  });

  it("keeps a stale rejection from replacing the latest successful result", async () => {
    const first = deferred<string>();
    const load = vi.fn().mockReturnValueOnce(first.promise).mockResolvedValue("current");
    const { result } = renderHook(() => useLiveQuery(load));
    act(() => result.current.refresh());
    await waitFor(() => expect(result.current.data).toBe("current"));
    await act(async () => first.reject(new Error("old request failed")));
    expect(result.current.error).toBe(false);
  });

  it("reports a current failure and clears it after an explicit retry", async () => {
    const load = vi.fn().mockRejectedValueOnce(new Error("network unavailable")).mockResolvedValue("recovered");
    const { result } = renderHook(() => useLiveQuery(load));
    await waitFor(() => expect(result.current.error).toBe(true));
    act(() => result.current.refresh());
    await waitFor(() => expect(result.current.data).toBe("recovered"));
    expect(result.current.error).toBe(false);
  });

  it("does not update a disposed query after its request completes", async () => {
    const pending = deferred<string>();
    const { result, unmount } = renderHook(() => useLiveQuery(() => pending.promise));
    unmount();
    await act(async () => pending.resolve("late data"));
    expect(result.current.data).toBeNull();
  });
});

describe("realtime connection lifetime", () => {
  it("uses the current web origin when no API override is configured", () => {
    vi.stubEnv("NEXT_PUBLIC_API_URL", undefined);
    renderHook(() => useDeviceUpdates(vi.fn()));
    expect(hub.withUrl).toHaveBeenCalledWith("/hubs/updates", {
      withCredentials: true, headers: { "X-SentinelLAN-CSRF": "1" }
    });
  });

  it("keeps employee protection panels off the administrator hub", async () => {
    resetApi();
    fakeApi.endpointObservations.mockResolvedValue(null);
    renderProviders(<><EndpointObservationPanel /><SoftwareCompliancePanel /></>);
    await waitFor(() => expect(fakeApi.endpointObservations).toHaveBeenCalledWith(undefined));
    expect(fakeApi.softwareStatus).toHaveBeenCalledWith(undefined);
    expect(hub.start).not.toHaveBeenCalled();
  });

  it("retains realtime updates for explicitly selected administrator devices", async () => {
    resetApi();
    fakeApi.endpointObservations.mockResolvedValue(null);
    renderProviders(<><EndpointObservationPanel deviceId="managed" /><SoftwareCompliancePanel deviceId="managed" /></>);
    await waitFor(() => expect(fakeApi.endpointObservations).toHaveBeenCalledWith("managed"));
    expect(fakeApi.softwareStatus).toHaveBeenCalledWith("managed");
    expect(hub.start).toHaveBeenCalledTimes(2);
  });

  it("does not create a connection for a disabled subscriber", () => {
    renderHook(() => useDeviceUpdates(vi.fn(), false));
    expect(hub.withUrl).not.toHaveBeenCalled();
    expect(hub.start).not.toHaveBeenCalled();
  });

  it("uses cookies and the CSRF header, and reloads on each tenant-scoped event and reconnect", async () => {
    vi.stubEnv("NEXT_PUBLIC_API_URL", "https://api.example.test");
    hub.start.mockResolvedValue(undefined);
    const update = vi.fn();
    renderHook(() => useDeviceUpdates(update));
    await waitFor(() => expect(update).toHaveBeenCalledOnce());
    expect(hub.withUrl).toHaveBeenCalledWith("https://api.example.test/hubs/updates", {
      withCredentials: true, headers: { "X-SentinelLAN-CSRF": "1" }
    });
    expect(hub.on.mock.calls.map(call => call[0])).toEqual(["device-status", "command-status", "policy-assigned", "alert-triggered", "alert-updated"]);
    for (const [, callback] of hub.on.mock.calls) act(() => callback());
    act(() => hub.onreconnected.mock.calls[0][0]());
    expect(update).toHaveBeenCalledTimes(7);
  });

  it("retries a failed start after five seconds and stops when unmounted", async () => {
    vi.useFakeTimers();
    hub.start.mockRejectedValueOnce(new Error("offline")).mockResolvedValue(undefined);
    const update = vi.fn();
    const { unmount } = renderHook(() => useDeviceUpdates(update));
    await act(async () => { await Promise.resolve(); });
    expect(update).not.toHaveBeenCalled();
    await act(async () => vi.advanceTimersByTimeAsync(5000));
    expect(hub.start).toHaveBeenCalledTimes(2);
    expect(update).toHaveBeenCalledOnce();
    unmount();
    await act(async () => { await Promise.resolve(); });
    expect(hub.stop).toHaveBeenCalled();
  });

  it("restarts after a closed connection and cancels scheduled retries during disposal", async () => {
    vi.useFakeTimers();
    hub.start.mockResolvedValue(undefined);
    const { unmount } = renderHook(() => useDeviceUpdates(vi.fn()));
    await act(async () => { await Promise.resolve(); });
    act(() => hub.onclose.mock.calls[0][0]());
    await act(async () => vi.advanceTimersByTimeAsync(5000));
    expect(hub.start).toHaveBeenCalledTimes(2);
    act(() => hub.onclose.mock.calls[0][0]());
    unmount();
    await act(async () => vi.advanceTimersByTimeAsync(5000));
    expect(hub.start).toHaveBeenCalledTimes(2);
  });

  it("stops a late successful start without notifying an unmounted subscriber", async () => {
    const pending = deferred<void>();
    hub.start.mockReturnValue(pending.promise);
    const update = vi.fn();
    const { unmount } = renderHook(() => useDeviceUpdates(update));
    const closed = hub.onclose.mock.calls[0][0];
    unmount();
    await act(async () => pending.resolve());
    act(() => closed());
    expect(update).not.toHaveBeenCalled();
    expect(hub.stop).toHaveBeenCalled();
  });
});
