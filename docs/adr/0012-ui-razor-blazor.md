# ADR 0012 — UI Razor/Blazor et formulaires extensibles

**Statut : acceptée.** Source : réponse « 2A » du porteur du projet.

## Contexte

Le Kernel doit proposer une UI métier et une administration intégrée, avec des formulaires simples et des extensions pour les cas métier particuliers.

## Décision

Utiliser Razor/Blazor pour l’UI métier et l’administration. Fournir des formulaires déclaratifs simples et permettre des composants Razor personnalisés.

## Options considérées

Une autre technologie frontend ou le report du choix ont été proposés ; le porteur a retenu Razor/Blazor.

## Conséquences

Les composants UI restent dans les packages d’intégration ; le Core ne dépend pas de Razor ou Blazor. Les formulaires personnalisés conservent la validation et l’autorisation côté serveur.

Le mode de rendu précis, le chargement des ressources de modules et l’outillage frontend de build restent à préciser au prototype. Cet arbitrage ne sélectionne pas la bibliothèque graphique du Studio, dont la livraison reste postérieure au MVP.

## Validation attendue

Parcours métier et administration utilisables avec formulaires déclaratifs ; module exposant un composant personnalisé et ses ressources ; tests de permissions, validation, navigation clavier et thèmes. Aucun changement du moteur requis pour ajouter un formulaire personnalisé.
