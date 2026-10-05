# ADR 0022 — Loader définitif, rétention des artefacts et migrations d’état L7b

Date : 5 octobre 2026. **Statut : acceptée.** Source : confirmation de G3-A puis validation explicite de M1-A et M2-A par le porteur.

## Contexte

L7a démontre que deux versions d’un module, de ses dépendances privées et de ses composants Razor peuvent coexister. L7b doit rendre ce modèle exploitable au démarrage, empêcher la disparition du code exact d’une définition publiée et clore la décision différée sur les évolutions de `WorkflowState.SchemaVersion`.

## Décisions

- **G3-A** : la coexistence côte à côte est le modèle nominal. Les changements sont activés uniquement au redémarrage ; aucun hot reload ou remplacement silencieux n’est introduit.
- **M1-A** : une migration d’état est une transformation explicite, déclarée par l’artefact exact sous forme d’une arête source → cible. Une commande administrative exécute une seule arête. La transformation s’effectue hors transaction et sans effet externe ; seul son résultat est committé atomiquement sous contrôle de la révision, du schéma source et de l’absence de bail worker actif. Un échec ne modifie rien et reste relançable. Le commit avance la révision et écrit l’audit `StateMigrated`.
- **M2-A** : l’inventaire des artefacts installés est durable dans le store. Au démarrage, le Kernel réconcilie la configuration avec cet inventaire. Toute définition persistée conserve son droit à être démarrée : son artefact exact doit donc rester configuré. Un identifiant/version ne peut jamais être remplacé par un autre hash. Un retrait non référencé est permis ; un retrait référencé bloque la readiness.
- L’interface administrative `IWorkflowMaintenanceStore` reste séparée de `IWorkflowStore`, utilisé par les workers. Les migrations de base restent explicites et ne sont jamais lancées implicitement par le loader.

## Alternatives écartées

La migration paresseuse dans un worker mélange évolution de données, acquisition de bail et exécution métier. Les scripts SQL externes ne donnent pas un contrat portable SQLite/Oracle et contournent validation et audit. Ne protéger que les instances non terminales permettrait de publier ou restaurer une définition dont le code a disparu. Un inventaire local hors base ne survivrait pas correctement à une restauration ou à un changement de serveur.

## Conséquences

Les modules implémentent désormais le contrat définitif `ConfigureStateMigrations`. `WorkflowModuleKernel.ReconcileAsync` doit réussir avant readiness. Les définitions historiques bloquent volontairement la suppression de leurs artefacts jusqu’à l’introduction future d’un retrait explicite de définition et d’une politique de sauvegarde/rétention. Les tables `EwModuleArtifacts` et `EW_MODULE_ARTIFACTS` appartiennent au framework.

## Validation

Chargement A puis A+B après reconstruction du catalogue, résolution d’un migrateur depuis l’artefact A exact, migration 1 → 2, conflit de révision, refus sous bail actif, audit atomique, hash remplacé refusé et retrait d’un artefact référencé bloqué sur SQLite et Oracle.
