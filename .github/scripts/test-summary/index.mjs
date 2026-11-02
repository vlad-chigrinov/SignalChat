// Writes one test report for the run: a table with a row per kind of test (backend, frontend, e2e).
//   node .github/scripts/test-summary/index.mjs [results-directory]
// The directory (default ci-results) holds what the test steps of the workflow wrote:
//   backend/*.trx, frontend.json (Vitest), e2e.json (Playwright).
// The report goes to the run page ($GITHUB_STEP_SUMMARY), or to the console when run by hand.
// A missing file is reported as "no results", never as an error: the test steps decide whether the job fails.
import { appendFileSync, existsSync, readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'

import { mergeResults } from './mergeResults.mjs'
import { parsePlaywright } from './parsePlaywright.mjs'
import { parseTrx } from './parseTrx.mjs'
import { parseVitest } from './parseVitest.mjs'
import { renderSummary } from './renderSummary.mjs'

const DEFAULT_RESULTS_DIRECTORY = 'ci-results'

function readBackend(directory) {
  const folder = join(directory, 'backend')
  if (!existsSync(folder)) return null
  const files = readdirSync(folder).filter((name) => name.endsWith('.trx'))
  if (files.length === 0) return null
  // A .trx file starts with a byte order mark, which the parser would take for text.
  return mergeResults(files.map((name) => parseTrx(readFileSync(join(folder, name), 'utf8').replace(/^﻿/, ''))))
}

function readJson(path, parse) {
  return existsSync(path) ? parse(JSON.parse(readFileSync(path, 'utf8'))) : null
}

const directory = process.argv[2] ?? DEFAULT_RESULTS_DIRECTORY
const markdown = renderSummary([
  { title: 'Backend (xUnit)', result: readBackend(directory) },
  { title: 'Frontend (Vitest)', result: readJson(join(directory, 'frontend.json'), parseVitest) },
  { title: 'E2E (Playwright)', result: readJson(join(directory, 'e2e.json'), parsePlaywright) },
])

if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, `${markdown}\n`)
else console.log(markdown)
