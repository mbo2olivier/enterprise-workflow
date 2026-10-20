# Tests d’intégration Oracle

Le projet `EnterpriseWorkflow.Persistence.Oracle.Tests` n’appartient pas à la solution exécutée sans base : il doit être lancé explicitement et un échec de provisionnement n’est jamais présenté comme un test réussi.

## Environnement local reproductible

La stack officielle utilise `tests/oracle/.env`, ignoré par Git. Le script ne révèle pas le secret dans la ligne de commande ou dans sa sortie.

```bash
./tests/oracle/oracle-test.sh init
./tests/oracle/oracle-test.sh reset
./tests/oracle/oracle-test.sh test --no-build --no-restore
```

`init` crée une seule fois un mot de passe Oracle 19c compatible de 27 caractères et un port local dans `.env`. Si `.env` contient un placeholder ou un ancien secret dépassant la limite Oracle de 30 caractères, il est remplacé automatiquement. Le fichier est limité au propriétaire (`0600`). `reset` détruit exclusivement les conteneurs et volumes de la composition située dans `tests/oracle`, puis reprovisionne Oracle Enterprise 19.19 et le compte jetable `EWTEST`. Cette commande efface donc toutes les données de cette stack de test.

La même variable `ORACLE_TEST_PASSWORD` alimente le mot de passe système de l'image officielle, le provisioning de `EWTEST` et la chaîne de connexion .NET. Une base déjà initialisée conserve toutefois ses anciens credentials dans son volume. Après toute modification manuelle de `.env`, ou après `oracle-test.sh rotate`, un `oracle-test.sh reset` est donc obligatoire.

Il n'est pas nécessaire de recréer l'instance avant chaque test. La collection Oracle interdit la parallélisation et chaque fixture appelle `EnsureDeletedAsync`, recrée ses migrations et travaille uniquement dans `EWTEST`. Le redémarrage coûteux du conteneur est réservé à un changement de credentials ou à une instance incohérente.

Le healthcheck Oracle 19.19 se connecte à `ORCLPDB1` avec le compte jetable `EWTEST`, et non avec `SYSTEM` : il vérifie ainsi le chemin réellement utilisé par la suite sans dépendre de l’état du compte d’administration. SQL*Plus utilise `-L` afin qu’une erreur d’authentification échoue immédiatement. `oracle-test.sh` borne l’attente à 20 minutes — l’initialisation 19c peut être longue — puis affiche automatiquement l’état et les 200 dernières lignes du journal au lieu de rester silencieux.

Si le journal indique que `ORCLPDB1` est ouvert mais que Compose reste en attente, exécuter `./tests/oracle/oracle-test.sh status`. Après une mise à jour du healthcheck, un simple `up` ou `test` recrée le conteneur si nécessaire tout en conservant le volume. Utiliser `reset` uniquement lorsque le mot de passe du volume ne correspond plus à `.env` ou lorsque la base doit réellement être reprovisionnée, car cette commande détruit les données de test.

Commandes de cycle de vie :

```bash
./tests/oracle/oracle-test.sh up
./tests/oracle/oracle-test.sh status
./tests/oracle/oracle-test.sh rotate
./tests/oracle/oracle-test.sh down
./tests/oracle/oracle-test.sh destroy
```

`down` conserve le volume ; `destroy` le supprime. Un modèle manuel sans secret réel est fourni dans `.env.example`.

## Boucle de développement ARM64

L’image publique `gvenzl/oracle-free:23.26.3-slim-faststart` permet de détecter rapidement les erreurs de mapping et de SQL sur Apple Silicon. Elle ne qualifie pas Oracle 19c.

```bash
export ORACLE_TEST_PASSWORD='ChooseAStrongTestPassword_123'
docker compose -f tests/oracle/docker-compose.yml up -d --wait
export ENTERPRISE_WORKFLOW_ORACLE_CONNECTION_STRING="User Id=EWTEST;Password=${ORACLE_TEST_PASSWORD};Data Source=localhost:1521/FREEPDB1;Connection Timeout=30"
dotnet test --project tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --configuration Release
```

## L3b-Q — qualification Oracle 19.19 officielle

Cette étape a été exécutée avec succès le 1er octobre 2026 sur Oracle Enterprise `19.19.0.0.0` ARM64. Les commandes restent la procédure de reproduction ; chaque future version ou plateforme revendiquée doit les repasser.

Après authentification à Oracle Container Registry et acceptation de ses conditions :

```bash
./tests/oracle/oracle-test.sh up
./tests/oracle/oracle-test.sh test
```

Le mot de passe reste local et ne doit pas être ajouté au dépôt. Le schéma `EWTEST` est jetable : les tests en suppriment les objets avant chaque scénario. Arrêt :

```bash
./tests/oracle/oracle-test.sh down
```
