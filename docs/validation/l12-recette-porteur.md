# L12 — recette détaillée des tests lourds

Statut : à exécuter par le porteur. Les tests ci-dessous ne sont pas annoncés comme acquis. Consigner résultats et plateforme dans [la preuve L12](../evidence/l12.md). Le script ne publie aucun package ; le feed de qualification est un répertoire local. Les commandes shell sont prévues pour macOS/Linux ou Git Bash sous Windows ; le script Python est multiplateforme.

## 1. Préparer et compiler

1. Depuis la racine du dépôt, relever `git rev-parse HEAD` et `git status --short`.
2. Vérifier `dotnet --version` : 10.0.401, conformément à `global.json`. Utiliser Python 3.
3. Créer un répertoire jetable **hors du dépôt**, par exemple :

   ```sh
   export EW_L12_ROOT="$(mktemp -d -t ew-l12.XXXXXX)"
   export EW_L12_ROOT="$(python3 -c 'import os,sys; print(os.path.realpath(sys.argv[1]))' "$EW_L12_ROOT")"
   ```

4. Restaurer puis compiler les dépendances verrouillées :

   ```sh
   dotnet restore EnterpriseWorkflow.slnx --locked-mode
   dotnet build EnterpriseWorkflow.slnx --configuration Release --no-restore
   ```

5. Attendu : zéro erreur, zéro avertissement. Conserver les sorties ; interrompre la recette dès qu’une étape échoue. Ne pas transmettre `.env`, cookies ou mots de passe dans les résultats.

## 2. Installer une distribution locale complète

1. Exécuter la phase de packaging (la plus longue ; elle restaure et packe les packages publics nécessaires puis les packages fictifs) :

   ```sh
   python3 scripts/qualify-l12.py packages "$EW_L12_ROOT"
   ```

2. Attendu : `feed/` contient les packages framework et `Example.Workflow.*` version 1.0.0-rc.1 ; `tools/few-example` existe (`.exe` sous Windows) ; `distribution` affiche `Example.Workflow 1.0.0-rc.1`.
3. Le script utilise un cache NuGet neuf pour ne pas confondre les packages construits avec les anciennes releases de même numéro. `NuGet.Config` ne contient que le feed local et NuGet.org, sans credentials.
4. Comparer `distribution.json` à `samples/OrganizationWorkflow/distribution.json`. Le feed privé réel et ses accès restent à qualifier dans le projet organisationnel.

## 3. Préparer les templates et générer

1. Lancer :

   ```sh
   python3 scripts/qualify-l12.py generate "$EW_L12_ROOT"
   ```

2. Le script vérifie d’abord que `new` échoue sans préparation avec une instruction `setup`, puis exécute `setup`, `new` et la restauration du projet.
3. Vérifier `Generated/WorkflowSampleModule.cs` ou le fichier renommé par le moteur : le module dérive de `ExampleWorkflowModule` et `CloseHandler` reçoit `IApprovalPolicy` par constructeur. Aucune liste de services requis dans un manifeste n’est demandée.
4. Vérifier le `.csproj` généré : référence SDK organisationnel exacte `[1.0.0-rc.1]`, et `packages.lock.json` créé après restauration.
5. Les templates de cette qualification utilisent `Templates__Root` dans le répertoire jetable. Un deuxième workspace n’hérite pas des templates du premier. Les réglages NuGet globaux existants ne sont pas réécrits.

## 4. Qualifier dev et les services partagés

1. Dans un premier terminal, démarrer l’API de démonstration :

   ```sh
   dotnet run --project samples/OrganizationWorkflow/PolicyApi --configuration Release --no-build -- --urls http://127.0.0.1:5099
   ```

2. Dans un deuxième terminal, depuis la racine du dépôt, lancer :

   ```sh
   "$EW_L12_ROOT/tools/few-example" dev "$EW_L12_ROOT/Generated" --url http://127.0.0.1:5080
   ```

3. Ouvrir `/__ew/dev`. Choisir Alice, démarrer une demande avec un motif d’au moins trois caractères. Choisir Bob, réclamer la tâche si nécessaire, puis approuver.
4. Attendu : la demande se termine ; le champ `closed` devient vrai. Le terminal de l’API montre un appel `/policy?reason=...`. Cela vérifie la résolution du contrat injecté et l’implémentation HTTP réelle partagée.
5. Arrêter l’API et refaire une demande : l’étape de service doit échouer/rester en attente de retry selon la politique runtime, sans remplacement silencieux par un double. Relancer l’API puis vérifier la reprise selon cette politique.
6. Modifier un libellé dans le module généré : nouvelle génération, anciennes instances conservées. Introduire temporairement une erreur C# : ancienne génération toujours disponible ; corriger ensuite. Ne pas modifier les fichiers du dépôt pour ce scénario.
7. Arrêter `dev` avec Ctrl+C. Le `.env` généré contient un endpoint de démonstration et une langue ; il reste hors de `runtime.json` et des artefacts. Les identités restent celles de `ew.dev.json` ; aucune connexion bancaire, OTP ou admission maker/checker n’est proposée.

