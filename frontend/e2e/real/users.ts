import { expect, type Browser, type BrowserContext, type Page } from '@playwright/test'

import { REAL_BASE_URL, TEST_LOGIN_PATH } from './constants'

const SUFFIX_RADIX = 36
const SUFFIX_RANDOM_START = 2
const SUFFIX_RANDOM_END = 8

/** Unique per run, so leftovers in the database never collide with a new run. */
export function uniqueSuffix(): string {
  const random = Math.random().toString(SUFFIX_RADIX).slice(SUFFIX_RANDOM_START, SUFFIX_RANDOM_END)
  return `${Date.now().toString(SUFFIX_RADIX)}${random}`
}

export interface RealUser<Extra = undefined> {
  /** Test handle; the backend also uses it as the display name. */
  handle: string
  context: BrowserContext
  page: Page
  /** Whatever `prepare` returned for this page, e.g. a socket gate. */
  extra: Extra
}

/**
 * Signs the context in through the test sign-in (the cookie stays in the context), then opens
 * the app. `prepare` runs before the first navigation, so it can install page routes in time.
 */
export async function startUser<Extra = undefined>(
  browser: Browser,
  name: string,
  prepare?: (page: Page) => Extra,
): Promise<RealUser<Extra>> {
  const handle = `${name}-${uniqueSuffix()}`
  const context = await browser.newContext({ baseURL: REAL_BASE_URL })
  await signInAs(context, handle)
  const page = await context.newPage()
  const extra = prepare ? prepare(page) : (undefined as Extra)
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'Комнаты' })).toBeVisible()
  return { handle, context, page, extra }
}

export async function signInAs(context: BrowserContext, handle: string): Promise<void> {
  const response = await context.request.post(TEST_LOGIN_PATH, { data: { handle } })
  expect(response.ok(), `test sign-in as ${handle} failed: ${response.status()}`).toBeTruthy()
}
