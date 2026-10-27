# SDK fluent — lot L8b

Date : 20 octobre 2026. Implémentation livrée ; qualification intégrée locale documentée en [L9](evidence/l9.md), compléments de qualification restants dans [l8b-validation.md](l8b-validation.md). Les décisions acceptées sont consignées dans l’[ADR 0024](adr/0024-sdk-fluent-permissions-affectation.md).

## Module et processus

Le [sample LeaveRequest](../samples/LeaveRequest.Module/LeaveRequestModule.cs) est la référence complète. Un module dérive de `WorkflowModule`, déclare `Info`, ses permissions dans `ConfigurePermissions`, puis ses handlers, formulaires et classes de processus dans `Configure(WorkflowModuleBuilder builder)`. Les méthodes vides du contrat de démarrage n’ont plus à être recopiées.

```csharp
public override WorkflowModuleInfo Info { get; } =
    new WorkflowModuleInfo("sample.leave-request", "1.0.0", 1)
        .WithName("Gestion des congés");
```

La release textuelle identifie l’artefact ; la révision entière est héritée des contrats nouvellement déclarés. `WithStateSchemaVersion(...)` reste obligatoire dans chaque processus. Les identifiants de processus, formulaires, handlers et permissions sont qualifiés par le module ; ceux des nœuds, champs et actions restent locaux. Le nœud d’entrée généré utilise l’ID réservé `start`.

Les déclarations sont collectées avant résolution. Un formulaire peut donc précéder son handler dans le code. Le formulaire métier porte toujours `.HandledBy<THandler>()` ; la tâche peut remplacer explicitement ce défaut. Les formulaires humains standard utilisent un handler du framework enregistré sous une clé qualifiée du module et la révision courante.

## Permissions et affectation

```csharp
process.Approval("validation-rh", "Validation RH", stage => stage
    .RequiresPermission("hr.review")
    .RequiresAnyPermission("hr.senior", "hr.deputy")
    .ClaimedByEligibleParticipant()
    .ShowFields("startDate", "endDate", "reason")
    .Actions(actions =>
    {
        actions.Add("validate", "Valider", action => action
            .SelectAssigneeFor("validation-manager")
            .Then("validation-manager"));
        actions.Add("decline", "Refuser", action => action
            .RequireComment()
            .Then("rejected"));
    }));
```

Chaque appel ajoute une exigence cumulative. Ici il faut `hr.review` ET (`hr.senior` OU `hr.deputy`). Les exigences de l’action s’ajoutent à celles du stage. L’administration attribue les permissions aux profils/identités ; une attribution vaut pour les futures versions exigeant la même permission. Les anciennes définitions sans politique fluent conservent leurs grants exacts ; aucun grant historique n’est converti automatiquement. Le Kernel compose `ModulePermissionAuthorizationService` avec le fournisseur historique. Un hôte embarqué doit composer la même frontière.

Le processus déclare sa permission de démarrage avec `RequiresPermission`. La lecture complète d’instance et l’annulation ont leurs déclarations distinctes `ReadableByPermission` et `CancellableByPermission`. La lecture des champs reste bornée par `ReadableBy` ; `ShowFields` refuse les champs inconnus ou non lisibles par un participant. Le statut d’administrateur ne donne aucun droit métier implicite.

Une tâche humaine déclare exactement une politique : `ClaimedByEligibleParticipant`, `AssignedFromPreviousTask` ou `AssignedToInitiator`. La dernière concerne une saisie/confirmation, pas une approbation. Une personne habilitée à au moins une action peut prendre une tâche de pool ; les autres actions restent masquées et refusées côté serveur. Elle peut libérer son claim. `AllowInitiator` et `DistinctFrom` permettent de déclarer explicitement les contraintes d’acteurs supplémentaires.

La sélection du destinataire n’est requise que pour l’action qui rejoint directement la tâche cible. Le sélecteur utilise cette action source et les permissions de la cible ; il ne nécessite pas un profil connu du module. Les contrôles sont répétés côté serveur. L’enveloppe réservée `$assignments` participe au hash d’idempotence et est retirée avant d’appeler le handler métier. Une sélection différente sous la même clé est un conflit.

Si le candidat perd ses droits avant activation, la tâche reste `AwaitingAssignment`. Aucune personne n’est substituée. La commande `AssignAsync` et le formulaire de récupération permettent une affectation explicite, soumise au droit d’administration des accès et à l’éligibilité du candidat. L’accès à la page et aux données demeure soumis aux droits métier de lecture ; un administrateur sans ces droits n’obtient pas les données du dossier.

