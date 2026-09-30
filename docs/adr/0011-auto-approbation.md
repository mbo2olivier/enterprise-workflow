# ADR 0011 — Auto-approbation configurable, interdite par défaut

**Statut : acceptée.** Source : réponse « 1A » du porteur du projet.

## Contexte

La séparation des organisations ne détermine pas si le demandeur peut approuver sa propre demande. Certains workflows peuvent nécessiter une exception, tandis que le comportement par défaut doit interdire cette auto-approbation.

## Décision

L’auto-approbation est configurable par workflow et interdite par défaut. L’absence de configuration explicite ne permet donc pas au demandeur d’approuver sa propre demande.

Cette règle concerne les tâches d’approbation, pas les tâches de saisie ou de correction normalement réalisées par le demandeur. Autoriser l’auto-approbation ne dispense ni de l’affectation à la tâche ni des permissions ordinaires.

## Options considérées

Interdiction universelle : trop restrictive pour les workflows qui autorisent ce comportement. Absence de règle par défaut : ne fournit pas le comportement retenu par le porteur.

## Conséquences

Le contrôle doit être appliqué côté serveur à la soumission, indépendamment de l’UI et du fournisseur d’identité. Les sujets sont comparés par identité stable, pas par nom affiché.

Détails proposés pour le contrat : marquer explicitement les tâches d’approbation et porter la politique effective dans la définition versionnée. Ainsi, une nouvelle version de workflow ne change pas silencieusement la règle des instances en cours. Le schéma exact sera fixé avant L6. La délégation n’est pas définie par cet arbitrage et ne doit pas constituer un contournement implicite.

## Validation attendue

Sans configuration, le demandeur ne peut approuver ; une autorisation explicite du workflow permet cette action seulement si les autres droits et l’affectation sont satisfaits. La saisie initiale reste possible. Tester également l’appel direct à l’API et la conservation de la politique des instances en cours.
