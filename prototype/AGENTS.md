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
- The integrated administration reuses the same shell and visual language. Enter it from the top-right user menu; show a clear return to the business application, switch the compact rail to administrative destinations, and preserve the logo, breadcrumb, grid, panels, typography, and interaction tokens.
- Administration is a control center, not the Studio: it covers overview, access, exact workflow-stage grants, operations, audit, and settings. Never expose workflow graph editing, DLL upload, secrets, arbitrary instance-state mutation, or an unrestricted retry action.
- Model stage access explicitly as workflow + definition version + node + action. Support both designated identities and eligible pools, and surface versioned separation-of-actors policies without implying that administrative profiles grant business-task access.
- For L8, this existing prototype is the approved UI source of truth. Reuse its shell, pages, component anatomy, interactions, responsive behavior, and visual tokens when translating the product to Razor/Blazor; do not redesign those surfaces or ship the prototype's simulated data and React runtime as the production application.
- Treat fidelity as a strict acceptance criterion for the production translation: keep the pointer-reactive grid, the exact Phosphor icon semantics and alignment, the top-right profile control with its dropdown (including logout), the connected user’s name in the Today greeting, the two approved Today card anatomies, and the approved administration overview. Framework data may replace simulated content, but must not alter the approved composition.
- Apply the same faint and pointer-reactive grid treatment to the unauthenticated login surface; authentication must not fall back to a static or visually separate background.
- Treat every approved administration detail page as a fixed product composition, not merely a style reference. Access remains a tabbed Profiles / Identities / Providers surface with one list at a time and the creation action at the top right; Operations keeps its four-part health strip, Instances / Outbox / Modules tabs, and one diagnostic table; Settings keeps the three Appearance / Sessions / Runtime panels, their field rhythm, and the detached save action. Real framework data and honest empty states may replace prototype fixtures, but must not introduce dashboard metrics, extra stacked forms, or alternate information architecture.
- In Access, keep the list visible and full-width while profile creation, profile editing, and local identity creation happen in focused modal dialogs. Never embed these editors beside or inside the table.
