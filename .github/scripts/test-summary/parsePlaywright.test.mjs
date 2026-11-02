import assert from 'node:assert/strict'
import { test } from 'node:test'

import { parsePlaywright } from './parsePlaywright.mjs'

const ESC = String.fromCharCode(27)

const REPORT = {
  stats: { duration: 16403.4, expected: 1, unexpected: 1, flaky: 1, skipped: 1 },
  suites: [
    {
      title: 'a.spec.ts',
      specs: [
        {
          title: 'breaks',
          ok: false,
          file: 'a.spec.ts',
          line: 5,
          tests: [
            {
              results: [
                { status: 'failed', errors: [{ message: 'first attempt' }] },
                { status: 'failed', errors: [{ message: `Error: ${ESC}[2mexpect(${ESC}[22mreceived${ESC}[2m)${ESC}[22m.toBe()` }] },
              ],
            },
          ],
        },
      ],
      suites: [
        {
          title: 'nested',
          specs: [{ title: 'is fine', ok: true, file: 'a.spec.ts', line: 20, tests: [{ results: [{ status: 'passed', errors: [] }] }] }],
        },
      ],
    },
  ],
}

test('counts a flaky test as its own kind and keeps the total', () => {
  const result = parsePlaywright(REPORT)

  assert.equal(result.total, 4)
  assert.equal(result.passed, 1)
  assert.equal(result.failed, 1)
  assert.equal(result.skipped, 1)
  assert.equal(result.flaky, 1)
  assert.equal(result.durationMs, 16_403)
})

test('lists only the failed spec, with the last error and no colour codes', () => {
  const { failures } = parsePlaywright(REPORT)

  assert.equal(failures.length, 1)
  assert.equal(failures[0].name, 'breaks (a.spec.ts:5)')
  assert.equal(failures[0].message, 'Error: expect(received).toBe()')
})
