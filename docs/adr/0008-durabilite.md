# ADR 0008 — Transitions atomiques et exécution au moins une fois

Date : 30 septembre 2026.

**Statut : Proposée.**

## Contexte

Une instance doit survivre aux interruptions sans doublons de transitions persistées. Un effet externe peut avoir réussi avant un crash.

## Décision ou proposition

Store atomique, travaux persistés, baux renouvelables et générations de possession ; exécution hors transaction longue, commit conditionnel. Idempotence des commandes et clés d’effets stables ; outbox pour intentions de livraison. Worker à polling, sans broker obligatoire.

## Options considérées

File mémoire seule perd les réveils. Transaction SQL durant un appel distant ne rend pas l’effet atomique. Exactly-once externe et transaction distribuée ne sont pas des garanties réalistes du MVP.

## Conséquences

Des tentatives et livraisons peuvent se répéter ; les nœuds métier doivent gérer ce contrat. Timers non temps réel. La rétention des reçus et les valeurs de retry/bail doivent être fixées avant stabilisation.

## Validation attendue

Injection de crash avant/après effet et commit, worker périmé, double complétion, annulation concurrente, réponse perdue et retry. Voir [tests](../testing.md).
