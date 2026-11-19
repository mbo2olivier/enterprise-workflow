# Public documentation examples

`LeaveRequest/` is a standalone NuGet-consuming copy of the shipped business sample.
The empty `Directory.*` files prevent inheritance of repository build and package
settings. Copy `LeaveRequest/` outside the repository to use it independently.

```bash
dotnet restore LeaveRequest/LeaveRequest.csproj
ew dev LeaveRequest --environment tutorial
```

The example is pinned to the published framework version and its reviewed source
snapshot in `leave-request.snapshot.json`. `scripts/check-examples.mjs` checks the
snapshot hashes, module metadata, NuGet versions and absence of project references.
It intentionally does not compare with `samples/LeaveRequest.Module/`, which may
already use unpublished SDK APIs (for example L11 localization).

Do not copy the development sample into this directory until the required packages
are published. When intentionally upgrading the public example, qualify it with the
published packages, update the snapshot and walkthrough together, and update the
module artifact version verified by the documentation workflow. The separate
`public-examples` CI job continues to compile, pack and verify the standalone example
against published packages. A snapshot update alone is not compatibility evidence.
