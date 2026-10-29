// The import makes this file a module, so the block below augments vue-router instead of replacing it.
import 'vue-router'

declare module 'vue-router' {
  interface RouteMeta {
    /** Reachable without a session. Every other route requires sign-in. */
    public?: boolean
  }
}
