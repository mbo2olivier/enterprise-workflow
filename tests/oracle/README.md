# Tests d’intégration Oracle

Le projet `EnterpriseWorkflow.Persistence.Oracle.Tests` n’appartient pas à la solution exécutée sans base : il doit être lancé explicitement et un échec de provisionnement n’est jamais présenté comme un test réussi.

## Boucle de développement ARM64

L’image publique `gvenzl/oracle-free:23.26.3-slim-faststart` permet de détecter rapidement les erreurs de mapping et de SQL sur Apple Silicon. Elle ne qualifie pas Oracle 19c.

```bash
export ORACLE_TEST_PASSWORD='ChooseAStrongTestPassword_123'
docker compose -f tests/oracle/docker-compose.yml up -d --wait
export ENTERPRISE_WORKFLOW_ORACLE_CONNECTION_STRING="User Id=EWTEST;Password=${ORACLE_TEST_PASSWORD};Data Source=localhost:1521/FREEPDB1;Connection Timeout=30"
dotnet test --project tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --configuration Release
```

## L3b-Q — qualification Oracle 19.19 officielle

Cette étape est prête mais différée jusqu’à la disponibilité d’une connexion non limitée. Elle ne doit pas être présentée comme réussie avant l’exécution complète des commandes suivantes.

Après authentification à Oracle Container Registry et acceptation de ses conditions :

```bash
export ORACLE_TEST_PASSWORD='ChooseAStrongTestPassword_123'
docker compose -f tests/oracle/docker-compose.yml -f tests/oracle/docker-compose.19c.yml up -d --wait
export ENTERPRISE_WORKFLOW_ORACLE_CONNECTION_STRING="User Id=EWTEST;Password=${ORACLE_TEST_PASSWORD};Data Source=localhost:1521/ORCLPDB1;Connection Timeout=30"
dotnet test --project tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --configuration Release
```

Le mot de passe reste local et ne doit pas être ajouté au dépôt. Le schéma `EWTEST` est jetable : les tests en suppriment les objets avant chaque scénario. Arrêt :

```bash
docker compose -f tests/oracle/docker-compose.yml -f tests/oracle/docker-compose.19c.yml down -v
```
