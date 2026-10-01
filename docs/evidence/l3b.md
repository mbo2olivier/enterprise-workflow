# Preuve du lot L3b — Oracle

Date : 1er octobre 2026.

## Périmètre livré

- projet `EnterpriseWorkflow.Persistence.Oracle` sur Oracle Entity Framework Core `10.23.26301` ;
- contexte et mappings Oracle 19c-compatibles, migration initiale explicite et dictionnaire physique complet ;
- implémentation des six opérations de `IWorkflowStore`, avec horloge Oracle, transactions courtes, verrouillage `FOR UPDATE ... SKIP LOCKED`, génération et token de fencing ;
- normalisation commune des textes facultatifs vides vers `NULL` et rejet des sujets d’acteur vides ;
- clé physique de reçu SHA-256 calculée sur des composantes UTF-8 préfixées par leur longueur, avec conservation et revérification des valeurs originales ;
- composition Oracle Free pour la boucle de développement et surcharge prête pour l’image Oracle Enterprise officielle `19.19.0.0`.

## Environnement exécuté

Hôte : macOS 26.3 ARM64, SDK .NET `10.0.401`, runtime .NET `10.0.12`, Docker Desktop, image `gvenzl/oracle-free:23.26.3-slim-faststart`, service `FREEPDB1` et schéma jetable `EWTEST`.

Commandes essentielles :

```bash
ORACLE_TEST_PASSWORD='***' docker compose -f tests/oracle/docker-compose.yml up -d --wait
ENTERPRISE_WORKFLOW_ORACLE_CONNECTION_STRING='User Id=EWTEST;Password=***;Data Source=localhost:1521/FREEPDB1;Connection Timeout=30' \
  dotnet test --project tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --configuration Release
```

Résultat Oracle Free : **6 tests réussis, 0 échec, 0 ignoré**. Les scénarios couvrent migration et démarrage idempotent, collision de contenu, claims concurrents, expiration et fencing, reprise jusqu’à terminaison, annulation atomique, conflit de révision sans écriture partielle et normalisation chaîne vide/`NULL`.

La suite locale par défaut, indépendante de Docker, contient **49 tests réussis** : 44 tests Core/contrats/architecture et 5 tests SQLite.

## Qualification officielle différée

L’image Oracle Enterprise officielle `19.19.0.0` est configurée dans `tests/oracle/docker-compose.19c.yml`. L’accès au registre et les conditions d’utilisation ont été validés, puis le téléchargement a été interrompu à la demande du porteur afin de préserver une connexion limitée. Aucun résultat 19.19 n’est donc revendiqué.

Le plan sépare désormais :

- **L3b-I, terminé** : implémentation et validation de développement sur Oracle Free réel ;
- **L3b-Q, différé** : téléchargement sur connexion non limitée, migration, exécution des six tests sur 19.19, relevé de la version et du digest de l’image.

Oracle Free démontre le comportement du provider et du SQL sur une base Oracle réelle, mais ne remplace pas la qualification du minimum produit 19c. L3b-Q reste obligatoire avant toute annonce de support Oracle 19c ou toute release MVP.

## Limites restantes

Les tests L3b portent sur la persistance disponible à ce lot. Le worker, l’arrêt brutal de processus, les effets externes, l’outbox, les tâches humaines, les timers, la restauration et la charge appartiennent aux lots ultérieurs. Le compte de test peut créer et supprimer son schéma ; les privilèges minimaux séparés entre migration et exécution restent à qualifier dans le runbook de release.
