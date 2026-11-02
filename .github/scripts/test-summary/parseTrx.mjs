import { stripAnsi } from './text.mjs'

const RESULT_ELEMENT = /<UnitTestResult\b([^>]*?)(?:\/>|>([\s\S]*?)<\/UnitTestResult>)/g
const ERROR_MESSAGE = /<Message>([\s\S]*?)<\/Message>/
const RUN_TIMES = /<Times\b[^>]*\bstart="([^"]+)"[^>]*\bfinish="([^"]+)"/
const FAILED_OUTCOMES = new Set(['Failed', 'Error', 'Timeout', 'Aborted'])
/** .NET writes seven fractional digits; a date string takes three. */
const EXTRA_FRACTION_DIGITS = /(\.\d{3})\d+/

const XML_NAMED_ENTITIES = { lt: '<', gt: '>', quot: '"', apos: "'" }

function decodeXml(text) {
  return text
    .replace(/&#x([0-9A-Fa-f]+);/g, (_, hex) => String.fromCodePoint(parseInt(hex, 16)))
    .replace(/&#(\d+);/g, (_, decimal) => String.fromCodePoint(parseInt(decimal, 10)))
    .replace(/&(lt|gt|quot|apos);/g, (_, name) => XML_NAMED_ENTITIES[name])
    .replace(/&amp;/g, '&')
}

function attribute(attributes, name) {
  const match = new RegExp(`\\b${name}="([^"]*)"`).exec(attributes)
  return match ? decodeXml(match[1]) : ''
}

function runDurationMs(trx) {
  const times = RUN_TIMES.exec(trx)
  if (!times) return null
  const [start, finish] = times
    .slice(1, 3)
    .map((value) => Date.parse(value.replace(EXTRA_FRACTION_DIGITS, '$1')))
  return Number.isNaN(start) || Number.isNaN(finish) ? null : Math.round(finish - start)
}

/** Reads a .trx file (the VSTest result format that `dotnet test --logger trx` writes). */
export function parseTrx(trx) {
  const result = {
    total: 0,
    passed: 0,
    failed: 0,
    skipped: 0,
    flaky: 0,
    durationMs: runDurationMs(trx),
    failures: [],
  }

  for (const [, attributes, body = ''] of trx.matchAll(RESULT_ELEMENT)) {
    const outcome = attribute(attributes, 'outcome')
    result.total += 1
    if (outcome === 'Passed') {
      result.passed += 1
    } else if (FAILED_OUTCOMES.has(outcome)) {
      result.failed += 1
      const message = ERROR_MESSAGE.exec(body)
      result.failures.push({
        name: attribute(attributes, 'testName'),
        message: message ? stripAnsi(decodeXml(message[1])).replaceAll('\r', '').trim() : '',
      })
    } else {
      result.skipped += 1
    }
  }
  return result
}
