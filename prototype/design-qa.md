# Design QA

**Source visual truth**

- `/Users/olivier/Downloads/Sans-titre-2026-06-20-1319.png`
- Source pixels: 13,648 × 8,291.
- Source type: annotated layout board covering Today, À traiter, and request detail states.

**Implementation evidence**

- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-implementation-today.png` — 1,440 × 1,024.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-implementation-detail.png` — 1,440 × 1,206 full-page capture at a 1,440 × 1,024 CSS viewport.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-comparison.png` — normalized composite with the source board scaled to 1,440 px wide, followed by Today and request-detail captures at the same width.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-annotation-comparison.png` — 2,122 × 783 side-by-side comparison of the grid at rest and with pointer hover, captured at the user-comment viewport.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-sidebar-rail.png` — compact rail and top-profile treatment at the 1,061 × 783 user-comment viewport.
- `/Users/olivier/Projects/enterprise-workflow/prototype/qa-sidebar-rail-menu.png` — final inbox state with the relocated user menu expanded.
- Device scale factor: browser default, with source and implementation normalized by common pixel width for comparison.

**State**

- Desktop light theme.
- Today default state.
- Request detail for `REQ-124355` with the Reject action form expanded and the persistent action dock visible.
- Mobile request form checked at a 390 × 844 CSS viewport.

**Full-view comparison evidence**

- The implementation preserves the source shell: vertical navigation rail, top breadcrumb, centered content, contextual cards, user menu in the toolbar, and a persistent action dock in request detail.
- Today preserves the source hierarchy of greeting, immediate work, then recent requests.
- Request detail preserves the source split between request information, contextual action form, and docked decision controls.
- The implementation intentionally adds production-level hierarchy, semantic states, readable copy, and responsive behavior while retaining the annotated layout proportions and interaction model.

**Focused-region comparison evidence**

- Navigation: compact icon-first rail, labels beneath icons, vertically centered menu group, active state, task count, and top-toolbar user menu were checked against the latest annotation.
- Request detail: title, detail panel, action-specific form, and fixed bottom decision dock were checked at readable scale.
- Forms: input labels, focus treatments, required state, button hierarchy, and mobile stacking were checked separately.

**Required fidelity surfaces**

- Fonts and typography: Inter is bundled locally at weights 400, 500, 600, and 700. Headings, labels, body text, status labels, wrapping, and truncation are consistent and readable.
- Spacing and layout rhythm: the compact 96 px desktop rail, 66 px toolbar, centered 790–1,040 px content widths, 16 px panel radii, row separators, and 8 px-derived spacing rhythm match the source structure without overcrowding it.
- Colors and visual tokens: neutral blue-gray canvas, white surfaces, blue interaction accent, amber attention, green success, and red destructive states are consistently mapped. Contrast is sufficient and state is never conveyed by color alone.
- Image quality and asset fidelity: the source contains no photographic or illustrative assets to reproduce. UI icons use the Phosphor icon library; no placeholder imagery or emoji is used.
- Copy and content: French workflow copy is realistic and consistent across Today, inbox, request detail, start form, success, empty, and filtered states.

**Findings**

- No actionable P0, P1, or P2 issue remains.
- No remaining P3 finding from the annotated areas; the two grid states are visually distinguishable without competing with content.

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

**Comparison history**

1. Initial detail capture was taken after the action form had scrolled into view, which obscured the title in the viewport and made the evidence unsuitable for full-state comparison.
2. The page was returned to the top and recaptured at the same 1,440 × 1,024 viewport. Post-fix evidence shows the request title, detail card, progress, action form, and dock in the correct vertical relationship.
3. Browser annotation pass: the 232 px sidebar was judged too wide and the grid invisible at rest. The sidebar was reduced to 196 px with tighter internal spacing. The background was split into a faint always-visible grid and a stronger pointer-localized hover layer. Side-by-side post-fix evidence confirms both states at 1,061 × 783 with no overflow.
4. Sidebar refinement pass: the rail was reduced to 96 px, brand text removed, navigation centered vertically, icon-label anatomy stacked, and the user control moved to the far right of the breadcrumb toolbar. Post-fix evidence confirms the final shell and open user menu at 1,061 × 783.

**Implementation checklist**

- [x] Preserve the annotated navigation shell and breadcrumb.
- [x] Implement centered card content and request lists.
- [x] Implement contextual request actions and persistent bottom dock.
- [x] Implement pointer-revealed grid treatment.
- [x] Use Inter and Tailwind CSS.
- [x] Verify desktop, mobile, core interactions, and console output.
- [x] Apply and verify the compact icon-rail and relocated user menu annotation.

**Follow-up polish**

- Consider replacing the temporary abstract library icon with the final organization or product logo when brand assets exist.

final result: passed