## Formulaires et résultats

Les champs utilisent `Text`, `LongText`, `Date`, `Choice`, `Number`, `Boolean`, puis `Required`, `Length`, `Options`, `Help` et `ReadableBy`. Le défaut de visibilité est limité à l’initiateur ; un formulaire peut déclarer un défaut explicite pour ses champs avant `Fields(...)`.

Sans `UsesForm`, une tâche humaine reçoit un formulaire standard avec commentaire facultatif et actions déclarées. `RequireComment` impose un commentaire non blanc pour l’action concernée, y compris en appel runtime direct. `ShowFields` décrit le résumé autorisé séparé de la soumission. Un formulaire métier ou un handler de complétion peut être substitué. Le handler standard ne remplace pas l’état métier et ne déclenche aucun effet externe ; un traitement qui doit conserver un commentaire dans les données métier utilise un handler explicite.

La soumission du formulaire initial est validée côté serveur puis crée l’instance. Le handler par défaut d’un formulaire est le handler de complétion des tâches qui l’utilisent ; il n’est pas invoqué artificiellement comme une tâche humaine lors du démarrage. Une étape ultérieure nommée `draft` est une confirmation métier ordinaire.

Les transitions ciblent des étapes. Une opération automatique est un `Service<THandler>(id).Then(destination)` ; une fin est déclarée avec `End`. Le SDK historique reste disponible pour les contrats avancés, notamment décisions automatiques et timers. Les erreurs techniques ne deviennent pas des refus métier. Le graphe reste acyclique, séquentiel et à branches exclusives.

## Réutilisation et manifeste

`UseHandler<THandler>(reference, owner)` et `UseForm(localId, definition, handler, owner)` importent un contrat depuis un artefact exact, sans le réenregistrer comme nouveau propriétaire. Le loader vérifie le propriétaire annoncé. `UseProcess(descriptor)` réexpose la définition immuable du descripteur, sans la reconstruire avec l’artefact courant. Tous les artefacts référencés doivent rester disponibles ; aucun fallback de version n’est effectué.

`WorkflowModuleManifestWriter.Create(module, assemblyPath, dependencyPaths, resources)` produit identité, release, composants et hashes depuis la déclaration auteur. Il ne copie pas les fichiers ni ne remplace la future CLI L9. Le loader vérifie également la cohérence entre `Info` et le manifeste. `WithComponent<TComponent>()` peut générer un ID de composant ; un ID explicite est également accepté.

## Format durable et revue

Les politiques fluent sont conservées dans la propriété réservée `$workflow` de la configuration canonique des nœuds, avec une version de contrat propre. Le compilateur contrôle leur forme, la cohérence des actions et des affectations ainsi que les désignations sur chaque chemin entrant. Une définition ne peut pas utiliser des tâches à permissions tout en conservant un démarrage implicitement historique.

Ce format ne modifie pas les octets ou hashes des anciennes définitions. Le JSON canonique de la définition constitue l’aperçu déterministe : références qualifiées, révisions, artefacts, permissions, affectations, actions et transitions. Aucun type CLR ou callback d’auteur n’est sérialisé comme comportement exécutable.

## Boucle de développement L9

`ew dev` compile le module et prépare un artefact local pour un hôte de développement avec SQLite et UI, sans projet Kernel à écrire. Il partage les règles de manifeste et de validation de `ew pack`, préserve les artefacts exacts et permet de tester les identités et parcours L8b. `--module-directory` fournit explicitement les propriétaires de contrats externes réutilisés. Voir le [guide CLI](l9-cli.md), la [spécification L9](l9-spec.md) et la [preuve locale](evidence/l9.md) ; aucune qualification CI L9 n’est présumée.

## Complément accepté le 25 octobre 2026 — state DTO en L9b

L’[ADR 0026](adr/0026-etat-metier-type-et-validation.md) complète cette conception : le processus déclare un DTO d’état métier annoté via une façade de type `WithState<TState>()`, tout en conservant la version explicite et les migrations actuelles. Les attributs portent libellés, visibilité du détail et validation permanente/contextuelle ; les permissions des champs complètent l’accès à l’instance. Les formulaires restent indépendants : leurs handlers réalisent explicitement le mapping vers le state, y compris au démarrage. Aucune copie implicite des entrées ni seconde projection métier persistée. Les signatures restent à stabiliser ; voir la [spécification L9b, §1.1–1.3](l9b-spec.md) pour les règles et critères de qualification. Ce complément est implémenté localement en L9b ; voir le [guide des API effectives](l9b-sdk.md) et la [qualification](evidence/l9b.md).
