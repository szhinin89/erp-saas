/**
 * Global navigator — allows code outside React (e.g. Axios interceptors) to trigger
 * SPA navigation without a full page reload that would clear the in-memory access token.
 *
 * Register the navigate function once inside the Router context (see SessionBootstrap or App).
 * Falls back to window.location.href when the router is not yet mounted.
 */
let _navigate: ((path: string) => void) | null = null;

export function registerGlobalNavigator(fn: (path: string) => void): void {
  _navigate = fn;
}

export function spaNavigate(path: string): void {
  if (_navigate) {
    _navigate(path);
  } else {
    window.location.href = path;
  }
}
