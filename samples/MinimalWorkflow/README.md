# MinimalWorkflow

Cet exemple construit en C# une demande de congé séquentielle avec décision exclusive, valide explicitement le brouillon, puis affiche l’identité et l’empreinte SHA-256 de sa représentation canonique. Il démontre l’auteur/validateur L2 ; il n’exécute pas encore le workflow.

## Prérequis

- SDK .NET `10.0.102` ;
- aucun secret, service ou base de données.

Depuis la racine du dépôt :

```bash
dotnet run --project samples/MinimalWorkflow/MinimalWorkflow.csproj --no-restore
```
