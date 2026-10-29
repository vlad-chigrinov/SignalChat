import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

import { describe, expect, it } from 'vitest'

const SOURCE_ROOT = fileURLToPath(new URL('..', import.meta.url))
/** Attribute use only: a comment that merely names `v-html` does not match. */
const RAW_HTML_ATTRIBUTE = /v-html\s*=/

function sourceFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((entry) => {
    const path = join(directory, entry)
    if (statSync(path).isDirectory()) return sourceFiles(path)
    return /\.(vue|ts)$/.test(entry) ? [path] : []
  })
}

describe('user and room text', () => {
  it('is shown as plain text: no source file binds raw HTML with v-html', () => {
    const offenders = sourceFiles(SOURCE_ROOT).filter((file) =>
      RAW_HTML_ATTRIBUTE.test(readFileSync(file, 'utf8')),
    )

    expect(offenders).toEqual([])
  })
})
