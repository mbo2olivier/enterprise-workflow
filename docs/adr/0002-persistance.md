# ADR 0002 — SQLite et Oracle 19c minimum

Date : 30 septembre 2026.

**Statut : Acceptée pour les SGBD ; architecture EF proposée.**

## Contexte

L’intégration à un DbContext est demandée sans coupler le domaine à un SGBD. Le MVP doit couvrir une base locale et l’environnement Oracle.

## Décision ou proposition

SQLite et Oracle font partie du MVP. Oracle 19c est la version minimale cible. Proposer des contrats atomiques communs, un socle EF Core 10 et des adaptateurs/migrations spécifiques. Le patch provider sera fixé après prototype.

## Options considérées

SQLite uniquement simplifierait la tranche initiale mais ne satisferait pas le MVP. Un seul SQL commun masquerait les différences de concurrence. Un accès direct au provider depuis Core compromettrait l’extension.

## Conséquences

Qualification réelle de chaque provider, y compris 19c ; coût de CI et provisionnement Oracle à prévoir. Support de versions ultérieures ajouté sur preuves. Contexte dédié Kernel et mapping embedded proposés, sans promesse d’atomicité métier externe.

## Validation attendue

Tests de claim, commit conditionnel, reprise, migrations et double complétion sur les deux bases. Voir [persistance](../architecture/persistence.md).
