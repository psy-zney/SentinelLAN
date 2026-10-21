import { afterEach, describe, expect, it, vi } from "vitest";
import { startVisiblePolling } from "../src/lib/visible-poller";

function visibility() {
  const target = new EventTarget();
  let hidden = false;
  return {
    document: Object.assign(target, { get hidden() { return hidden; } }) as Document,
    change(value: boolean) { hidden = value; Object.defineProperty(target, "hidden", { value, configurable: true }); target.dispatchEvent(new Event("visibilitychange")); }
  };
}
afterEach(() => vi.useRealTimers());
describe("VPS polling while the admin is viewing the page", () => {
  it("checks on mount, uses the selected minutes and stops after cleanup", async () => {
    vi.useFakeTimers();
    const page = visibility(); const run = vi.fn().mockResolvedValue(undefined);
    const stop = startVisiblePolling(run, 3, page.document);
    await vi.advanceTimersByTimeAsync(0); expect(run).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(179_999); expect(run).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(1); expect(run).toHaveBeenCalledTimes(2);
    stop(); await vi.advanceTimersByTimeAsync(600_000); expect(run).toHaveBeenCalledTimes(2);
  });
  it("pauses hidden pages and checks an overdue sample once when visible", async () => {
    vi.useFakeTimers();
    const page = visibility(); const run = vi.fn().mockResolvedValue(undefined);
    const stop = startVisiblePolling(run, 1, page.document);
    await vi.advanceTimersByTimeAsync(0); page.change(true);
    await vi.advanceTimersByTimeAsync(300_000); expect(run).toHaveBeenCalledTimes(1);
    page.change(false); await vi.advanceTimersByTimeAsync(0); expect(run).toHaveBeenCalledTimes(2);
    page.change(true); page.change(false); await vi.advanceTimersByTimeAsync(0); expect(run).toHaveBeenCalledTimes(2);
    stop();
  });
  it("waits for a slow probe instead of overlapping it", async () => {
    vi.useFakeTimers();
    const page = visibility(); let resolve!: () => void;
    const run = vi.fn().mockImplementationOnce(() => new Promise<void>(done => { resolve = done; })).mockResolvedValue(undefined);
    const stop = startVisiblePolling(run, 1, page.document);
    await vi.advanceTimersByTimeAsync(0); await vi.advanceTimersByTimeAsync(180_000);
    expect(run).toHaveBeenCalledTimes(1); page.change(true); page.change(false); expect(run).toHaveBeenCalledTimes(1);
    resolve(); await vi.advanceTimersByTimeAsync(0); await vi.advanceTimersByTimeAsync(60_000);
    expect(run).toHaveBeenCalledTimes(2); stop();
  });
  it("does not execute the abandoned initial effect in React Strict Mode", async () => {
    vi.useFakeTimers();
    const page = visibility(); const run = vi.fn().mockResolvedValue(undefined);
    const stop = startVisiblePolling(run, 1, page.document); stop();
    const second = startVisiblePolling(run, 1, page.document);
    await vi.advanceTimersByTimeAsync(0); expect(run).toHaveBeenCalledTimes(1); second();
  });
});
