"use client";

import { useCallback, useEffect, useRef } from "react";

/**
 * The Idempotency-Key for this checkout: one UUID per purchase, created in the browser when the
 * page loads (never during server prerendering, where it would be shared by every visitor).
 *
 * Every retry of the same purchase reuses it, so the API returns the original result instead of
 * charging twice. `renew` starts a new purchase, e.g. after the selection changed.
 */
export function useIdempotencyKey() {
  const key = useRef<string | null>(null);

  useEffect(() => {
    key.current ??= crypto.randomUUID();
  }, []);

  const current = useCallback(() => (key.current ??= crypto.randomUUID()), []);
  const renew = useCallback(() => {
    key.current = crypto.randomUUID();
  }, []);

  return { current, renew };
}
