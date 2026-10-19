# Tester l’interface du Kernel

Date : 7 octobre 2026. Ce guide permet d’ouvrir l’application L8 dans un navigateur, de vérifier ses écrans métier et administratifs, puis d’exécuter le smoke Playwright. SQLite suffit pour le test manuel ; Docker et Oracle ne sont pas requis.

## Prérequis

- SDK .NET 10 accepté par `global.json` ;
- restauration et compilation `Release` réussies ;
- port local `5099` disponible ;
- navigateur récent.

Depuis la racine du dépôt :

```bash
dotnet restore EnterpriseWorkflow.slnx --locked-mode
dotnet build src/EnterpriseWorkflow.Kernel/EnterpriseWorkflow.Kernel.csproj --configuration Release --no-restore
```

## Préparer une installation SQLite jetable

Les commandes suivantes utilisent une base dédiée dans le répertoire temporaire. Elles ne touchent ni Oracle ni une base applicative existante.

### macOS ou Linux

Dans un même terminal :

```bash
export EW_UI_DATABASE="/tmp/enterprise-workflow-ui-test.db"
export ConnectionStrings__EnterpriseWorkflow="Data Source=${EW_UI_DATABASE};Pooling=False"
export Database__Provider="Sqlite"
export Database__MigrationMode="Validate"
export Security__AuthenticationProvider="local"

dotnet run --project src/EnterpriseWorkflow.Kernel/EnterpriseWorkflow.Kernel.csproj --configuration Release --no-build --no-restore -- migrate
dotnet run --project src/EnterpriseWorkflow.Kernel/EnterpriseWorkflow.Kernel.csproj --configuration Release --no-build --no-restore -- bootstrap ui-admin 'Ui_Test_Only_2026'
```

Le mot de passe ci-dessus est réservé à cette base locale jetable. Utiliser un secret distinct si la machine ou le Host est accessible à d’autres utilisateurs. Le bootstrap ne peut être exécuté qu’une fois sur une installation.

### Windows PowerShell

```powershell
$env:EW_UI_DATABASE = Join-Path $env:TEMP "enterprise-workflow-ui-test.db"
$env:ConnectionStrings__EnterpriseWorkflow = "Data Source=$env:EW_UI_DATABASE;Pooling=False"
$env:Database__Provider = "Sqlite"
$env:Database__MigrationMode = "Validate"
$env:Security__AuthenticationProvider = "local"

dotnet run --project src/EnterpriseWorkflow.Kernel/EnterpriseWorkflow.Kernel.csproj --configuration Release --no-build --no-restore -- migrate
dotnet run --project src/EnterpriseWorkflow.Kernel/EnterpriseWorkflow.Kernel.csproj --configuration Release --no-build --no-restore -- bootstrap ui-admin "Ui_Test_Only_2026"
```

## Démarrer l’application

Conserver les variables précédentes et lancer :

```bash
ASPNETCORE_URLS="http://127.0.0.1:5099" dotnet run --project src/EnterpriseWorkflow.Kernel/EnterpriseWorkflow.Kernel.csproj --configuration Release --no-build --no-restore
```

Sous PowerShell :

```powershell
$env:ASPNETCORE_URLS = "http://127.0.0.1:5099"
dotnet run --project src/EnterpriseWorkflow.Kernel/EnterpriseWorkflow.Kernel.csproj --configuration Release --no-build --no-restore
```

