# Preuve du lot L3a — SQLite

Date : 1er octobre 2026. Commit : changements de travail non encore commités au moment de cette preuve.

## Livrables exécutés

- projet `EnterpriseWorkflow.Persistence.Sqlite` sur EF Core SQLite 10.0.12, sans dépendance EF dans Core ni dans les contrats ;
- `SqliteWorkflowDbContext` dédié et extension de mapping intégrable à un modèle EF applicatif ;
- migration initiale explicite, réexécutable sans modification destructive ;
- schéma physique avec définitions immuables, instances, activations, travaux, reçus de démarrage et audit ;
- conversions explicites : UTC en millisecondes Unix, JSON canonique en texte, révisions entières et identifiants ordinaux en collation `BINARY` ;
- publication immuable, démarrage atomique/idempotent, claim, renouvellement, commit conditionnel, retry, annulation et audit atomique ;
- transaction SQLite d’écriture immédiate et horloge SQL commune au claim, au renouvellement et au commit ;
- fencing par génération, token et expiration stricte ; reprise d’un travail expiré avec génération avancée ;
- cinq tests d’intégration sur fichiers SQLite réels, sans provider mémoire.

## Commandes et résultats locaux

Environnement : macOS 26.3 ARM64, SDK .NET `10.0.401`, runtime .NET `10.0.12`, EF Core SQLite `10.0.12`.

```bash
dotnet restore EnterpriseWorkflow.slnx --locked-mode
dotnet build EnterpriseWorkflow.slnx --configuration Release --no-restore --disable-build-servers --maxcpucount:1
dotnet test --solution EnterpriseWorkflow.slnx --configuration Release --no-build --no-restore --max-parallel-test-modules 1
dotnet run --project samples/MinimalWorkflow/MinimalWorkflow.csproj --configuration Release --no-build --no-restore
```

Résultats : build réussi avec zéro avertissement et zéro erreur ; 47 tests réussis, 0 échec, 0 ignoré. Les cinq tests L3a couvrent migration répétée, publication et démarrage idempotents après réouverture, conflit de reçu, deux claims concurrents, expiration/reclaim, refus du token périmé, reprise jusqu’à `Completed`, annulation et absence d’écriture partielle sur conflit de révision.

## Correspondance avec la stratégie

- T03 SQLite : acquis pour démarrage idempotent et contenu conflictuel sous la même clé ;
- T04 SQLite : acquis avec deux connexions sur le même fichier, un seul propriétaire valide et génération avancée après expiration ;
- T05 SQLite : acquis pour expiration, fencing de l’ancien propriétaire, conflit de révision sans mutation partielle et annulation concurrente séquentiellement prouvée ;
- T13 SQLite : création à vide et application idempotente de la migration initiale acquises.

## Limites explicites

Oracle 19c n’est pas qualifié : le contrat reste révisable jusqu’à L3b. Aucun worker L4 n’existe encore ; l’arrêt brutal d’un processus, l’effet externe et l’outbox de T06/T07/T19 ne sont donc pas revendiqués. La montée depuis une future version de schéma et une procédure de sauvegarde/restauration opérationnelle restent à tester lorsqu’une deuxième migration et le Host existeront. Les paramètres WAL, synchronisation, charge et seuils de contention ne sont pas modifiés ni qualifiés en L3a.
