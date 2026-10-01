# Conventions de réalisation proposées

Documentation et échanges de cadrage en français ; code, identifiants et commentaires techniques en anglais. Préfixe de travail EnterpriseWorkflow, non réservé publiquement.

## Code

.NET 10, version C# stable correspondante, nullable et analyseurs activés. SDK et packages centralisés/épinglés après sélection au lot L1. Pas de preview implicite ni de compatibilité .NET Framework/Standard sans consommateur identifié.

API asynchrones suffixées Async ; CancellationToken propagé ; pas d’async void ou de fire-and-forget pour du travail durable. Scopes DI courts ; aucun service scoped capturé par singleton. TimeProvider injectable ; dates persistées selon un contrat UTC explicite. DTO/JSON versionnés, erreurs structurées, pas de désérialisation arbitraire de types CLR.

Composition plutôt que hiérarchies profondes. Les contrats publics documentent garanties, idempotence, limites et versionnement. Ne pas imposer CQRS, MediatR, event sourcing ou repository générique sans besoin démontré.

## CI

GitHub héberge le projet ; GitHub Actions est retenu pour cette version. Les commandes reproductibles de compilation et de test restent utilisables hors Actions pour faciliter une future adaptation à GitLab. Aucun pipeline GitLab n’est requis au premier lot.

## Changements et tests

Chaque lot fournit code compilable, tests adaptés aux risques et documentation synchronisée. Toute évolution du modèle durable inclut migration et compatibilité ; toute garantie de concurrence inclut preuve sur SQLite et Oracle. Les tests simplement non exécutés sont déclarés comme tels.

Suivre les frontières de l’architecture et les ADR acceptés. Proposer un nouvel ADR lorsqu’une décision change ; une mention dans le code ne remplace pas la décision. Les résultats de prototypes sont conservés avec environnement et limites.

## Dépôt et publication

Aucune licence n’est choisie par ce document. Avant publication : décision de licence, notices des dépendances, identifiants NuGet/CLI et règles de contribution. Aucun CLA, DCO ou processus de gouvernance n’est imposé implicitement.

Le futur README de chaque exemple précisera prérequis, commandes, données de démonstration et absence de secrets. L’outillage de build JavaScript éventuel reste distinct d’une dépendance Node.js d’exécution.
