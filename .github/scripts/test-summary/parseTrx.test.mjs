import assert from 'node:assert/strict'
import { test } from 'node:test'

import { parseTrx } from './parseTrx.mjs'

const TRX = `\uFEFF<?xml version="1.0" encoding="utf-8"?>
<TestRun id="x" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Times creation="2026-11-02T16:51:20.8899873+03:00" start="2026-11-02T16:51:16.0000000+03:00" finish="2026-11-02T16:51:26.5000000+03:00" />
  <Results>
    <UnitTestResult testName="Suite.Passes" duration="00:00:00.0100000" outcome="Passed" />
    <UnitTestResult testName="Suite.Fails(value: &quot;1&quot;)" duration="00:00:00.0200000" outcome="Failed">
      <Output>
        <ErrorInfo>
          <Message>Assert.Equal() Failure&#xD;
Expected: 2&#xD;
Actual:   1</Message>
          <StackTrace>at Suite.Fails()</StackTrace>
        </ErrorInfo>
      </Output>
    </UnitTestResult>
    <UnitTestResult testName="Suite.Skipped" outcome="NotExecuted" />
  </Results>
  <ResultSummary outcome="Failed"><Counters total="3" passed="1" failed="1" /></ResultSummary>
</TestRun>`

test('counts the outcomes and measures the run from its start and finish', () => {
  const result = parseTrx(TRX)

  assert.equal(result.total, 3)
  assert.equal(result.passed, 1)
  assert.equal(result.failed, 1)
  assert.equal(result.skipped, 1)
  assert.equal(result.flaky, 0)
  assert.equal(result.durationMs, 10_500)
})

test('lists a failed test with its decoded name and message', () => {
  const [failure] = parseTrx(TRX).failures

  assert.equal(failure.name, 'Suite.Fails(value: "1")')
  assert.equal(failure.message, 'Assert.Equal() Failure\nExpected: 2\nActual:   1')
})

test('an empty run has no tests and no failures', () => {
  const result = parseTrx('<TestRun><Results></Results></TestRun>')

  assert.equal(result.total, 0)
  assert.deepEqual(result.failures, [])
})
