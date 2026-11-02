import { readFileSync, writeFileSync } from 'node:fs'

import { normalizeApiOrigin } from './src/api/apiOrigin.ts'
import { renderSecurityHeaders } from './nginx.headers.ts'

/**
 * Image build step: `node render-nginx-headers.ts <template> <output>`.
 * The API origin comes from VITE_API_ORIGIN, the same variable the app is built with.
 */
const [templatePath, outputPath] = process.argv.slice(2)
if (!templatePath || !outputPath) {
  throw new Error('Usage: node render-nginx-headers.ts <template> <output>')
}

const apiOrigin = normalizeApiOrigin(process.env.VITE_API_ORIGIN)
writeFileSync(outputPath, renderSecurityHeaders(readFileSync(templatePath, 'utf8'), apiOrigin))
