import type { Page } from '@playwright/test'

/** Same key as FAKE_HUB_WINDOW_KEY in src/realtime/fakeHubControls.ts (e2e cannot use the `@` alias). */
const FAKE_HUB_WINDOW_KEY = '__fakeChatHub'

interface FakeHubControls {
  holdSends(): void
  releaseSends(): void
  failNextSends(codes: string[]): void
}

type WindowWithHub = Record<string, FakeHubControls>

/** Drives the in-memory hub of the mock-mode app from a test (the browser holds the real one). */
export function fakeHub(page: Page) {
  return {
    holdSends: () =>
      page.evaluate(
        (key) => (window as unknown as WindowWithHub)[key]!.holdSends(),
        FAKE_HUB_WINDOW_KEY,
      ),
    releaseSends: () =>
      page.evaluate(
        (key) => (window as unknown as WindowWithHub)[key]!.releaseSends(),
        FAKE_HUB_WINDOW_KEY,
      ),
    /** `codes` are hub error codes such as 'rate_limited' or 'unknown'. */
    failNextSends: (codes: string[]) =>
      page.evaluate(
        ([key, codeList]) =>
          (window as unknown as WindowWithHub)[key as string]!.failNextSends(codeList as string[]),
        [FAKE_HUB_WINDOW_KEY, codes] as const,
      ),
  }
}
