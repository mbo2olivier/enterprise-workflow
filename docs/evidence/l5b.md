# Preuves L5b — habilitations de stages

Date : 4 octobre 2026. R2-A, R3-A, R5-A, R6-A et R7-A sont confirmés dans l’ADR 0019.

## Livrables

- contrats typés `WorkflowActionId`, `WorkflowAuthorizationScope`, destinataires identité/profil et grants versionnés ;
- actions framework distinctes pour cycle de vie, lecture et tâches ;
- `WorkflowActionCatalog` exact et refus des actions non déclarées ;
- `InternalWorkflowAuthorizationService`, sans fallback depuis une permission globale ;
- `WorkflowAccessAdministration` protégée par `security.access.manage` ;
- recherche de candidats filtrée par workflow/version/nœud/action ;
- grants positifs exacts, refus par défaut, révocation immédiate et révision globale atomique ;
- audit des ajouts/retraits, endpoints d’exemple et configuration du catalogue ;
- migrations `202610040006_WorkflowAccessSqlite` et `202610040007_WorkflowAccessOracle`.

## Schéma

- `EwWorkflowAccessGrants` persiste la portée exacte, le destinataire stable et la révision de création ;
- `EwWorkflowPolicyState` porte la révision globale courante, incrémentée atomiquement avec chaque mutation ;
- le nœud interne `$workflow` représente les actions de niveau workflow/instance et évite la normalisation des chaînes vides vers `NULL` par Oracle ; aucun `StageId` n’est ajouté.

## Résultats

- build Release complet et projet Oracle hors solution : zéro avertissement, zéro erreur ;
- suite du socle et SQLite : 79/79 tests réussis ;
- Oracle Enterprise 19.19 ARM64 officiel : 9/9 tests réussis, incluant migration, grant exact, refus d’une autre action, révocation et audit ;
- cas couverts : version/nœud/action croisés, identité directe, profil direct, groupe courant, annuaire indisponible sans perte d’un grant direct, action non déclarée et filtrage des candidats.

Conformément à R6-A, L5b ne revendique ni tâche humaine durable, ni affectation persistée, ni claim, ni inbox, ni complétion. Ces contrôles et la fin de T23/T24 relèvent de L6 ; la visibilité par champ relève de L8.
