import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
  site: 'https://mbo2olivier.github.io',
  base: '/enterprise-workflow',
  trailingSlash: 'always',
  integrations: [starlight({
    title: 'Enterprise Workflow',
    description: 'Build, integrate, and operate durable workflows with C# and .NET.',
    defaultLocale: 'root',
    locales: { root: { label: 'English', lang: 'en' } },
    social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/mbo2olivier/enterprise-workflow' }],
    editLink: { baseUrl: 'https://github.com/mbo2olivier/enterprise-workflow/edit/main/wiki/' },
    customCss: ['./src/styles/custom.css'],
    sidebar: [
      { label: 'Home', slug: '' },
      { label: 'Getting Started', items: [{ autogenerate: { directory: 'getting-started' } }] },
      { label: 'Basics', items: [{ autogenerate: { directory: 'basics' } }] },
      { label: 'Core Concepts', items: [{ autogenerate: { directory: 'core-concepts' } }] },
      { label: 'Guides', items: [{ autogenerate: { directory: 'guides' } }], collapsed: true },
      { label: 'Tutorials & Examples', items: [{ autogenerate: { directory: 'examples' } }] },
      { label: 'Deployment & Operations', items: [{ autogenerate: { directory: 'operations' } }], collapsed: true },
      { label: 'Advanced Topics', items: [{ autogenerate: { directory: 'advanced-topics' } }], collapsed: true },
      { label: 'Reference', items: [{ autogenerate: { directory: 'reference' } }], collapsed: true },
    ],
  })],
});
