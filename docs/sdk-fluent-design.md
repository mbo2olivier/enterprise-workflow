# Conception du SDK fluent — cible L8b

Date : 20 octobre 2026. **Document de conception, API indicative non implémentée.** Les principes acceptés et les points ouverts sont distingués dans l’[ADR 0024](adr/0024-sdk-fluent-permissions-affectation.md). Le séquencement figure dans le [plan L8b](implementation-plan.md).

## Exemple de permissions et d’une tâche

```csharp
// Point de déclaration proposé dans IWorkflowModule.
public void ConfigurePermissions(WorkflowPermissionRegistryBuilder permissions)
{
    permissions.Add("hr.review", "Examiner les demandes de congé");
    permissions.Add("hr.override", "Autoriser une dérogation RH");
    permissions.Add("manager.approve", "Statuer comme responsable");
}

// Syntaxe proposée ; la conjonction stage/action reste à confirmer (P2).
process.Approval("validation-rh", "Validation RH", stage => stage
    .RequiresPermission("hr.review")
    .ClaimedByEligibleParticipant()
    .Actions(actions =>
    {
        actions.Add("validate", "Valider", action => action
            .SelectAssigneeFor("validation-manager")
            .Then("validation-manager"));

        actions.Add("decline", "Refuser", action => action
            .Then("rejected"));

        actions.Add("override", "Dérogation", action => action
            .RequiresPermission("hr.override")
            .SelectAssigneeFor("validation-manager")
            .Then("validation-manager"));
    }));

process.Approval("validation-manager", "Validation manager", stage => stage
    .RequiresPermission("manager.approve")
    .AssignedFromPreviousTask()
    .Actions(actions =>
    {
        actions.Add("validate", "Valider", action => action.Then("close-request"));
        actions.Add("decline", "Refuser", action => action.Then("rejected"));
    }));

process.Service<CloseRequestHandler>("close-request").Then("completed");
process.End("completed");
process.End("rejected");
```

L’extrait montre une partie d’un processus, pas un graphe complet compilable. `AssignedFromPreviousTask` et `SelectAssigneeFor` sont des noms proposés : la première méthode décrit la politique cible, la seconde l’interaction exigée sur la source. Le sélecteur n’est obligatoire que pour l’action qui rejoint cette cible. Le compilateur doit vérifier la correspondance et refuser une désignation manquante sur un chemin concerné.

Une branche peut traverser des étapes techniques avant la cible : soit le SDK limite explicitement cette première version à une destination humaine directe, soit il valide le chemin concerné. Il ne doit pas accepter une cible arbitraire au seul motif que son identifiant existe. Aucun retour au stage courant n’est généré ; le graphe reste acyclique.

## Formulaires métier et formulaires intégrés

Un formulaire métier est déclaré indépendamment du registre des handlers mais porte toujours `.HandledBy<THandler>()`. Le handler doit être enregistré et résolu sans ambiguïté. Une surcharge par tâche est proposée en P1 ; elle n’est pas supposée acquise.

Dans l’extrait, les approbations sans `UsesForm` utilisent un formulaire et un handler intégrés versionnés. Un remplacement explicite peut être fourni avec `.UsesForm("manager-review")`. Le comportement du commentaire, du motif obligatoire et du résumé est ouvert en O5. La sélection d’un destinataire est une capacité du contrat de tâche, rendue par le formulaire intégré ou l’adaptateur du composant personnalisé ; elle n’est pas un champ libre d’identité.

Un nœud de décision automatique n’utilise pas ce formulaire humain : son évaluateur reste enregistré comme handler de décision. Le SDK ne doit pas confondre ces deux sens du mot décision.

## Lecture des conventions

- Le module porte sa release et sa `ContractRevision` ; les contrats héritent de cette révision sauf référence explicite. La version du schéma initial est obligatoire. Les noms exacts des méthodes de déclaration restent à stabiliser.
- Les chaînes locales sont qualifiées selon leur catégorie. Le nom de classe n’est jamais une identité durable implicite.
- `RequiresPermission` décrit une exigence métier, pas une attribution aux personnes. L’administration conserve la maîtrise des profils et identités ; le modèle d’évaluation entre permissions et grants exacts reste ouvert en O2.
- Le claim réserve atomiquement une tâche à un candidat habilité. La notification SMTP ne réserve rien et ne donne aucun droit.
- Une tâche n’ayant aucun destinataire à choisir pour la suite conserve néanmoins sa propre politique d’affectation.
- `AssignedToInitiator()` est proposé pour la confirmation de la demande : cette tâche est une saisie/confirmation, pas une approbation contournant l’interdiction d’auto-approbation. Arbitrage O4.

## Revue et validation

L’aperçu compilé doit montrer les IDs qualifiés, révisions et artefacts exacts, le formulaire/handler effectif, les permissions, les politiques d’affectation et toutes les transitions. Les valeurs héritées doivent y être visibles. Cet aperçu permet de relire les conventions sans multiplier les paramètres dans le code source.

Les erreurs doivent mentionner processus, stage, action et référence locale concernés. Collecter les déclarations avant validation évite de rendre leur ordre significatif. Vérifier références absentes, permissions inconnues, ambiguïtés de handler, étapes inaccessibles, cycles et branches sans destination. Une erreur technique d’exécution n’est jamais transformée en refus métier par `Otherwise`.