Ouvrir [http://127.0.0.1:5099/login](http://127.0.0.1:5099/login), puis se connecter avec :

- utilisateur : `ui-admin` ;
- mot de passe : `Ui_Test_Only_2026`.

L’avertissement indiquant que le port HTTPS n’a pas pu être déterminé est attendu pour ce démarrage HTTP local. Il ne doit pas être ignoré dans un déploiement réel.

## Parcours manuel conseillé

| Écran | URL | Vérification |
| --- | --- | --- |
| Connexion | `/login` | Mauvais mot de passe refusé ; bon mot de passe redirigé vers l’accueil |
| Aujourd’hui | `/` | Shell, navigation, identité de session et cartes sans débordement horizontal |
| Catalogue | `/start` | Liste des processus autorisés, ou état vide explicite lorsqu’aucun module n’est installé |
| Inbox | `/inbox` | Liste bornée ou état vide, sans exposition d’une tâche non autorisée |
| Mes demandes | `/requests` | Instances initiées visibles sans exposition de l’état JSON brut |
| Administration | `/admin` | Accès aux surfaces autorisées pour l’administrateur initial |
| Accès | `/admin/access` | Onglets Profils / Identités / Fournisseurs ; création et modification d’un profil en modale ; création d’une identité locale en modale |
| Workflows | `/admin/workflows` | Habilitations exactes par workflow, version, nœud et action |
| Exploitation | `/admin/operations` | Bandeau de santé et onglets Instances / Outbox / Modules en lecture contrôlée |
| Audit | `/admin/audit` | Mutations administratives précédentes présentes |
| Réglages | `/admin/settings` | Panneaux Apparence / Sessions / Runtime ; nom et couleur enregistrés après redirection |

Pour les vérifications responsive, utiliser les outils du navigateur avec une largeur de `390 px`. Parcourir ensuite la page uniquement avec `Tab`, `Maj+Tab`, `Entrée` et `Espace` ; le focus doit rester visible et atteindre liens, boutons, champs et listes.

## Catalogue et module LeaveRequest

Le Kernel ne découvre pas directement un projet source ou une DLL isolée. `Modules:Directories` attend un artefact L7 complet : `module.json`, assembly d’entrée, dépendances et empreintes SHA-256. Sans répertoire configuré, le catalogue vide est donc le comportement attendu et le reste de l’administration demeure testable.

Le source de démonstration se trouve dans `samples/LeaveRequest.Module`. Son packaging reproductible appartient au lot L9 ; ne pas fabriquer un manifeste approximatif ni désactiver les contrôles d’empreinte pour remplir le catalogue. Après installation d’un artefact valide, ajouter son répertoire à `Modules:Directories`, redémarrer le Kernel, puis vérifier :

1. apparition de « Demande de congé » dans le catalogue ;
2. habilitation de l’action de démarrage et des actions du nœud `manager-approval` ;
3. recherche d’un responsable habilité ;
4. formulaire versionné et validation serveur ;
5. apparition de la tâche dans l’inbox du responsable ;
6. claim, release, approbation ou refus ;
7. visibilité de la demande et audit correspondant.

## Qualification Playwright

Le test navigateur crée sa propre base temporaire et son propre administrateur. Il n’utilise pas la base manuelle préparée plus haut.

```bash
dotnet build tests/EnterpriseWorkflow.Web.Playwright.Tests/EnterpriseWorkflow.Web.Playwright.Tests.csproj --configuration Release --no-restore
pwsh tests/EnterpriseWorkflow.Web.Playwright.Tests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium
PLAYWRIGHT_TEST_ENABLED=1 dotnet test tests/EnterpriseWorkflow.Web.Playwright.Tests/EnterpriseWorkflow.Web.Playwright.Tests.csproj --configuration Release --no-build --no-restore
```

Sous PowerShell, définir la variable avant la dernière commande :

```powershell
$env:PLAYWRIGHT_TEST_ENABLED = "1"
dotnet test tests/EnterpriseWorkflow.Web.Playwright.Tests/EnterpriseWorkflow.Web.Playwright.Tests.csproj --configuration Release --no-build --no-restore
```

Le test qualifie la connexion, la mutation des réglages, l’application du thème, une largeur mobile et la navigation clavier. La procédure de qualification complète du lot, incluant Oracle et GitHub Actions, reste décrite dans [Validation du lot L8](l8-validation.md).

## Arrêt, reprise et réinitialisation

Arrêter le Host avec `Ctrl+C`. Relancer la même commande avec la même chaîne SQLite doit conserver le compte, les réglages et les données persistées.

Pour repartir de zéro, arrêter d’abord le Host puis supprimer uniquement le fichier explicitement choisi pour `EW_UI_DATABASE`, ainsi que ses éventuels fichiers suffixés `-wal` et `-shm`. Ne jamais appliquer cette opération à une base partagée ou à un chemin non vérifié. Rejouer ensuite `migrate` et `bootstrap`.

## Diagnostic rapide

| Symptôme | Cause probable | Action |
| --- | --- | --- |
| « Database schema is not ready » | Migration absente ou chaîne différente | Vérifier `ConnectionStrings__EnterpriseWorkflow`, puis rejouer `migrate` |
| « already bootstrapped » | Le compte initial existe déjà | Utiliser le compte existant ou repartir d’une base jetable explicitement identifiée |
| Connexion refusée après plusieurs essais | Mot de passe incorrect ou verrouillage temporaire | Attendre la durée configurée ou recréer la base jetable |
| Port déjà utilisé | Un autre Host écoute sur `5099` | Arrêter l’ancien processus ou choisir une autre valeur `ASPNETCORE_URLS` |
| Catalogue vide | Aucun artefact de module configuré | Comportement attendu avant packaging/installation L9 |
| Mutation administrative refusée | Permission absente, antiforgery ou révision périmée | Consulter l’alerte de la page et le journal Kernel sans désactiver le contrôle |
