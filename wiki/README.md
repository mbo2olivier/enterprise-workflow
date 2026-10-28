# Enterprise Workflow documentation

Public English documentation, built independently from the .NET solution with Astro/Starlight.

```bash
npm ci
npm run check
npm run build
npm run dev
```

Use Node.js 22.23.4 or newer (CI uses Node 24). Preview the production build with
`npm run preview`, at `/enterprise-workflow/`. Build includes a local link,
fragment and asset check with the GitHub Pages base path.

Pages are under `src/content/docs/`; the sidebar follows the approved reading
order with page frontmatter `sidebar.order`. Keep tutorial paths and snippets
aligned with the published framework release. `examples/LeaveRequest` consumes
public NuGet packages; `node scripts/check-examples.mjs` checks its source parity.

The dedicated `documentation.yml` workflow builds pull requests and deploys main
to GitHub Pages. Configure repository Pages to use **GitHub Actions**. The public
URL is `https://mbo2olivier.github.io/enterprise-workflow/`; no custom domain is
configured. A successful build alone is not evidence of a deployed site.

The root `docs/` directory is retained for maintainer/coding-agent specifications,
ADRs, plans and evidence. The website build does not import it. Build output is
only `wiki/dist/`; never upload the repository root as a Pages artifact.
