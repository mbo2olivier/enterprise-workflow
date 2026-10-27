# L9b — validation de formulaires et correctifs CI

Les tests longs ci-dessous restent à exécuter par le développeur. Aucune réinitialisation de sa base courante n’est nécessaire.

## Préparation commune

Depuis la racine du dépôt, avec le SDK indiqué dans `global.json` :

```sh
dotnet restore EnterpriseWorkflow.slnx --locked-mode
dotnet build EnterpriseWorkflow.slnx --configuration Release --no-restore
```

Relancer le serveur de développement pour charger les nouvelles assemblies. Le sample courant est en release 1.7.0 / contrat 8 ; les sources produisent leur propre génération locale.

```sh
dotnet run --project src/EnterpriseWorkflow.Cli --configuration Release --no-build -- dev samples/LeaveRequest.Module --environment validation --configuration Release --url http://localhost:5099 --no-watch
```

Cet environnement nommé utilise `.ew/environments/validation`, indépendamment du serveur habituel. Le sample du dépôt référence les projets sources ; il ne nécessite pas de NuGet local supplémentaire.

## V1 — erreur au démarrage et conservation de la saisie

1. Choisir Alice, ouvrir le catalogue et démarrer une demande de congé.
2. Saisir une date de fin antérieure à la date de début et un motif reconnaissable. Joindre un document autorisé si souhaité.
3. Envoyer la demande.
4. Attendre un récapitulatif avec le message exact : « La date de fin doit être postérieure ou égale à la date de début. »
5. Les valeurs restent saisies ; les pièces déjà déposées restent attachables sans nouveau dépôt. Aucun processus n’est créé lors du rejet.
6. Cette erreur est globale : le validateur d’état ne déclare pas de correspondance avec un champ de formulaire.
7. Corriger les dates et renvoyer : une seule demande doit être créée.

## V2 — plusieurs erreurs provenant d’un handler

Dans un handler de formulaire, remplacer temporairement le retour de succès par :

```csharp
return ValueTask.FromResult(HumanTaskCompletionResult.Rejected(
    new WorkflowValidationError("leave.invalid-dates", "La date de fin est incohérente.", "endDate"),
    new WorkflowValidationError("leave.reason-required", "Précisez le motif de la demande.", "reason"),
    new WorkflowValidationError("leave.balance", "Le solde disponible est insuffisant.")));
```

Namespace : `EnterpriseWorkflow.Core.Model` pour les erreurs, `EnterpriseWorkflow.Runtime` pour le résultat. Choisir des identifiants correspondant aux champs du formulaire concerné. Utiliser un formulaire contenant ces champs ; le formulaire de confirmation du sample n’a pas nécessairement les mêmes champs que celui de démarrage.

Résultat attendu : trois messages dans le récapitulatif, deux champs marqués invalides avec leur message en dessous, et la troisième erreur uniquement dans le récapitulatif. Aucun changement d’état, commentaire ou transition n’est enregistré. Les messages sont du texte : du HTML fourni dans un message ne doit pas s’exécuter.

Les initialisateurs peuvent interrompre le démarrage par `throw new WorkflowValidationException(error1, error2);`. Les validateurs d’état implémentent `ValidateErrorsAsync` et retournent une liste de `WorkflowValidationError` ; `Field` reste absent sauf correspondance de formulaire explicitement voulue. Ne pas utiliser `Exception.Message` pour exposer une erreur technique ; une panne inattendue doit produire une référence de diagnostic corrélée aux logs.

## V3 — validation serveur et audit

1. Dans les outils du navigateur, enlever temporairement `required` d’un champ texte obligatoire et envoyer une valeur vide ou composée d’espaces.
2. Vérifier le message « Ce champ est obligatoire. », le libellé du champ dans le récapitulatif et son indication visuelle d’erreur.
3. Ouvrir `/admin/audit` en administrateur : tous les champs doivent rester dans la grille supérieure, les commandes Rechercher et Exporter dans un pied distinct, y compris sur une fenêtre étroite.
4. Vérifier qu’un autre utilisateur ne peut pas récupérer les saisies d’Alice en copiant l’URL de retour. Le retour est lié à l’identité, à la page, consommé une fois et expire après dix minutes. Une actualisation après consommation ne restaure pas les saisies.

## C1 — cycle complet CLI natif (ancien échec macOS)

Ce test reconstruit plusieurs générations et environnements ; il est volontairement laissé à la recette longue.

macOS / Linux :

```sh
EW_DEV_LIFECYCLE_TESTS=1 dotnet test --project tests/EnterpriseWorkflow.Cli.Tests/EnterpriseWorkflow.Cli.Tests.csproj --configuration Release --no-build --no-restore
```

PowerShell :

```powershell
$env:EW_DEV_LIFECYCLE_TESTS = '1'
dotnet test --project tests/EnterpriseWorkflow.Cli.Tests/EnterpriseWorkflow.Cli.Tests.csproj --configuration Release --no-build --no-restore
Remove-Item Env:EW_DEV_LIFECYCLE_TESTS
```

Attendu : aucune erreur, notamment `SourceGenerationsPreserveInstancesAndNamedEnvironmentsAreIsolated`. Le test attend maintenant l’apparition de la tâche après restauration (30 secondes maximum), au lieu de confondre disponibilité HTTP et disponibilité du travail asynchrone. Il conserve ses environnements temporaires `ew-native-lifecycle-*` pour diagnostic.

## C2 — packages locaux, projets générés et watch (anciens échecs Linux/Windows)

Prérequis : .NET, Python 3 pour les scripts de qualification seulement, et Bash (Git Bash sous Windows). Employer un nouveau répertoire à chaque exécution ; ne pas réutiliser les artefacts d’une exécution précédente.

macOS / Linux, exemple :

```sh
bash scripts/qualify-l9.sh /private/tmp/ew-l9-validation-ci-01
```

Sur Linux remplacer `/private/tmp/...` par `/tmp/...`. Sous Git Bash Windows :

```sh
bash scripts/qualify-l9.sh "$TEMP/ew-l9-validation-ci-01"
```

Le script construit le feed local, écrit son `NuGet.Config` avec le mapping `EnterpriseWorkflow.*` vers ce feed, installe le CLI et les templates localement, puis restaure les projets générés. Aucune sélection manuelle d’un package et aucun nettoyage du cache global n’est nécessaire.

Attendu : aucune 404 lors du démarrage ; la version provient du lien réel du catalogue. La base est `.ew/environments/qualification/workflow.db`. Une erreur de compilation garde l’ancienne génération active ; une correction des sources produit g2 sans changement manuel de release ; l’instance g1 conserve son état et termine avec ses handlers d’origine.

Conserver `dev-qualification.log` et le répertoire de qualification en cas d’échec. Après commit/push, relancer la matrice GitHub Actions sur les trois OS ; les résultats locaux ne remplacent pas cette validation.

## C3 — parcours navigateur et Oracle, si souhaité avant clôture

Les instructions complètes de préparation des packages locaux, navigateur et SMTP restent dans `docs/validation/l9b-final-recette.md`. La qualification complète peut être exécutée avec :

```sh
bash scripts/qualify-l9-browser.sh /private/tmp/ew-l9-validation-browser-01
./tests/oracle/oracle-test.sh test
```

La recette Oracle utilise exclusivement la base de tests configurée par son script. Les tests navigateur incluent un scénario de refus et vérifient maintenant le message de commentaire obligatoire ainsi que la conservation de sa saisie.
