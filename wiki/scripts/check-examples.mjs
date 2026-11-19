import { readFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import assert from 'node:assert/strict';

const defaultWikiRoot = fileURLToPath(new URL('../', import.meta.url));

// Public examples consume published packages; development samples can target newer APIs.
// Check the reviewed release snapshot instead of requiring equality with working sources.
export async function checkExamples(wikiRoot = defaultWikiRoot) {
  const snapshot = JSON.parse(await readFile(resolve(wikiRoot, 'examples/leave-request.snapshot.json'), 'utf8'));
  const catalog = JSON.parse(await readFile(resolve(wikiRoot, '../release/catalog.json'), 'utf8'));
  assert.equal(snapshot.frameworkVersion, catalog.version, 'Public example framework version differs from the release catalog.');
  for (const required of ['LeaveRequestModule.cs', 'LeaveRequestSummary.razor', 'ew.dev.json', 'LeaveRequest.csproj']) {
    assert(snapshot.files[required], `Public example snapshot is missing ${required}.`);
  }
  for (const [file, expectedHash] of Object.entries(snapshot.files)) {
    assert(/^[\w./-]+$/.test(file) && !file.startsWith('/') && !file.split('/').includes('..'), 'Invalid public example snapshot path.');
    const source = await readFile(resolve(wikiRoot, 'examples/LeaveRequest', file), 'utf8');
    // Git checkouts may use CRLF on Windows; line endings do not change the example.
    const hash = createHash('sha256').update(source.replaceAll('\r\n', '\n')).digest('hex');
    assert.equal(hash, expectedHash, `Public example drift: ${file}. Update the release snapshot only after validating the example against published packages.`);
  }
  const project = await readFile(resolve(wikiRoot, 'examples/LeaveRequest/LeaveRequest.csproj'), 'utf8');
  assert(!project.includes('ProjectReference'), 'Public example must use NuGet, not repository project references.');
  const packages = [...project.matchAll(/<PackageReference\b[^>]*\bVersion="([^"]+)"/g)];
  assert(packages.length > 0 && packages.every(match => match[1] === snapshot.frameworkVersion), 'Example package version drift.');
  const source = await readFile(resolve(wikiRoot, 'examples/LeaveRequest/LeaveRequestModule.cs'), 'utf8');
  const module = source.match(/new WorkflowModuleInfo\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*(\d+)\s*\)/);
  assert(module, 'Public example module metadata was not found.');
  assert.deepEqual({ id: module[1], release: module[2], revision: Number(module[3]) }, snapshot.module, 'Public example module version drift.');
  return `Standalone LeaveRequest ${snapshot.module.release} / revision ${snapshot.module.revision} matches its reviewed snapshot and uses NuGet ${snapshot.frameworkVersion}. Development samples are versioned separately.`;
}

if (process.argv[1] && pathToFileURL(resolve(process.argv[1])).href === import.meta.url) {
  try {
    console.log(await checkExamples());
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    console.error(process.env.GITHUB_ACTIONS === 'true' ? `::error::${message.replaceAll('\n', '%0A').replaceAll('\r', '%0D')}` : message);
    process.exitCode = 1;
  }
}
