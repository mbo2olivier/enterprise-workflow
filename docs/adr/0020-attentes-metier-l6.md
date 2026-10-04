# ADR 0020 — attentes métier durables du lot L6

Date : 4 octobre 2026. **Statut : acceptée.** Source : arbitrages explicites D1-B et D2-A à D7-A du porteur.

## Contexte

L5b sait déterminer si une identité est habilitée sur une action exacte de workflow/nœud, mais il ne crée ni tâche humaine, ni affectation, ni claim, ni timer. L6 doit ajouter ces attentes sans conserver de transaction ou de worker pendant leur durée, et sans confondre une interaction humaine avec le futur modèle de callback technique de G1a.

Le framework n'a encore aucune version officielle taguée ni aucun usage à préserver. Le schéma canonique `schemaVersion = 1` peut donc encore être corrigé directement, à condition de mettre à jour dans le même changement les samples, snapshots et tests de tous les providers.

## Décisions

### D1-B — schéma canonique v1 corrigé avant le premier tag

`HumanTask` et `Timer` deviennent des rôles et contrats typés du schéma canonique v1. Le numéro de schéma reste `1` jusqu'au premier tag officiel. À partir de ce tag, toute évolution incompatible devra suivre une politique de compatibilité et de migration explicite. Aucun lecteur double ni mode legacy n'est ajouté maintenant.

### D2-A — cycle de vie et affectation explicites

Une tâche désignée suit `AwaitingAssignment → Assigned → Completed|Cancelled`. Une tâche de pool suit `Available → Claimed → Available|Completed|Cancelled`. L'affectation, le claim et la release sont atomiques et révisionnés. Le claim n'expire pas en L6 ; seul son propriétaire peut le libérer. La réaffectation, la délégation et l'escalade sont hors périmètre.

Une désignation peut être préparée au démarrage ou lors de la complétion de l'activité précédente, ou décidée par une commande `Assign` pendant l'attente. Elle est qualifiée par l'identité stable `(ProviderId, SubjectId)` et doit être autorisée sur une action applicable avant persistance.

### D3-A — formulaire et handler de complétion versionnés

Chaque tâche référence `FormId/FormVersion` et `CompletionHandlerId/CompletionHandlerVersion`. Le handler reçoit l'action, la soumission JSON canonique, l'état et le contexte durables ; il retourne une mise à jour d'état facultative, l'issue choisie et des intentions outbox facultatives. Il s'exécute hors transaction SQL, doit être déterministe et ne produit aucun effet externe direct.

### D4-A — idempotence liée à l'acteur et au contenu

Le reçu durable est indexé par `(TaskId, IdempotencyKey)` et conserve l'acteur, l'action, le hash de la requête et le résultat révisionné, sans recopier la soumission sensible. Une répétition authentifiée par la même identité et avec le même contenu retourne le résultat connu, sans réévaluer les grants courants. Toute variation d'acteur ou de contenu est un conflit.

### D5-A — timer fixe fondé sur l'horloge du store

Un timer possède sa propre entité durable. Sa définition porte une durée fixe et son échéance est calculée avec l'horloge de la base lors de l'activation. L'instance passe à `Waiting`; `FireDueTimer` est atomique et un seul concurrent gagne. L'annulation d'instance annule les timers ouverts. Les calendriers et échéances dynamiques sont différés.

### D6-A — séparation des tâches configurable et versionnée

Une tâche d'approbation interdit par défaut l'auto-approbation de l'initiateur. Elle peut en plus imposer que l'acteur soit distinct des auteurs d'actions amont nommées. La décision repose sur l'identité de l'instance et l'historique durable des tâches complétées, pas sur un cache ou un attribut d'annuaire. Une tâche sans politique explicite n'ajoute que la règle d'auto-approbation correspondant à son type.

### D7-A — inbox bornée et filtrée au moment de la lecture

L'inbox lit une page stable et bornée de tâches ouvertes. Les tâches affectées ne sont exposées qu'à leur destinataire ; les tâches de pool sont filtrées par le service d'autorisation L5b. Le scan maximal et la taille de page sont configurables et validés. Aucun snapshot de grants et aucune jointure inter-store ne sont persistés.

## Conséquences

- Le sample minimal et les snapshots canoniques sont recompilés avec le nouveau schéma v1 dans le même lot.
- SQLite et Oracle reçoivent les mêmes invariants métier par des migrations provider-spécifiques et des tests contractuels communs.
- G1a reste ouverte : une attente humaine ou temporelle n'est pas présentée comme un protocole requête/callback distant.
- L8 rendra les formulaires et l'inbox ; L6 livre leurs contrats et services indépendants de l'UI.

