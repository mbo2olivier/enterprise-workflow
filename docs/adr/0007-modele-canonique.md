# ADR 0007 — Modèle canonique et graphe MVP borné

Date : 30 septembre 2026.

**Statut : Proposée.**

## Contexte

C# et Studio doivent produire la même sémantique sans rendre le moteur dépendant de l’éditeur.

## Décision ou proposition

Modèle canonique versionné, validate-before-publish, graphe acyclique séquentiel à décisions exclusives. Évaluateurs nommés et versionnés, données JSON/DTO ; pas de delegates persistés. C# et Studio produisent le même modèle normalisé.

## Options considérées

Interprétation directe du DSL couple auteur et moteur. Sérialisation de lambdas ou CLR arbitraire fragilise la reprise. Cycles et parallélisme immédiats nécessitent une sémantique supplémentaire.

## Conséquences

Diagnostics localisés et export déterministe. Les fonctionnalités non supportées sont rejetées. L’export du Studio transfère explicitement l’autorité vers le C# ; pas de synchronisation bidirectionnelle implicite.

## Validation attendue

Validation des erreurs de graphe, équivalence de modèles et compilation des exports. Voir [runtime](../architecture/runtime.md).
