# Public documentation examples

`LeaveRequest/` is a standalone NuGet-consuming copy of the shipped business sample.
The empty `Directory.*` files prevent inheritance of repository build and package
settings. Copy `LeaveRequest/` outside the repository to use it independently.

```bash
dotnet restore LeaveRequest/LeaveRequest.csproj
ew dev LeaveRequest --environment tutorial
```

The source, Razor view and development configuration are kept equal to
`samples/LeaveRequest.Module/` by `scripts/check-examples.mjs`. This copy supplies
explicit public package references instead of framework project references.
