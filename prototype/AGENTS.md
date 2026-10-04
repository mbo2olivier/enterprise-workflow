# Prototype Instructions

Run the local server yourself and open the preview in the browser available to this environment. Do not give the user server-start instructions when you can run it.

Before making substantial visual changes, use the Product Design plugin's `get-context` skill when the visual source is unclear or no longer matches the current goal. When the user gives durable prototype-specific design feedback, preferences, or decisions, record them in `AGENTS.md`.

When implementing from a selected generated mock, treat that image as the source of truth for layout, component anatomy, density, spacing, color, typography, visible content, and hierarchy.

Build app UI in `src/`. Keep `.openai/hosting.json`, `worker/index.js`, `scripts/prepare-sites-build.mjs`, and `tests/sites-worker.test.mjs` intact so the same local prototype can be handed to Sites. Before a Sites handoff, run `npm run build` and `npm run test:sites`; the build must leave `dist/client/index.html`, `dist/server/index.js`, and `dist/.openai/hosting.json`.

## Product-specific design direction

- Use Tailwind CSS and Inter for the prototype UI.
- Preserve the user's shell: compact vertical navigation on desktop, breadcrumb toolbar, centered content panels, and a user menu at the far right of the top toolbar.
- The workspace background uses a subtle grid that is always faintly visible; pointer movement locally strengthens the gray grid around the hovered area.
- Keep the desktop sidebar as a compact icon rail (about 96 px): logo only at the top, navigation vertically centered, large icons with small labels underneath, and no user profile in the rail.
- The request detail keeps a persistent bottom action dock. Choosing an action reveals its contextual form immediately below the request details; unusually long actions may use a dialog instead.
- Keep the prototype suitable for later translation into reusable Blazor/Razor components; avoid coupling the visual system to React-specific behavior.
