import { stripAnsi } from './text.mjs'

function* specsOf(suite) {
  yield* suite.specs ?? []
  for (const child of suite.suites ?? []) yield* specsOf(child)
}

/** The error of the last attempt: earlier attempts of a retried test only repeat it. */
function lastError(spec) {
  const results = spec.tests.at(-1)?.results ?? []
  const message = results.at(-1)?.errors?.[0]?.message ?? ''
  return stripAnsi(message).trim()
}

/** Reads the JSON that Playwright's `json` reporter writes. */
export function parsePlaywright(report) {
  const { expected, unexpected, flaky, skipped, duration } = report.stats
  const failures = []

  for (const suite of report.suites) {
    for (const spec of specsOf(suite)) {
      if (spec.ok) continue
      failures.push({ name: `${spec.title} (${spec.file}:${spec.line})`, message: lastError(spec) })
    }
  }
  // A failure of the run itself (the dev server did not start) belongs to no spec.
  for (const error of report.errors ?? []) {
    failures.push({ name: 'Run error', message: stripAnsi(error.message ?? '').trim() })
  }

  return {
    total: expected + unexpected + flaky + skipped,
    passed: expected,
    failed: Math.max(unexpected, failures.length),
    skipped,
    flaky,
    durationMs: Math.round(duration),
    failures,
  }
}
