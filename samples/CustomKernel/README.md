# Custom kernel composition

This example uses the hosting SDK and registers a compiled second-factor handler. The demonstration handler deliberately returns **Unavailable**; it never accepts a hard-coded code or simulates a production identity provider. Second authentication defaults to disabled.

```sh
dotnet run --project samples/CustomKernel -- --Database:MigrationMode=Apply
```

`Apply` is an explicit choice for a disposable development database. Use a dedicated connection string and media root; normal deployments validate schemas. Add `--Security:SecondFactor:Enabled=true` to exercise the unavailable-handler path. To use a real mechanism, replace the handler with the organization's challenge service and store; it must bind identity/attempt, expire challenges, bound attempts and atomically consume successful proofs across all host instances.

The default kernel has no handler. `ew dev` does not run this custom kernel and continues using only its JSON test identities.
