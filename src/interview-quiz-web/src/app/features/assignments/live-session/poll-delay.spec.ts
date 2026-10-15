import { pollDelayMs } from './poll-delay';

describe('pollDelayMs', () => {
  it('uses the steady interval until a failure', () => {
    expect(pollDelayMs(0, 3000, () => 0)).toBe(3000);
  });

  it('backs off from 1s to 30s', () => {
    expect(pollDelayMs(1, 3000, () => 0)).toBe(1000);
    expect(pollDelayMs(2, 3000, () => 0)).toBe(2000);
    expect(pollDelayMs(3, 3000, () => 0)).toBe(4000);
    expect(pollDelayMs(6, 3000, () => 0)).toBe(30_000);
    expect(pollDelayMs(12, 3000, () => 0)).toBe(30_000);
  });

  it('adds at most 200ms of jitter', () => {
    expect(pollDelayMs(1, 3000, () => 0.999)).toBe(1200);
  });
});
