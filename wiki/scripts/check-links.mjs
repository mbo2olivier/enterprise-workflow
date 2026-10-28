import { readdir, readFile, stat } from 'node:fs/promises';
import { resolve, join, extname } from 'node:path';

const dist = resolve('dist');
const base = '/enterprise-workflow';
const origin = 'https://mbo2olivier.github.io';
const pages = [];
async function walk(dir) {
  for (const item of await readdir(dir, { withFileTypes: true })) {
    const path = join(dir, item.name);
    if (item.isDirectory()) await walk(path);
    else if (extname(path) === '.html') pages.push(path);
  }
}
await walk(dist);
const content = new Map(await Promise.all(pages.map(async path => [path, await readFile(path, 'utf8')])));
const errors = [];
let checked = 0;
for (const [file, html] of content) {
  const relative = file.slice(dist.length).replaceAll('\\', '/').replace(/index\.html$/, '');
  const current = new URL(base + relative, origin);
  for (const match of html.matchAll(/\b(?:href|src)=["']([^"']*)["']/g)) {
    const raw = match[1].replaceAll('&amp;', '&');
    if (!raw || /^(?:data:|mailto:|tel:|javascript:)/i.test(raw)) continue;
    const url = new URL(raw, current);
    if (url.origin !== origin) continue;
    if (url.pathname !== base && !url.pathname.startsWith(base + '/')) {
      errors.push(`${relative}: escapes GitHub Pages base: ${raw}`); continue;
    }
    const targetPath = decodeURIComponent(url.pathname.slice(base.length));
    let target = resolve(dist, '.' + (targetPath || '/'));
    // Astro's 404 canonical route is served by GitHub Pages via 404.html.
    if (targetPath === '/404/') target = join(dist, '404.html');
    if (!target.startsWith(dist + '/') && target !== dist) { errors.push(`${relative}: invalid path ${raw}`); continue; }
    try {
      if ((await stat(target)).isDirectory()) target = join(target, 'index.html');
      await stat(target);
      if (url.hash && extname(target) === '.html') {
        const targetHtml = content.get(target) ?? await readFile(target, 'utf8');
        const id = decodeURIComponent(url.hash.slice(1));
        const ids = [...targetHtml.matchAll(/\bid=["']([^"']*)["']/g)].map(m => m[1]);
        if (id && !ids.includes(id)) errors.push(`${relative}: missing fragment ${raw}`);
      }
      checked++;
    } catch { errors.push(`${relative}: missing file ${raw}`); }
  }
}
// The public README must point to generated wiki routes, not internal specifications.
const readme = await readFile('../README.md', 'utf8');
if (/\]\([^)]*docs\//.test(readme)) errors.push('README links to internal docs/');
for (const match of readme.matchAll(/https:\/\/mbo2olivier\.github\.io\/enterprise-workflow\/[^)\s]*/g)) {
  const path = new URL(match[0]).pathname.slice(base.length);
  try { await stat(join(dist, path, 'index.html')); checked++; }
  catch { errors.push(`README: missing wiki route ${match[0]}`); }
}
if (errors.length) throw new Error(errors.join('\n'));
console.log(`Checked ${checked} local links/resources across ${pages.length} HTML pages, including fragments and Pages base.`);
