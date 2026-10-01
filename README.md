# Enterprise Workflow

Framework de workflows réutilisable en C#/.NET 10 : moteur durable, SDK, application d’accueil modulaire et, dans une livraison suivante, Studio pour développeurs.

**Statut : lots L1 et L2 implémentés.** Le dépôt contient le socle .NET 10, le modèle canonique, le validateur, la DSL C#, les contrats atomiques de persistance, leurs tests et un exemple de workflow. Aucun adaptateur SQL ni package n’est encore publié. La licence open source reste à choisir avant publication.

Le produit vise Windows, Linux et macOS, avec SQLite et Oracle dans le MVP. Chaque organisation possède sa propre installation. Le support sera qualifié par combinaison OS, architecture CPU et adaptateur.

Le MVP suit un graphe séquentiel à décisions exclusives, sans boucles ni parallélisme. Le projet sera hébergé sur GitHub et utilisera GitHub Actions pour la première CI.

## Compiler et vérifier le socle

`global.json` fixe la ligne minimale au SDK `10.0.100` et autorise les feature bands stables ultérieures de .NET 10 ; le SDK local `10.0.102` est donc accepté. Depuis la racine :

```bash
dotnet restore EnterpriseWorkflow.slnx --locked-mode
dotnet build EnterpriseWorkflow.slnx --configuration Release --no-restore
dotnet test --solution EnterpriseWorkflow.slnx --configuration Release --no-build --no-restore
dotnet run --project samples/MinimalWorkflow/MinimalWorkflow.csproj --configuration Release --no-build --no-restore
```

Les versions NuGet sont centralisées et chaque projet possède un fichier de verrouillage. `NuGet.Config` isole la restauration du dépôt sur nuget.org afin de ne pas dépendre de flux privés configurés sur une machine de développement.

## Parcours de lecture

1. [Cadrage et décisions à clarifier](docs/cadrage.md)
2. [PRD](docs/prd.md)
3. [Architecture](docs/architecture/overview.md)
4. [ADR](docs/adr/README.md)
5. [Plan d’implémentation](docs/implementation-plan.md)
6. [Prérequis et arbitrages pour L2](docs/l2-readiness.md)
7. [Preuve du lot L1](docs/evidence/l1.md)
8. [Preuve du lot L2](docs/evidence/l2.md)

## Documents de référence

- [Modèle et exécution durable](docs/architecture/runtime.md)
- [Persistance SQLite et Oracle](docs/architecture/persistence.md)
- [Sécurité et administration](docs/architecture/security.md)
- [Modules, SDK, formulaires et Studio](docs/architecture/extensions.md)
- [Compatibilité et distribution](docs/compatibilite.md)
- [Stratégie de tests](docs/testing.md)
- [Exploitation](docs/operations.md)
- [Traçabilité](docs/traceability.md)
- [Conventions proposées](docs/contributing.md)
- [Glossaire](docs/glossary.md)

Les décisions **acceptées** proviennent des réponses explicites du porteur. Les mécanismes **proposés** restent à valider ; les points **en attente** ne sont pas résolus par défaut.