## 5. Vérifier et empaqueter le même module

1. Exécuter :

   ```sh
   python3 scripts/qualify-l12.py artifact "$EW_L12_ROOT"
   ```

2. Attendu : `validate`, `inspect`, `pack`, `verify` réussissent. Le manifeste annonce `Example.Workflow` et `[1.0.0-rc.1,2.0.0)`. `artifact-path.txt` identifie le répertoire exact à conserver ; aucun `.env` ou fichier de base ne s’y trouve.
3. Tester le refus par le runtime standard :

   ```sh
   python3 scripts/qualify-l12.py incompatible "$EW_L12_ROOT"
   ```

4. Attendu : diagnostic **EW7055_INCOMPATIBLE_RUNTIME**, et non un autre échec générique. Les bornes SemVer et préversions sont aussi couvertes par les tests unitaires ciblés.

## 6. Exécuter le Kernel et qualifier la marque

1. Publier l’hôte fictif, écrire sa configuration locale et migrer sa base SQLite jetable :

   ```sh
   python3 scripts/qualify-l12.py kernel "$EW_L12_ROOT"
   ```

2. Initialiser un administrateur de test uniquement :

   ```sh
   dotnet "$EW_L12_ROOT/kernel/Example.Workflow.Kernel.dll" bootstrap l12-admin 'Local-L12-Test-2026!'
   ```

3. Redémarrer l’API de démonstration si nécessaire. Dans un autre terminal :

   ```sh
   ASPNETCORE_URLS=http://127.0.0.1:5081 dotnet "$EW_L12_ROOT/kernel/Example.Workflow.Kernel.dll"
   ```

4. Ouvrir `/login` : nom `Example Workflow`, logo et favicon violets ; la feuille `/example-brand.css` doit être chargée sans 404. Vérifier aussi `/_content/EnterpriseWorkflow.Web/enterprise-workflow.css`.
5. Se connecter avec l’administrateur. Dans l’administration, créer les profils/affectations nécessaires pour `sample.workflow.request`, `.review`, `.read`. Exécuter le parcours du module empaqueté, **sans le recompiler**, et retrouver l’appel à la même API `/policy`.
6. Dans les réglages : nom, logo et accent sont verrouillés ; fuseau horaire modifiable. Enregistrer un fuseau différent puis recharger. Modifier un champ verrouillé via les outils du navigateur et soumettre : refus serveur `branding.locked`, sans changement effectif. La protection ne repose pas sur `readonly`.
7. Vérifier portail, administration et connexion en français et anglais, puis en largeur mobile : logo contenu dans son cadre, focus visible, accents cohérents, surfaces et textes lisibles, aucune page de profil de langue réintroduite.
8. Vérifier qu’une ancienne valeur persistée verrouillée ne remplace pas la marque de l’hôte : sur la base jetable arrêtée seulement, modifier `EwPresentationSettings.DisplayName`, redémarrer puis constater que `Example Workflow` reste affiché. Les tests unitaires vérifient aussi ce cas.
9. Arrêter tous les processus. Conserver les logs et artefacts avant de supprimer le répertoire jetable.

## 7. Non-régression et CI

1. Exécuter le socle complet après compilation Release :

   ```sh
   dotnet test --solution EnterpriseWorkflow.slnx --configuration Release --no-build --no-restore
   ```

2. Exécuter le cycle natif de développement :

   ```sh
   EW_DEV_LIFECYCLE_TESTS=1 dotnet run --project tests/EnterpriseWorkflow.Cli.Tests --configuration Release --no-build -- -class '*DevelopmentLifecycleTests'
   ```

3. Pour le navigateur standard, installer Chromium selon la recette L11 puis exécuter :

   ```sh
   PLAYWRIGHT_TEST_ENABLED=1 dotnet run --project tests/EnterpriseWorkflow.Web.Playwright.Tests --configuration Release --no-build
   ```

4. Rejouer les qualifications locales de packaging et navigateur déjà utilisées en L11 (`scripts/qualify-l9.sh`, `scripts/qualify-l9-browser.sh`, avec les prérequis et arguments de leur recette), puis la matrice CI Linux/macOS/Windows. Les modifications du socle CLI justifient cette non-régression ; les campagnes SMTP/LDAP ne sont pas remplacées par les tests L12.
5. Documentation : `node wiki/scripts/check-examples.mjs`, puis `npm --prefix wiki run check` et `npm --prefix wiki run build` après installation des dépendances. Le snapshot public LeaveRequest reste indépendant des nouveautés non publiées.
6. Aucune migration de schéma Oracle n’est introduite ici. Si la qualification Oracle complète est souhaitée pour la release, réutiliser la recette L11 sur un schéma dédié, sans présenter une simple compilation comme preuve Oracle.
7. Remonter pour chaque étape : commande, OS/CPU, code de sortie, nombre de tests réussis/échoués/ignorés et erreurs éventuelles. L12 reste ouvert jusqu’à confirmation des campagnes déléguées.

