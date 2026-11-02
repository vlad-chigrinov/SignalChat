import assert from 'node:assert/strict'
import { test } from 'node:test'

import { parseVitest } from './parseVitest.mjs'

const REPORT = {
  numTotalTests: 3,
  numPassedTests: 1,
  numFailedTests: 1,
  numPendingTests: 1,
  numTodoTests: 0,
  testResults: [
    {
      name: 'C:/work/signalchat/frontend/src/a.spec.ts',
      status: 'failed',
      assertionResults: [
        { fullName: 'a works', status: 'passed', failureMessages: [] },
        { fullName: 'a breaks', status: 'failed', failureMessages: ['AssertionError: expected 1 to be 2\n    at a.spec.ts:5'] },
      ],
    },
    {
      name: 'C:/work/signalchat/frontend/src/b.spec.ts',
      status: 'failed',
      message: 'Failed to resolve import "./missing"',
      assertionResults: [],
    },
  ],
}

test('takes the counts from the report', () => {
  const result = parseVitest(REPORT)

  assert.equal(result.total, 4)
  assert.equal(result.passed, 1)
  assert.equal(result.failed, 2)
  assert.equal(result.skipped, 1)
  assert.equal(result.durationMs, null)
})

test('lists a failed test with the file it is in', () => {
  const [failure] = parseVitest(REPORT).failures

  assert.equal(failure.name, 'a breaks (src/a.spec.ts)')
  assert.match(failure.message, /expected 1 to be 2/)
})

test('lists a file that failed to load, which has no tests of its own', () => {
  const failure = parseVitest(REPORT).failures[1]

  assert.equal(failure.name, 'src/b.spec.ts')
  assert.match(failure.message, /Failed to resolve import/)
})
