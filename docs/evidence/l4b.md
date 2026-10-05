# Preuve du lot L4b — worker durable et outbox

Date d’exécution : 3 octobre 2026. Environnement local : macOS ARM64, SDK .NET `10.0.401`, SQLite fichier réel et image officielle `container-registry.oracle.com/database/enterprise:19.19.0.0` ARM64.

## Décisions mises en œuvre

L’ADR 0015 consigne les choix acceptés 1A à 5A et 6A : snapshot atomique au claim, configuration complète des paramètres, exceptions inattendues permanentes, intentions d’effets atomiques, conservation de version d’état et dead letter après erreur permanente ou tentatives épuisées. Le plan contient G1a pour attente/callback et G3-M (anciennement G3a) pour les migrations de schéma d’état.

## Livrables vérifiés

- `ClaimDueWorkAsync` retourne définition publiée revalidée, état, tentative et bail fenced dans `ClaimedWork` ;
- worker hébergé à concurrence configurable, renouvellement de bail, timeout, retry exponentiel avec full jitter et plafond ;
- interprétation des nœuds Start, Service, Decision et End exclusivement via le registre L4a ;
- remplacement d’état conservant le `SchemaVersion` courant ;
- `ExternalEffectIntent` bornée à 256 KiB, commit atomique avec la transition et clé d’idempotence stable ;
- dispatcher outbox à bail fenced, redelivery, retry configurable, erreur permanente et statut `Failed` après dix tentatives par défaut ;
- migrations `202610030002_AddOutbox` pour SQLite et `202610030003_AddOutbox` pour Oracle ;
- sample `ExecutableWorkflow` réellement exécutable sur SQLite.

## Commandes et résultats

```bash
dotnet restore EnterpriseWorkflow.slnx --force-evaluate
dotnet build EnterpriseWorkflow.slnx --no-restore --disable-build-servers
dotnet test --solution EnterpriseWorkflow.slnx --no-build --no-restore --minimum-expected-tests 62
dotnet run --project samples/ExecutableWorkflow/ExecutableWorkflow.csproj --no-build --no-restore
```

Résultat : build avec **0 avertissement et 0 erreur**, puis **62 tests réussis, 0 échec, 0 ignoré**. Le sample termine `Start → Service → End`, persiste l’état et affiche la livraison de l’intention avec sa clé stable.

Qualification Oracle officielle :

```bash
docker compose -f tests/oracle/docker-compose.yml -f tests/oracle/docker-compose.19c.yml up -d --wait
dotnet restore tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --locked-mode
dotnet build tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --no-restore
dotnet test --project tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --no-build --no-restore --minimum-expected-tests 8
```

La chaîne Oracle est injectée par `ENTERPRISE_WORKFLOW_ORACLE_CONNECTION_STRING` et n’est pas stockée. Résultat : **8 tests réussis, 0 échec, 0 ignoré** sur Oracle Enterprise `19.19.0.0.0`. Ils incluent migration, démarrage idempotent, claims concurrents, reprise/fencing, annulation, atomicité outbox, fencing de livraison et parcours runtime complet avec effet livré. La stack Oracle reste saine et démarrée après qualification.

## Incidents injectés

Le test runtime abandonne un bail outbox après que le faux destinataire a appliqué l’effet, sans acquittement du store. Après expiration, une nouvelle génération redélivre la même clé ; le destinataire ne l’applique qu’une fois et l’ancien acquittement est refusé. Les tests de store abandonnent également un bail métier, rouvrent le store et vérifient la reprise ainsi que le rejet de l’ancien token.

## Limites restantes

- l’injection d’incident reproduit les frontières durables et la réouverture du store, mais ne tue pas encore un processus Host par le système d’exploitation ; cette variante reste requise dans la qualification de déploiement ;
- le dispatcher ne renouvelle pas son bail pendant un appel externe : l’entreprise doit dimensionner `OutboxLeaseDuration` au-dessus du timeout de son transport ; une livraison longue peut être redélivrée et doit rester idempotente ;
- aucun mécanisme de rejeu administratif d’un message `Failed` n’est livré ; il devra être explicite, autorisé et audité ;
- l’outbox ne modélise pas une réponse distante nécessaire à la suite du workflow ; décision reportée à G1a ;
- aucune migration de `WorkflowState.SchemaVersion` n’est implicite ; décision ensuite franchie par G3-M/M1-A en L7b.
