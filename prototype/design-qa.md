# Design QA

**Source visual truth**

- `/Users/olivier/Downloads/Sans-titre-2026-06-20-1319.png`
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-sidebar-rail-menu.png` — implemented business shell used as the direct visual target for the integrated administration extension.
- Product concept and interaction constraints from `docs/prd.md`, `docs/architecture/security.md`, `docs/adr/0019-workflow-stages.md`, and `docs/adr/0020-attentes-metier-l6.md`.
- Source pixels: 13,648 × 8,291.
- Source type: annotated layout board covering Today, À traiter, and request detail states.

**Implementation evidence**

- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-implementation-today.png` — 1,440 × 1,024.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-implementation-detail.png` — 1,440 × 1,206 full-page capture at a 1,440 × 1,024 CSS viewport.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-comparison.png` — normalized composite with the source board scaled to 1,440 px wide, followed by Today and request-detail captures at the same width.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-annotation-comparison.png` — 2,122 × 783 side-by-side comparison of the grid at rest and with pointer hover, captured at the user-comment viewport.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-sidebar-rail.png` — compact rail and top-profile treatment at the 1,061 × 783 user-comment viewport.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-sidebar-rail-menu.png` — final inbox state with the relocated user menu expanded.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-admin-overview.png` — administration control center at 1,061 × 783.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-admin-workflow-access.png` — workflow-stage access editor at 1,061 × 783.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-admin-workflow-access-full.png` — full 1,061 × 1,439 workflow-stage access view with exact grants.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-admin-mobile.png` — administration control center at 390 × 844.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-admin-shell-comparison.png` — 2,122 × 783 side-by-side business/admin shell comparison at equal scale.
- Device scale factor: browser default, with source and implementation normalized by common pixel width for comparison.

**State**

- Desktop light theme.
- Today default state.
- Request detail for `REQ-124355` with the Reject action form expanded and the persistent action dock visible.
- Mobile request form checked at a 390 × 844 CSS viewport.
- Administration control center, access tabs, workflow-stage editor, operations tabs, audit filtering, and settings save state.

**Full-view comparison evidence**

- The implementation preserves the source shell: vertical navigation rail, top breadcrumb, centered content, contextual cards, user menu in the toolbar, and a persistent action dock in request detail.
- Today preserves the source hierarchy of greeting, immediate work, then recent requests.
- Request detail preserves the source split between request information, contextual action form, and docked decision controls.
- The implementation intentionally adds production-level hierarchy, semantic states, readable copy, and responsive behavior while retaining the annotated layout proportions and interaction model.
- The administration extension preserves the exact 96 px icon rail, 66 px translucent toolbar, grid canvas, content widths, panel geometry, Inter hierarchy, profile control, and semantic colors of the business application.
- Administrative mode is clearly differentiated through contextual navigation, the `Administration` breadcrumb, and a persistent return-to-application control without introducing a second visual system.

**Focused-region comparison evidence**

- Navigation: compact icon-first rail, labels beneath icons, vertically centered menu group, active state, task count, and top-toolbar user menu were checked against the latest annotation.
- Request detail: title, detail panel, action-specific form, and fixed bottom decision dock were checked at readable scale.
- Forms: input labels, focus treatments, required state, button hierarchy, and mobile stacking were checked separately.
- Workflow access: designated identity and eligible pool choices, exact version/node/action grants, policy revision, and stage disclosure were inspected in the full-page focused capture.
- Administration shell: business and administration states were normalized to the same 1,061 × 783 viewport and combined side by side. The common logo, rail, toolbar, grid, spacing tokens, and profile control align visually.

**Required fidelity surfaces**

- Fonts and typography: Inter is bundled locally at weights 400, 500, 600, and 700. Headings, labels, body text, status labels, wrapping, and truncation are consistent and readable.
- Spacing and layout rhythm: the compact 96 px desktop rail, 66 px toolbar, centered 790–1,040 px content widths, 16 px panel radii, row separators, and 8 px-derived spacing rhythm match the source structure without overcrowding it.
- Colors and visual tokens: neutral blue-gray canvas, white surfaces, blue interaction accent, amber attention, green success, and red destructive states are consistently mapped. Contrast is sufficient and state is never conveyed by color alone.
- Image quality and asset fidelity: the source contains no photographic or illustrative assets to reproduce. UI icons use the Phosphor icon library; no placeholder imagery or emoji is used.
- Copy and content: French workflow copy is realistic and consistent across Today, inbox, request detail, start form, success, empty, and filtered states.

