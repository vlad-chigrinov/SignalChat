import assert from 'node:assert/strict'
import { test } from 'node:test'

import { renderSummary } from './renderSummary.mjs'

const GREEN = { total: 3, passed: 3, failed: 0, skipped: 0, flaky: 0, durationMs: 2_500, failures: [] }
const RED = {
  total: 2,
  passed: 1,
  failed: 1,
  skipped: 0,
  flaky: 0,
  durationMs: null,
  failures: [{ name: 'a `breaks`', message: 'expected 1 to be 2' }],
}

test('has a row per suite, in the order given', () => {
  const markdown = renderSummary([
    { title: 'Backend', result: GREEN },
    { title: 'Frontend', result: RED },
  ])

  assert.match(markdown, /\| Backend \| ✅ \| 3 \| 3 \| 0 \| 0 \| 0 \| 2\.5 s \|/)
  assert.match(markdown, /\| Frontend \| ❌ \| 2 \| 1 \| 1 \| 0 \| 0 \| — \|/)
  assert.ok(markdown.indexOf('Backend') < markdown.indexOf('Frontend'))
})

test('says so when a suite left no results', () => {
  const markdown = renderSummary([{ title: 'E2E', result: null }])

  assert.match(markdown, /\| E2E \| ⚠️ no results/)
})

test('shows a failed test under its suite, safe inside a code span', () => {
  const markdown = renderSummary([{ title: 'Frontend', result: RED }])

  assert.match(markdown, /<details>/)
  assert.match(markdown, /Frontend: 1 failed/)
  assert.ok(!markdown.includes('`breaks`'), 'backticks of the name are removed')
  assert.match(markdown, /expected 1 to be 2/)
})

test('cuts a long list of failures and says how many were left out', () => {
  const failures = Array.from({ length: 40 }, (_, index) => ({ name: `t${index}`, message: 'x' }))
  const markdown = renderSummary([{ title: 'Backend', result: { ...RED, failed: 40, failures } }])

  assert.match(markdown, /and 20 more/)
})
