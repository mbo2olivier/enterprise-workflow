# ADR 0005 — Une installation par organisation

Date : 30 septembre 2026.

**Statut : Acceptée.**

## Contexte

Les succursales peuvent réutiliser des workflows tout en conservant leur propre administration.

## Décision ou proposition

Une installation logique indépendante par organisation ; déploiement des modules nécessaires dans chaque installation. Aucune multi-tenancy dans le moteur du MVP.

## Options considérées

Base partagée avec TenantId et administration centrale : non retenues. Une même installation pour toutes les organisations ne répond pas à la séparation demandée.

## Conséquences

Données, configuration, accès et secrets propres à chaque installation. Un serveur Oracle physique peut éventuellement héberger des schémas séparés si l’exploitation les isole ; pas de partage implicite des tables. Copier un module ne copie ni identités ni instances. Les droits intra-organisation restent nécessaires.

## Validation attendue

Deux installations avec le même module et des données/configurations indépendantes ; permissions propres à chacune.