**Findings**

- No actionable P0, P1, or P2 issue remains.
- No remaining P3 finding from the annotated areas; the two grid states are visually distinguishable without competing with content.
- No actionable administration P0, P1, or P2 issue remains. The compact mobile administration rail intentionally scrolls horizontally to retain all six destinations and their labels.

**Interaction and browser verification**

- Tested navigation between Today, Démarrer, À traiter, Mes demandes, request detail, and new-request form.
- Tested inbox text filtering and the Today-only filter.
- Tested opening a request, selecting Validate and Reject actions, revealing their contextual forms, and confirming a decision.
- Tested new-request form navigation and submission success state.
- Tested the user menu.
- Browser console checked: no warnings or errors.
- Desktop viewport checked at 1,440 × 1,024; mobile checked at 390 × 844 with `scrollWidth === innerWidth`.
- Browser-comment viewport checked at 1,061 × 783: sidebar width 96 px, vertically centered column navigation, labels at 10 px below 27 px icons, user profile aligned 30 px from the toolbar's right edge, and no horizontal overflow.
- User menu verified after relocation: `Administration` and `Se déconnecter` are visible from the top-toolbar profile control.
- Administration entry and return-to-application path verified from the user menu and top toolbar.
- Verified all six administrative destinations: overview, access, workflows, operations, audit, and settings.
- Verified access tabs, LDAP/local provider capability views, identity search, profile inspector, workflow selection, stage disclosure, assignment-mode switching, exact-grant save confirmation, operations tabs, audit filtering, security switch, and settings save confirmation.
- Desktop administration verified at 1,061 × 783 with `scrollWidth === innerWidth`, 96 px rail, and 66 px toolbar. Mobile administration verified at 390 × 844 with no document-level overflow; the six-item admin navigation remains independently scrollable.
- Browser console checked after the complete administration flow: no warnings or errors.

**Comparison history**

1. Initial detail capture was taken after the action form had scrolled into view, which obscured the title in the viewport and made the evidence unsuitable for full-state comparison.
2. The page was returned to the top and recaptured at the same 1,440 × 1,024 viewport. Post-fix evidence shows the request title, detail card, progress, action form, and dock in the correct vertical relationship.
3. Browser annotation pass: the 232 px sidebar was judged too wide and the grid invisible at rest. The sidebar was reduced to 196 px with tighter internal spacing. The background was split into a faint always-visible grid and a stronger pointer-localized hover layer. Side-by-side post-fix evidence confirms both states at 1,061 × 783 with no overflow.
4. Sidebar refinement pass: the rail was reduced to 96 px, brand text removed, navigation centered vertically, icon-label anatomy stacked, and the user control moved to the far right of the breadcrumb toolbar. Post-fix evidence confirms the final shell and open user menu at 1,061 × 783.
5. Administration extension pass: the existing business shell was treated as the direct visual source. The new control center and stage-access editor reuse the same typography, tokens, grid, rail, toolbar, cards, controls, and responsive rules. Equal-scale comparison and focused full-page evidence found no P0/P1/P2 drift; no corrective visual iteration was required.

**Implementation checklist**

- [x] Preserve the annotated navigation shell and breadcrumb.
- [x] Implement centered card content and request lists.
- [x] Implement contextual request actions and persistent bottom dock.
- [x] Implement pointer-revealed grid treatment.
- [x] Use Inter and Tailwind CSS.
- [x] Verify desktop, mobile, core interactions, and console output.
- [x] Apply and verify the compact icon-rail and relocated user menu annotation.
- [x] Implement and verify the integrated administration shell and six destinations.
- [x] Represent R2-A assignment modes, R3-A exact grants, and D6-A actor-separation policy.
- [x] Verify administration desktop/mobile states and primary interactions.

**Follow-up polish**

- Consider replacing the temporary abstract library icon with the final organization or product logo when brand assets exist.
- Consider grouping the two least-used mobile administration destinations under a `Plus` item if future usage testing shows the horizontally scrollable six-item rail is hard to discover.

final result: passed
