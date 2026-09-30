# ADR 0006 — Runtime bibliothèque et Kernel de composition

Date : 30 septembre 2026.

**Statut : Proposée.**

## Contexte

Le moteur doit être intégrable et aussi distribué dans une application modulaire. Le terme Runtime a été utilisé pour les deux responsabilités.

## Décision ou proposition

Séparer le Runtime durable de l’exécutable Kernel/Host. Core et contrats purs sans EF, ASP.NET, LDAP ou Razor. Le Host compose services applicatifs, sécurité, UI, stockage et loader. Les contrats d’intégration sont séparés des contrats métier purs.

## Options considérées

Un monolithe Host/Runtime simplifierait le départ mais rendrait l’embarquement et les tests indépendants plus difficiles. Une architecture en microservices introduirait une exploitation non requise.

## Conséquences

Plusieurs bibliothèques à responsabilités précises ; création progressive seulement. Règles de références contrôlées en CI. Les choix de sécurité ne contaminent pas le modèle de workflow.

## Validation attendue

Exemple embedded et Kernel réutilisant le même moteur ; vérification des références interdites. Voir [architecture](../architecture/overview.md).
