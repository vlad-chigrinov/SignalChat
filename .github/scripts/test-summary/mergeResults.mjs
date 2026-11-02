/** Adds up the results of several result files of one kind of test (a solution can have several test projects). */
export function mergeResults(results) {
  const merged = { total: 0, passed: 0, failed: 0, skipped: 0, flaky: 0, durationMs: null, failures: [] }
  for (const result of results) {
    merged.total += result.total
    merged.passed += result.passed
    merged.failed += result.failed
    merged.skipped += result.skipped
    merged.flaky += result.flaky
    // The projects run one after another, so their times add up.
    if (result.durationMs !== null) merged.durationMs = (merged.durationMs ?? 0) + result.durationMs
    merged.failures.push(...result.failures)
  }
  return merged
}
