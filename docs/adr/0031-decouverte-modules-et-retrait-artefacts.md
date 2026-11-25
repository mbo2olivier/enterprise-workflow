# ADR 0031 — découverte des modules et retrait des anciens artefacts

Date : 24 novembre 2026. Statut : décisions produit acceptées par le porteur ; mécanismes détaillés L14 à compléter avant code. L13 est implémenté localement le 24 novembre 2026, qualification lourde en attente ; L14 reste non implémenté.

## Contexte

La configuration actuelle liste chaque répertoire d’artefact. La réconciliation exige les artefacts de toutes les définitions publiées, y compris quand les instances sont closes. Le porteur demande une découverte depuis une racine et la possibilité de retirer le code ancien tout en gardant l’historique.

## Décisions

- **L13** : découverte au démarrage sous `Modules:RootDirectory`. Si cette configuration est définie, ignorer intégralement `Modules:Directories`. Sans racine configurée, garder le mode historique comme fallback. Une erreur de racine ne réactive pas la liste.
- Réutiliser le chargeur, ses contrôles d’intégrité et sa compatibilité runtime. Recherche récursive bornée, sans suivre de liens ni descendre dans les dépendances d’un candidat. Pas de chargement à chaud. Les incompatibles intègres sont exclus avec diagnostic ; corruption et ambiguïtés bloquent le démarrage.
- **L14** : séparer les artefacts nécessaires à l’exécution et les références historiques. Une définition conservée n’impose plus seule de charger son code ; tout traitement encore exécutable impose son artefact exact. Préserver les références historiques et les hashes.
- Une consultation générique autorisée en lecture seule doit fonctionner sans ancien code. Préparer les historiques existants avant d’autoriser un retrait qui rendrait leur consultation impossible. Aucun élargissement implicite des permissions, aucune purge automatique.
- **L15** : Studio et export C#, avec les exigences antérieures ; les lots clos ne sont pas rouverts.

## Portée et conséquences

Cette décision complète l’ADR 0022 pour la politique de rétention et l’ADR 0030 pour la composition d’hôte, sans réécrire les preuves historiques. L13 conserve la rétention actuelle jusqu’à livraison de L14. Les migrations, snapshots et transitions nécessaires à L14 sont à définir avant son implémentation ; le retrait ne se réduit pas à filtrer les instances sur leur statut.

Références : [L13](../l13-spec.md), [L14](../l14-spec.md), [plan](../implementation-plan.md). Les tests lourds restent délégués au porteur.
