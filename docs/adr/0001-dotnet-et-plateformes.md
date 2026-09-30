# ADR 0001 — .NET 10 et plateformes

Date : 30 septembre 2026.

**Statut : Acceptée pour la cible ; modalités de packaging proposées.**

## Contexte

Le framework doit profiter de la portabilité .NET et permettre la compilation depuis les sources. Le porteur souhaite des binaires Windows x86/x64, Linux et macOS, selon faisabilité.

## Décision ou proposition

.NET 10 ; Windows, Linux et macOS. Le porteur accepte un support distinct par adaptateur : Windows x86 et macOS Intel avec SQLite, Oracle seulement sur plateformes compatibles et qualifiées. Voir la [matrice](../compatibilite.md).

## Options considérées

.NET Standard pour les contrats, .NET antérieur ou cible Windows seule : non retenus comme cibles de départ. Une cible supplémentaire nécessitera un consommateur et une qualification.

## Conséquences

La portabilité du code métier et des dépendances natives reste à vérifier. Les RID et OS exacts, patch SDK, runners et archives seront arrêtés avant livraison. Pas de promesse « toutes les distributions Linux ».

## Validation attendue

Build et exécution des artefacts sur leurs OS/CPU annoncés ; aucun simple cross-publish utilisé comme preuve d’exécution.
