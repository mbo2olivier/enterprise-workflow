# Enterprise Workflow

Framework de workflows réutilisable en C#/.NET 10 : moteur durable, SDK, application d’accueil modulaire et, dans une livraison suivante, Studio pour développeurs.

**Statut : conception préalable à l’implémentation.** Ce dépôt contient la documentation ; aucun package, binaire ni API n’est disponible. La licence open source reste à choisir avant publication.

Le produit vise Windows, Linux et macOS, avec SQLite et Oracle dans le MVP. Chaque organisation possède sa propre installation. Le support sera qualifié par combinaison OS, architecture CPU et adaptateur.

Le MVP suit un graphe séquentiel à décisions exclusives, sans boucles ni parallélisme. Le projet sera hébergé sur GitHub et utilisera GitHub Actions pour la première CI.

## Parcours de lecture

1. [Cadrage et décisions à clarifier](docs/cadrage.md)
2. [PRD](docs/prd.md)
3. [Architecture](docs/architecture/overview.md)
4. [ADR](docs/adr/README.md)
5. [Plan d’implémentation](docs/implementation-plan.md)

## Documents de référence

- [Modèle et exécution durable](docs/architecture/runtime.md)
- [Persistance SQLite et Oracle](docs/architecture/persistence.md)
- [Sécurité et administration](docs/architecture/security.md)
- [Modules, SDK, formulaires et Studio](docs/architecture/extensions.md)
- [Compatibilité et distribution](docs/compatibilite.md)
- [Stratégie de tests](docs/testing.md)
- [Exploitation](docs/operations.md)
- [Traçabilité](docs/traceability.md)
- [Conventions proposées](docs/contributing.md)
- [Glossaire](docs/glossary.md)

Les décisions **acceptées** proviennent des réponses explicites du porteur. Les mécanismes **proposés** restent à valider ; les points **en attente** ne sont pas résolus par défaut.
