# Checkout frontend

Next.js 16 (App Router) + React 19 + Tailwind 4 + TanStack Query. See the
[root README](../README.md) for how to run the whole stack and how the pages work.

```bash
pnpm install
pnpm dev          # http://localhost:3000, expects the API at NEXT_PUBLIC_API_URL (default http://localhost:8080)
pnpm test         # Vitest + Testing Library
pnpm lint && pnpm typecheck
```
