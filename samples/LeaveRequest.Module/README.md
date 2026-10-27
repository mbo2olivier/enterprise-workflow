# Module LeaveRequest — L8b

La déclaration complète est dans [LeaveRequestModule.cs](LeaveRequestModule.cs). Elle illustre les métadonnées uniques, le schéma d’état explicite, les champs fluents, les permissions métier, les processus séparés et les formulaires d’approbation intégrés.

Parcours : formulaire initial → confirmation du demandeur → pool RH → manager sélectionné depuis l’action RH → service de clôture → fin. Le refus RH ou manager exige un commentaire ; l’annulation de la confirmation est une branche métier distincte. Le stage `draft` ne crée aucun mécanisme de brouillon avant démarrage. Le handler de clôture est volontairement un service minimal à remplacer par le traitement métier de l’installation.

Permissions déclarées, attribuables dans l’administration des profils sous les noms de profils choisis par l’installation :

| Permission qualifiée | Usage |
| --- | --- |
| `sample.leave-request.request` | Créer puis confirmer sa demande |
| `sample.leave-request.read` | Lire les instances ; cette attribution est indépendante du traitement d’une tâche |
| `sample.leave-request.hr.review` | Prendre puis traiter une tâche RH |
| `sample.leave-request.manager.approve` | Être sélectionné et statuer comme responsable |

Prévoir au moins trois identités pour démontrer les séparations : demandeur, RH et manager. L’approbation de sa propre demande est interdite par défaut. Le choix du manager utilise les candidats habilités de la tâche cible ; aucun rôle ou nom de groupe LDAP n’est inscrit dans le module.

Le formulaire métier déclare son handler indépendamment de son enregistrement. Les tâches de confirmation et d’approbation utilisent les formulaires standard ; la confirmation montre une substitution explicite de handler. Les résumés exposent uniquement les champs déclarés pour les participants. `privateNote` n’apparaît pas dans les résumés. Les commentaires sont validés mais le handler standard ne les fusionne pas dans les données métier ; cette conservation relève d’un handler métier si elle est requise.

Le manifeste est produit depuis le module avec `WorkflowModuleManifestWriter.Create` ; le test `LeaveRequestModuleTests` construit l’artefact et le charge réellement. Les composants restent manifestés et hachés avec leur artefact. Le processus est qualifié sous `sample.leave-request.leave-request`, révision 7 (release 1.6.0, schéma d’état 4).

Les emails de pool sont activés uniquement si `Notifications:Smtp:Host` est configuré dans le Kernel. Sans SMTP, le parcours inbox est identique. Pour les comptes locaux, fournir la correspondance `Notifications:Smtp:Addresses` avec une clé `local|<subjectId>` ; les identités d’annuaire utilisent leur email courant lorsqu’il est disponible. Les secrets SMTP restent dans la configuration de l’installation.

Le [guide SDK](../../docs/sdk-fluent-design.md) décrit les contrats ; [l8b-validation.md](../../docs/l8b-validation.md) contient les tests externes restant à exécuter.

## Exécuter avec la CLI L9

Après installation locale de `ew` selon le [guide](../../docs/l9-cli.md), lancer `ew dev --project samples/LeaveRequest.Module/EnterpriseWorkflow.LeaveRequest.Module.csproj` depuis le dépôt. Le fichier `ew.dev.json` initialise Alice, Bob et Carol avec des permissions explicites ; choisir une identité dans le sélecteur. Aucun SMTP n’est activé par défaut. `ew dev --reset` archive l’ancien environnement si son seed doit être remplacé.

`ew pack --project samples/LeaveRequest.Module/EnterpriseWorkflow.LeaveRequest.Module.csproj --output ./artifacts` produit le package installable ; `ew verify ./artifacts/sample.leave-request/1.6.0` vérifie son intégrité et ses déclarations. Le [script navigateur](../../scripts/qualify-l9-browser.sh) utilise une copie isolée du module et la [capture SMTP Docker](../../tests/smtp/README.md).


Le state DTO `LeaveRequestState` est annoté et validé par `LeaveDatesValidator`. `LeaveRequestInitializer` mappe explicitement les entrées, et collecte `documents` sans en dupliquer les références dans l’état : formulaires et state restent indépendants. Le champ fichier accepte trois justificatifs bornés ; la clôture génère un fichier texte via l’API médias, attaché au commit et disponible dans la bibliothèque selon les droits. La note personnelle est cachée dans le détail. Les anciennes releases déjà déployées restent nécessaires pour les anciennes instances ; ce changement de source n’en modifie pas les artefacts.

## Lancement natif .NET

Depuis la racine du dépôt, sans copie de sources ni Python :

```bash
dotnet build src/EnterpriseWorkflow.Cli/EnterpriseWorkflow.Cli.csproj --configuration Release
dotnet src/EnterpriseWorkflow.Cli/bin/Release/net10.0/EnterpriseWorkflow.Cli.dll dev samples/LeaveRequest.Module --environment recette --configuration Release --url http://localhost:5099
```

Le CLI utilise `.ew/environments/recette` dans le projet du module. Les anciennes données `.ew/dev` restent conservées. À chaque modification de sources, une génération locale immutable est produite automatiquement ; les instances existantes restent sur leur génération et les nouvelles utilisent la génération sélectionnée. Aucune modification manuelle de release n’est nécessaire pendant ce développement.

```bash
# Avec le tool ew installé depuis les packages locaux :
ew dev .
ew dev . --environment recette-rh
ew dev . --environment recette-rh --reset
ew dev --artifact ./artifacts/sample.leave-request/1.6.0 --environment qualification --config ./ew.dev.json
```

`--reset` archive uniquement l’environnement sélectionné et sa base possédée. `--artifact` ne compile pas et ne surveille pas les sources ; les flags CLI `--configuration` et `--no-watch` sont refusés dans ce mode. L’artefact ciblé est copié et vérifié dans le registre local. Les anciennes générations nécessaires à l’historique ou aux instances restent disponibles ; les versions simplement présentes sur disque ne sont pas chargées.

Les générations locales portent une release `…-ewdev.gN` et une révision interne réservée ; elles sont refusées par `ew verify` et par le Kernel de production. Pour une distribution réelle, utiliser `ew pack`, qui conserve les métadonnées de release déclarées par l’auteur.

La configuration v2 choisit le défaut avec `defaultEnvironment`, les réglages communs avec `build`, `server`, `seed`, `smtp`, `serialization`, et les adaptations sous `environments`. Priorité : CLI > environnement choisi > commun > défauts. Les noms sont normalisés sans distinction de casse et doivent être uniques. Les bases externes explicites ne sont pas archivées par un reset.

Le sample initialise son DTO avec le service DI `IWorkflowSerializer` ; la confirmation retourne `HumanTaskCompletionResult.Success()` sans outcome. La transition appartient à l’action définie dans le processus.
