import type { HubErrorCode } from './hubContract'

/** Where the mock-mode app publishes the fake hub's controls, so e2e tests can drive the hub. */
export const FAKE_HUB_WINDOW_KEY = '__fakeChatHub'

/** What a browser test may do to the fake hub: slow its sends down or make them fail. */
export interface FakeHubControls {
  /** Sends stay pending until `releaseSends`. */
  holdSends(): void
  releaseSends(): void
  /** The next sends fail with these codes, one per call, in order. */
  failNextSends(codes: HubErrorCode[]): void
}

export function exposeFakeHubControls(controls: FakeHubControls): void {
  ;(window as unknown as Record<string, FakeHubControls>)[FAKE_HUB_WINDOW_KEY] = controls
}
