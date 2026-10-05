# Preuve du lot L7b

Date : 5 octobre 2026. Décisions G3-A, M1-A et M2-A consignées dans l'[ADR 0022](../adr/0022-loader-definitif-et-migrations-etat-l7b.md).

## Réalisation

- contrat définitif `IWorkflowModule` avec handlers, services et migrations d’état explicites ;
- catalogue startup-only conservant un `AssemblyLoadContext` non collectible par artefact exact ;
- registre immuable des migrations indexé par `(ModuleId, Version, Sha256, SourceSchemaVersion, TargetSchemaVersion)` ;
- `WorkflowModuleKernel.ReconcileAsync` comme porte de readiness ;
- `IWorkflowMaintenanceStore`, séparé du store worker ;
- inventaire durable `EwModuleArtifacts` / `EW_MODULE_ARTIFACTS` et migrations provider ;
- migration d’état en un seul pas, compare-and-swap sur révision/schéma, refus sous bail actif et audit `StateMigrated` atomique ;
- sélection des fixtures alignée sur la configuration Debug/Release courante pour préserver la CI propre.

## Vérifications locales acquises

- builds Release des projets de solution et des tests Oracle : 0 avertissement, 0 erreur ;
- solution : **92/92 tests réussis** ;
- tests SQLite : **9/9**, incluant retrait référencé bloqué, remplacement de hash refusé, migration atomique/auditée, conflit de révision et refus sous bail actif ;
- tests extensions : **4/4**, incluant redémarrage A puis A+B, résolution du migrateur par artefact exact et absence de commit lorsqu’un migrateur échoue ;
- sample `ExecutableWorkflow` : terminé avec persistance du service, de la tâche humaine et de l’effet externe.

## Qualification Oracle Enterprise 19.19 acquise

Le conteneur officiel `container-registry.oracle.com/database/enterprise:19.19.0.0` est actif et sain. Après remise en cohérence et déverrouillage du compte de test jetable `EWTEST`, la suite Oracle Release réussit **12/12 tests**. Le scénario L7b qualifie sur le vrai provider l’application de la migration `202610050005_AddModuleArtifacts`, l’inventaire, le retrait référencé bloqué et la migration d’état 1 → 2. Le secret du conteneur a été utilisé en mémoire avec autorisation ponctuelle, sans affichage ni stockage.

L7b satisfait donc son critère de sortie local sur les deux providers prioritaires. La matrice GitHub Actions reste la confirmation distante multi-OS des tests sans Oracle.
