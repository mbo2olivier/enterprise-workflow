# Validation du lot L8

Date : 7 octobre 2026.

Statut : qualification terminée. Retour du porteur : `OK solution / Playwright Linux / Oracle 19.19 / GitHub Actions`.

Ce fichier constitue la procédure unique de qualification finale de L8. L'implémentation est terminée ; les commandes longues sont volontairement laissées au porteur afin qu'il puisse les exécuter dans son environnement et ne transmettre que le résultat ou les logs d'échec.

## 1. Solution complète

Depuis la racine du dépôt :

```bash
dotnet restore EnterpriseWorkflow.slnx --locked-mode
MSBUILDDISABLENODEREUSE=1 DOTNET_CLI_USE_MSBUILD_SERVER=0 dotnet build EnterpriseWorkflow.slnx --configuration Release --no-restore -m:1
dotnet test --solution EnterpriseWorkflow.slnx --configuration Release --no-build --no-restore
```

Résultat attendu : compilation sans erreur et aucun test en échec. Le test Playwright est ignoré dans cette commande tant que `PLAYWRIGHT_TEST_ENABLED` n'est pas défini ; cet état est attendu et ne vaut pas qualification navigateur.

## 2. Parcours navigateur Playwright sur Linux

La solution doit d'abord avoir été compilée en `Release`. Installer Chromium puis activer explicitement le test :

```bash
pwsh tests/EnterpriseWorkflow.Web.Playwright.Tests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium
PLAYWRIGHT_TEST_ENABLED=1 dotnet test tests/EnterpriseWorkflow.Web.Playwright.Tests/EnterpriseWorkflow.Web.Playwright.Tests.csproj --configuration Release --no-build --no-restore
```

Résultat attendu : connexion, réglages administratifs, thème, affichage mobile et navigation clavier réussissent sur Chromium. Cette qualification est prévue sur Linux ; elle n'est pas requise sur les jobs Windows et macOS.

Après une modification du test ou du Host, reconstruire uniquement cette cible avant de la rejouer :

```bash
dotnet build tests/EnterpriseWorkflow.Web.Playwright.Tests/EnterpriseWorkflow.Web.Playwright.Tests.csproj --configuration Release --no-restore
PLAYWRIGHT_TEST_ENABLED=1 dotnet test tests/EnterpriseWorkflow.Web.Playwright.Tests/EnterpriseWorkflow.Web.Playwright.Tests.csproj --configuration Release --no-build --no-restore
```

En cas d'échec de la sauvegarde des réglages, le message rapporte le statut HTTP du POST, l'URL après soumission, l'alerte éventuelle et le journal du processus Kernel. Ces informations permettent de distinguer un formulaire bloqué côté navigateur, un refus antiforgery/autorisation, un conflit de révision, une exception serveur et un défaut de rendu du message de succès.

## 3. Oracle Enterprise 19.19

La suite Oracle réinitialise son schéma de test. Ne jamais lui fournir un schéma partagé, une base de production ou un compte contenant des données à conserver. Le script versionné utilise exclusivement le compte jetable `EWTEST` et lit son secret depuis `tests/oracle/.env`, ignoré par Git.

Lors de la première utilisation, ou pour remplacer une ancienne stack dont les credentials ne sont plus connus :

```bash
./tests/oracle/oracle-test.sh init
./tests/oracle/oracle-test.sh reset
dotnet build tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --configuration Release --no-restore -m:1
./tests/oracle/oracle-test.sh test --no-build --no-restore
```

`reset` détruit le volume du projet de test Oracle avant de le recréer. Pour les exécutions suivantes, lancer seulement `oracle-test.sh test --no-build --no-restore` : les fixtures nettoient et remigrent le schéma avant leurs scénarios. Le mot de passe ne doit ni être commité, ni être copié dans un rapport.

## 4. Validation GitHub Actions

La matrice doit être verte sur Linux, macOS et Windows. Le job Linux doit en plus réussir la qualification LDAP existante et le test Playwright activé. Oracle reste une qualification séparée avec secret injecté.

## Retour attendu

En cas de succès, un retour court suffit, par exemple :

```text
OK solution / Playwright Linux / Oracle 19.19 / GitHub Actions
```

En cas d'échec, transmettre la commande concernée et son log depuis la première erreur. Il n'est pas nécessaire de joindre les sorties des étapes réussies.
