import { readFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
for (const file of ['LeaveRequestModule.cs', 'LeaveRequestSummary.razor', 'ew.dev.json']) {
  assert.equal(await readFile(`examples/LeaveRequest/${file}`, 'utf8'),
    await readFile(`../samples/LeaveRequest.Module/${file}`, 'utf8'), `Documentation sample drift: ${file}`);
}
const project = await readFile('examples/LeaveRequest/LeaveRequest.csproj', 'utf8');
assert(!project.includes('ProjectReference'), 'Public example must use NuGet, not repository project references.');
const catalog = JSON.parse(await readFile('../release/catalog.json', 'utf8'));
assert([...project.matchAll(/Version="([^"]+)"/g)].every(match => match[1] === catalog.version), 'Example package version drift.');
console.log('Standalone LeaveRequest matches the shipped sample and references the release NuGet version.');
