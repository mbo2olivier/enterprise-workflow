# MinimalWorkflow

Cet exemple vérifie que le SDK et ses dépendances transitives peuvent être consommés par une application .NET 10. Le modèle canonique et la DSL seront ajoutés au lot L2 ; l’exemple ne simule donc pas encore un workflow.

## Prérequis

- SDK .NET `10.0.102` ;
- aucun secret, service ou base de données.

Depuis la racine du dépôt :

```bash
dotnet run --project samples/MinimalWorkflow/MinimalWorkflow.csproj --no-restore
```

