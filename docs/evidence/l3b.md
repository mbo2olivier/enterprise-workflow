# Preuve du lot L3b — Oracle

Date : 1er octobre 2026.

## Périmètre livré

- projet `EnterpriseWorkflow.Persistence.Oracle` sur Oracle Entity Framework Core `10.23.26301` ;
- contexte et mappings Oracle 19c-compatibles, migration initiale explicite et dictionnaire physique complet ;
- implémentation des six opérations de `IWorkflowStore`, avec horloge Oracle, transactions courtes, verrouillage `FOR UPDATE ... SKIP LOCKED`, génération et token de fencing ;
- normalisation commune des textes facultatifs vides vers `NULL` et rejet des sujets d’acteur vides ;
- clé physique de reçu SHA-256 calculée sur des composantes UTF-8 préfixées par leur longueur, avec conservation et revérification des valeurs originales ;
- composition Oracle Free pour la boucle de développement et surcharge qualifiée sur l’image Oracle Enterprise officielle `19.19.0.0` ;
- healthcheck 19.19 exécutant une requête SQL avec `whenever sqlerror exit 1`, afin d’attendre la disponibilité réelle de `ORCLPDB1`.

## Environnement exécuté

Hôte : macOS 26.3 ARM64, SDK .NET `10.0.401`, runtime .NET `10.0.12` et Docker Desktop.

Bases exécutées :

- `gvenzl/oracle-free:23.26.3-slim-faststart`, service `FREEPDB1` ;
- `container-registry.oracle.com/database/enterprise:19.19.0.0`, serveur `19.19.0.0.0`, Linux ARM64, service `ORCLPDB1` ;
- schéma jetable `EWTEST` dans les deux environnements.

Commandes essentielles :

```bash
ORACLE_TEST_PASSWORD='***' docker compose -f tests/oracle/docker-compose.yml up -d --wait
ENTERPRISE_WORKFLOW_ORACLE_CONNECTION_STRING='User Id=EWTEST;Password=***;Data Source=localhost:1521/FREEPDB1;Connection Timeout=30' \
  dotnet test --project tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --configuration Release
```

Résultat Oracle Free : **6 tests réussis, 0 échec, 0 ignoré**. Les scénarios couvrent migration et démarrage idempotent, collision de contenu, claims concurrents, expiration et fencing, reprise jusqu’à terminaison, annulation atomique, conflit de révision sans écriture partielle et normalisation chaîne vide/`NULL`.

La suite locale par défaut, indépendante de Docker, contient **49 tests réussis** : 44 tests Core/contrats/architecture et 5 tests SQLite.

## Qualification officielle L3b-Q

Image qualifiée :

```text
container-registry.oracle.com/database/enterprise:19.19.0.0
sha256:3843234f6fd1ed084b1dd7ad42eeeaa95ad13f12c790810cb368bb58f8ad42ba
linux/arm64
Oracle Database 19.19.0.0.0
```

Commande de qualification :

```bash
ORACLE_TEST_PASSWORD='***' docker compose \
  -f tests/oracle/docker-compose.yml \
  -f tests/oracle/docker-compose.19c.yml up -d --wait
ENTERPRISE_WORKFLOW_ORACLE_CONNECTION_STRING='User Id=EWTEST;Password=***;Data Source=localhost:1521/ORCLPDB1;Connection Timeout=30' \
  dotnet test --project tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --configuration Release
```

Résultat Oracle Enterprise 19.19 : **6 tests réussis, 0 échec, 0 ignoré** en 4,141 secondes. La migration initiale est appliquée par chaque fixture après remise à zéro du schéma. Les mêmes scénarios que sur Oracle Free couvrent idempotence, claims concurrents, fencing, reprise, annulation, conflit de révision et sémantique chaîne vide/`NULL`.

Le plan conserve deux jalons, désormais terminés :

- **L3b-I, terminé** : implémentation et validation de développement sur Oracle Free réel ;
- **L3b-Q, terminé** : migration et six tests d’intégration sur Oracle Enterprise 19.19 officiel, version et digest relevés.

Cette preuve qualifie le minimum Oracle 19c pour le périmètre de persistance L3b sur la combinaison testée. Elle ne qualifie pas à elle seule une distribution complète du produit ni d’autres OS, architectures ou patchsets Oracle.

## Limites restantes

Les tests L3b portent sur la persistance disponible à ce lot. Le worker, l’arrêt brutal de processus, les effets externes, l’outbox, les tâches humaines, les timers, la restauration et la charge appartiennent aux lots ultérieurs. Le compte de test peut créer et supprimer son schéma ; les privilèges minimaux séparés entre migration et exécution restent à qualifier dans le runbook de release.
