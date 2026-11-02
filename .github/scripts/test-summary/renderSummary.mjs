/** A long list of failures would bury the page; the first ones tell the story, the log has the rest. */
const MAX_LISTED_FAILURES = 20
const MAX_MESSAGE_LINES = 12
const MILLISECONDS_PER_SECOND = 1000

function formatDuration(durationMs) {
  return durationMs === null ? '—' : `${(durationMs / MILLISECONDS_PER_SECOND).toFixed(1)} s`
}

function statusOf(result) {
  if (result.failed > 0) return '❌'
  return result.total === 0 ? '⚠️' : '✅'
}

function row({ title, result }) {
  if (result === null) return `| ${title} | ⚠️ no results | | | | | | |`
  const { total, passed, failed, skipped, flaky, durationMs } = result
  return `| ${title} | ${statusOf(result)} | ${total} | ${passed} | ${failed} | ${skipped} | ${flaky} | ${formatDuration(durationMs)} |`
}

function failureBlock({ name, message }) {
  const lines = message.split('\n')
  const shown = lines.slice(0, MAX_MESSAGE_LINES)
  if (lines.length > shown.length) shown.push(`… ${lines.length - shown.length} more lines`)
  return [`**\`${name.replaceAll('`', '')}\`**`, '', '~~~~', shown.join('\n'), '~~~~', ''].join('\n')
}

function failureSection({ title, result }) {
  if (result === null || result.failures.length === 0) return ''
  const listed = result.failures.slice(0, MAX_LISTED_FAILURES)
  const hidden = result.failures.length - listed.length
  return [
    '<details>',
    `<summary>${title}: ${result.failures.length} failed</summary>`,
    '',
    ...listed.map(failureBlock),
    hidden > 0 ? `…and ${hidden} more (see the job log).` : '',
    '</details>',
    '',
  ].join('\n')
}

/** Builds the markdown of the run page: one table, a row per kind of test, failures folded under it. */
export function renderSummary(suites) {
  return [
    '## Test report',
    '',
    '| Suite | Status | Total | Passed | Failed | Skipped | Flaky | Time |',
    '|---|---|---:|---:|---:|---:|---:|---:|',
    ...suites.map(row),
    '',
    ...suites.map(failureSection),
  ].join('\n')
}
