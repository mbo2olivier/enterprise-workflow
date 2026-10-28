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

## Required setup before the first deployment

A repository administrator or maintainer must configure GitHub Pages **before
running the deployment workflow**:

1. Open [repository Pages settings](https://github.com/mbo2olivier/enterprise-workflow/settings/pages).
2. Under **Build and deployment → Source**, select **GitHub Actions**.
3. Return to **Actions → documentation** and re-run the failed jobs, or choose
   **Run workflow** on `main`.

Use the custom Actions source, not a branch folder. In particular, do not select
`docs/`: that directory contains internal maintainer/coding-agent material.
There is no need to create a separate GitHub starter workflow; the repository
already includes `.github/workflows/documentation.yml`.

The workflow checks this prerequisite in `pages-configuration`, builds pull
requests independently of Pages settings, and deploys `main` only after Pages
configuration, build and public examples pass. A missing site or incorrect
publishing source produces a direct settings link and recovery instructions.
The normal workflow token only checks the existing site; it does not enable
Pages automatically or require a personal access token.

The configured public URL is `https://mbo2olivier.github.io/enterprise-workflow/`;
no custom domain is configured. A successful build alone is not evidence of a
deployed site. After deployment, verify the home page, LeaveRequest tutorial,
search and assets at that URL.

The root `docs/` directory is retained for maintainer/coding-agent specifications,
ADRs, plans and evidence. The website build does not import it. Build output is
only `wiki/dist/`; never upload the repository root as a Pages artifact.
