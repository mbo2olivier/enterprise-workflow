# Preuve du lot L4a — binding runtime

Date : 1er octobre 2026.

Environnement : macOS 26.3 ARM64, SDK .NET `10.0.401`, runtime .NET `10.0.12` et Microsoft.Extensions.DependencyInjection `10.0.12`.

## Décisions appliquées

L’[ADR 0014](../adr/0014-binding-handlers-l4a.md) consigne les choix 1A, 2A et 3A : clé globale `(HandlerId, HandlerVersion)`, résultats bornés sans choix de transition par le handler, registre immuable et validation obligatoire avant publication.

## Livrables

- `EnterpriseWorkflow.Runtime.Abstractions` : `NodeExecutionContext`, interfaces distinctes `IServiceNodeHandler` / `IDecisionNodeHandler`, résultats succès/retryable/permanent et mise à jour d’état facultative ;
- `EnterpriseWorkflow.Runtime` : builder et registre immuable, diagnostics `EW4001` à `EW4005`, validateur de définition, intégration `IServiceCollection` et résolution exacte dans un scope DI court ;
- `EnterpriseWorkflow.Runtime.Tests` : dix tests unitaires et d’intégration DI sans worker ni base ;
- frontières de projets vérifiées : les contrats n’importent pas DI et seul Runtime référence `Microsoft.Extensions.DependencyInjection.Abstractions`.

## Scénarios acquis

- collision de clé globale refusée pendant la composition, avant toute modification de `IServiceCollection` ;
- ordre ordinal, versions distinctes et sensibilité à la casse ;
- clé absente et rôle Service/Decision incompatible empêchant la publication ;
- définition entièrement liée acceptée ;
- résolution exacte sans fallback de version ou de rôle ;
- scope et dépendances distincts par tentative, avec disposition vérifiée ;
- contexte rejetant identités vides et tentative non positive ;
- résultats rejetant issue ou code technique vide ;
- handler incapable de choisir destination, transaction ou délai de retry via le contrat.

## Commandes et résultats

```bash
dotnet restore EnterpriseWorkflow.slnx --locked-mode
dotnet build EnterpriseWorkflow.slnx --configuration Release --no-restore
dotnet test --solution EnterpriseWorkflow.slnx --configuration Release --no-build --no-restore
```

Résultat exécuté : build avec **0 avertissement et 0 erreur**, puis **59 tests réussis, 0 échec, 0 ignoré**.

## Limites

L4a n’exécute pas encore de workflow. Le chargement de l’état depuis le store, l’interprétation des transitions, la validation d’une issue contre les branches, les valeurs de polling/bail/retry, le renouvellement de bail, le commit et l’outbox appartiennent à L4b. L7 remplacera l’enregistrement direct par l’alimentation depuis des manifestes sans modifier la sémantique de clé ni autoriser l’écrasement.
