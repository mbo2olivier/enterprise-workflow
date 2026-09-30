# ADR 0010 — Distribution par RID et qualification explicite

Date : 30 septembre 2026.

**Statut : Proposée pour le packaging ; périmètre plateformes accepté.**

## Contexte

Les utilisateurs doivent pouvoir compiler ou utiliser des versions préconstruites sans ajout gratuit d’infrastructure.

## Décision ou proposition

Proposer packages bibliothèques, modèles/CLI et archives Host par RID qualifié. Commencer par publications non trimmées, sans Native AOT, pour préserver chargement dynamique. Évaluer archives autonomes et dépendantes du runtime avec coûts de mise à jour explicites.

## Options considérées

Un seul exécutable universel masque les dépendances natives. Native AOT/trimming sans qualification des extensions serait risqué. Docker uniquement contredirait les objectifs d’hébergement.

## Conséquences

Versions des dépendances, checksums, restrictions, procédure de build et licences tierces accompagnent les releases. Pas de publication NuGet ou GitHub Release durant le cadrage. Licence du projet et noms publics à choisir.

## Validation attendue

Smoke tests des archives sur leur plateforme, chargement d’un module externe, providers revendiqués testés. Voir [compatibilité](../compatibilite.md).