### Reprise après NU5104

La distribution fictive est une préversion `1.0.0-rc.1`, car elle dépend du framework préversion. Après récupération du correctif, recommencer la phase `packages` avec un **nouveau workspace** : le feed partiellement construit est volontairement conservé et le script refuse de l’écraser. Ne pas désactiver NU5104 ni réutiliser les anciens templates stables.

### Reprise après package CLI absent

Le projet CLI Web doit déclarer `IsPackable=true` en plus de `PackAsTool=true`. Le script contrôle désormais les fichiers nupkg et leur identité/version avant installation. Pour reprendre le workspace conservé, sans reconstruire tous les packages :

```sh
dotnet pack samples/OrganizationWorkflow/Cli/Example.Workflow.Cli.csproj --configuration Release --output "$EW_L12_ROOT/feed" -m:1 -p:UseSharedCompilation=false
python3 scripts/qualify-l12.py install "$EW_L12_ROOT"
```

Après réussite de `distribution`, poursuivre à la phase `generate` du même workspace. La phase `install` vérifie les cinq packages organisationnels avant d’appeler NuGet. Une exécution complète dans un workspace neuf reste possible.

### Reprise après « Missing expected diagnostic: setup »

La génération ne s’est pas encore produite : le premier `new` est un test négatif avant `setup`. L’aide de `dotnet new` peut retourner 0 même sans template ; la CLI contrôle maintenant le code 103 de la génération réelle pour afficher le diagnostic attendu. Recompiler et réinstaller l’outil est nécessaire, car une modification des sources ne remplace pas l’outil déjà installé.

Depuis la racine du dépôt, en conservant le workspace actuel :

```sh
dotnet pack samples/OrganizationWorkflow/Cli/Example.Workflow.Cli.csproj -c Release -o "$EW_L12_ROOT/feed" -m:1 -p:UseSharedCompilation=false
dotnet tool uninstall Example.Workflow.Cli --tool-path "$EW_L12_ROOT/tools"
NUGET_PACKAGES="$(mktemp -d -t ew-l12-tool-refresh.XXXXXX)" dotnet tool install Example.Workflow.Cli --version 1.0.0-rc.1 --tool-path "$EW_L12_ROOT/tools" --configfile "$EW_L12_ROOT/NuGet.Config" --no-cache
python3 scripts/qualify-l12.py generate "$EW_L12_ROOT"
```

Le cache neuf évite de réutiliser le package local de même version construit avant ce correctif. Le dossier attendu est **`$EW_L12_ROOT/Generated`**, pas `Generate` ni un dossier à la racine du dépôt. Ne pas lancer `setup` manuellement avant cette reprise : la phase `generate` vérifie son absence initiale puis l’exécute elle-même.

### Chemin temporaire macOS contenant un alias système

`mktemp` peut renvoyer un chemin commençant par `/var` (alias vers `/private/var`) ou `/tmp` (alias vers `/private/tmp`). Le script Python résout son workspace, mais ne peut pas modifier la variable du shell parent. Avant d’appeler directement l’outil `dev`, normaliser cette variable :

```sh
export EW_L12_ROOT="$(python3 -c 'import os,sys; print(os.path.realpath(sys.argv[1]))' "$EW_L12_ROOT")"
"$EW_L12_ROOT/tools/few-example" dev "$EW_L12_ROOT/Generated" --url http://127.0.0.1:5080
```

Aucune réinstallation ou suppression de données nécessaire. La protection contre les liens symboliques des répertoires de développement reste active. Si une erreur persiste avec un workspace physique, vérifier les liens présents dans `Generated/.ew` plutôt que désactiver ce contrôle.

### Reprise après échec de validation du hash des packages

Le script et les commandes manuelles doivent utiliser le même cache NuGet. La variable NUGET_PACKAGES définie uniquement dans un sous-processus Python ne se propage pas au terminal parent. NuGet.Config fixe désormais globalPackagesFolder dans le workspace et réserve EnterpriseWorkflow.* et Example.Workflow.* au feed local via packageSourceMapping.

Pour mettre à jour un workspace existant, sans supprimer le lockfile ou le cache habituel :

```sh
python3 scripts/qualify-l12.py configure "$EW_L12_ROOT"
"$EW_L12_ROOT/tools/few-example" dev "$EW_L12_ROOT/Generated" --url http://127.0.0.1:5080
```

Une variable NUGET_PACKAGES déjà exportée dans le terminal prévaut sur NuGet.Config : elle doit être absente ou pointer sur "$EW_L12_ROOT/packages". Ne pas désactiver les validations de hash ni effacer packages.lock.json pour contourner une différence de contenu. Les nouvelles qualifications appliquent cette configuration dès la phase packages.
