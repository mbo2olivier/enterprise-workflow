# Organization security adapters

Compilable source for the wiki organization tutorial (source checkout track).

```sh
dotnet build wiki/examples/OrganizationSecurity/OrganizationSecurity.csproj -c Release
```

This is a library, not a runnable login server. Move these adapters to the
organization's Security project and implement both gateway interfaces against
its actual identity and durable challenge services. Register them through
`AddOrganizationSecurity` before kernel `BuildAsync`. Providers are singletons;
gateways must be thread-safe and must not capture scoped services.

The adapters delegate security decisions: compiling them does not establish
password validation, challenge expiry, replay protection or bank integration.
The tutorial provides a gateway acceptance matrix. No usable OTP, password,
remote endpoint or credential is embedded. `ew dev` does not use these providers.
