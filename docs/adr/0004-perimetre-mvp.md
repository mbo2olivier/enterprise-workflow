# ADR 0004 — Moteur, Kernel et CLI avant Studio

Date : 30 septembre 2026.

**Statut : Acceptée pour le séquencement ; détails du moteur proposés.**

## Contexte

Le produit inclut Runtime, SDK, Kernel et Studio, mais la durabilité doit être démontrée tôt.

## Décision ou proposition

MVP : moteur durable, tâches humaines, formulaires simples, Kernel modulaire, CLI et administration/sécurité ajoutées au cadrage. Studio dans une livraison suivante. Le SDK C# doit fonctionner sans Studio.

## Options considérées

Développer d’abord le designer retarderait la preuve de reprise. Supprimer le Studio de la vision contredirait le besoin.

## Conséquences

Le modèle canonique prépare l’export visuel ; ni palette ni canvas nécessaires à la première tranche. Le périmètre graphe séquentiel à décisions exclusives, sans cycles ni parallélisme, est accepté dans l’ADR 0007.

## Validation attendue

Parcours métier complet avec arrêt/reprise, puis export compilable en phase Studio. Voir [PRD](../prd.md).
