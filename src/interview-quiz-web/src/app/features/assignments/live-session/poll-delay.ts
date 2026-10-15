/** Steady interval, or 1s, 2s, 4s… capped at 30s, plus up to 200ms of jitter. */
export function pollDelayMs(failures: number, steadyMs: number, random: () => number = Math.random): number {
  if (failures <= 0) {
    return steadyMs;
  }
  const exponent = Math.min(failures - 1, 8);
  const base = Math.min(30_000, 2 ** exponent * 1000);
  const jitter = Math.floor(Math.max(0, Math.min(1, random())) * 201);
  return base + jitter;
}
