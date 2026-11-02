import { stripAnsi } from './text.mjs'

/** The report holds absolute paths of the machine that ran it; the project-relative part is what a reader needs. */
function relativePath(path) {
  return path.replaceAll('\\', '/').replace(/^.*\/frontend\//, '')
}

/**
 * The report has no total time. The files run in parallel, so adding up their times would overstate it; the run lasts
 * from its start to the end of the file that finished last.
 */
function runDurationMs(report) {
  const ends = report.testResults.map((file) => file.endTime).filter(Number.isFinite)
  if (!Number.isFinite(report.startTime) || ends.length === 0) return null
  return Math.round(Math.max(...ends) - report.startTime)
}

/** Reads the JSON that `vitest run --reporter=json` writes. */
export function parseVitest(report) {
  const failures = []
  let loadErrors = 0

  for (const file of report.testResults) {
    const path = relativePath(file.name)
    if (file.status === 'failed' && file.assertionResults.length === 0) {
      // The file did not load (a bad import, a syntax error): it has no tests of its own to blame.
      loadErrors += 1
      failures.push({ name: path, message: stripAnsi(file.message ?? '').trim() })
      continue
    }
    for (const test of file.assertionResults) {
      if (test.status !== 'failed') continue
      failures.push({
        name: `${test.fullName} (${path})`,
        message: stripAnsi(test.failureMessages.join('\n')).trim(),
      })
    }
  }

  return {
    total: report.numTotalTests + loadErrors,
    passed: report.numPassedTests,
    failed: report.numFailedTests + loadErrors,
    skipped: report.numPendingTests + report.numTodoTests,
    flaky: 0,
    durationMs: runDurationMs(report),
    failures,
  }
}
