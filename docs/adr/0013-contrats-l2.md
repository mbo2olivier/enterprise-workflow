# ADR 0013 — Conventions et contrats du lot L2

Date : 1er octobre 2026. **Statut : acceptée.** Source : validation explicite des décisions D1 à D6 par le porteur.

## Contexte

L1 et sa CI sont terminés. L2 doit livrer modèle canonique, validateur, DSL et contrats de stockage sans dépendre d’un serveur Oracle déjà disponible.

## Décision

Adopter les décisions [L2-D1 à L2-D6](../l2-readiness.md) :

- Identifiants techniques stables, comparaison ordinale et limite de 128 caractères ; versions de définition entières positives et immuables, schéma distinct.
- Un Start, une ou plusieurs fins, branches exclusives nommées et défaut facultatif explicite ; validation structurelle puis par catalogue.
- État JSON versionné, inchangé ou remplacé explicitement ; normalisation déterministe et empreinte SHA-256, sans fusion implicite.
- Limites configurables : définition 1 Mio / 1 000 nœuds, état 1 Mio, configuration par nœud 256 Kio, profondeur JSON 64 ; aucun tronquage.
- Store à opérations atomiques, déduplication scoped par installation/commande/acteur, conflits explicites, pas de purge automatique des reçus ; temps UTC à la milliseconde et possession vérifiée.
- Preuves de contrat en L2, qualification transactionnelle réelle et schémas physiques en L3 ; contrats révisables avant stabilisation publique.

La préparation détaillée fait partie de cette décision et précise caractères autorisés, portée des limites, diagnostics et critères de sortie.

## Options considérées

Différer tout L2 jusqu’à un accès Oracle bloquerait inutilement le modèle et le DSL. Stabiliser l’API de stockage sur un simple double mémoire masquerait les différences des providers. Le choix retenu sépare contrat et qualification réelle.

## Conséquences

Les arbitrages nécessaires au démarrage de L2 sont levés. Les durées de bail, polling et retry seront finalisées avec L4. Une base Oracle de test sera préparée avec Docker Compose, autorisé par le porteur, sans rendre Docker obligatoire pour le framework. Une version Oracle récente ne qualifie pas le minimum 19c.

## Validation attendue

Tests du modèle/DSL, normalisation, limites, transitions et déduplication ; extension des tests d’architecture au projet Persistence.Abstractions. Preuves SQLite et Oracle réelles en L3 avant stabilisation du store. Aucun code L2 ou environnement Oracle n’est déclaré réalisé par l’acceptation de cet ADR.
