import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdtemp, mkdir, readFile, writeFile, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { checkExamples } from './check-examples.mjs';

async function fixture(t) {
  const root = await mkdtemp(join(tmpdir(), 'ew-doc-example-'));
  t.after(() => rm(root, { recursive: true, force: true }));
  const wiki = join(root, 'wiki');
  const example = join(wiki, 'examples/LeaveRequest');
  await mkdir(example, { recursive: true });
  await mkdir(join(root, 'release'));
  const snapshot = JSON.parse(await readFile(new URL('../examples/leave-request.snapshot.json', import.meta.url), 'utf8'));
  for (const file of Object.keys(snapshot.files)) {
    await writeFile(join(example, file), await readFile(new URL(`../examples/LeaveRequest/${file}`, import.meta.url)));
  }
  await writeFile(join(root, 'release/catalog.json'), JSON.stringify({ version: snapshot.frameworkVersion }));
  const saveSnapshot = () => writeFile(join(wiki, 'examples/leave-request.snapshot.json'), JSON.stringify(snapshot));
  await saveSnapshot();
  async function replace(file, transform) {
    const content = transform(await readFile(join(example, file), 'utf8'));
    await writeFile(join(example, file), content);
    snapshot.files[file] = createHash('sha256').update(content.replaceAll('\r\n', '\n')).digest('hex');
    await saveSnapshot();
  }
  return { wiki, example, replace };
}

test('published example is independent of development sample sources', async t => {
  const { wiki } = await fixture(t);
  assert.match(await checkExamples(wiki), /NuGet 1\.0\.0-rc\.1/);
});

test('unreviewed source drift remains an error', async t => {
  const { wiki, example } = await fixture(t);
  await writeFile(join(example, 'LeaveRequestSummary.razor'), 'unreviewed change');
  await assert.rejects(checkExamples(wiki), /Public example drift: LeaveRequestSummary\.razor/);
});

test('updating a hash does not bypass the published package version check', async t => {
  const { wiki, replace } = await fixture(t);
  await replace('LeaveRequest.csproj', source => source.replaceAll('Version="1.0.0-rc.1"', 'Version="9.0.0"'));
  await assert.rejects(checkExamples(wiki), /Example package version drift/);
});

test('updating a hash does not bypass module metadata checks', async t => {
  const { wiki, replace } = await fixture(t);
  await replace('LeaveRequestModule.cs', source => source.replace('"1.8.0", 9', '"1.9.0", 10'));
  await assert.rejects(checkExamples(wiki), /Public example module version drift/);
});

test('snapshot checks accept Windows line endings', async t => {
  const { wiki, example } = await fixture(t);
  const file = join(example, 'LeaveRequestModule.cs');
  await writeFile(file, (await readFile(file, 'utf8')).replaceAll('\r\n', '\n').replaceAll('\n', '\r\n'));
  assert.match(await checkExamples(wiki), /matches its reviewed snapshot/);
});
