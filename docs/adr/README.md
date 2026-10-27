# Registre des ADR

Les ADR 0001 à 0009 et 0011 à 0025 consignent les décisions explicites du porteur ; leurs détails encore proposés sont signalés. L'ADR 0010 n'est pas acceptée implicitement par sa rédaction. Un ADR proposé doit être validé avant le lot qu’il engage.

Une modification significative donnera lieu à un nouvel ADR qui remplace le précédent ; conserver l’historique et le motif. « Acceptée » signifie décision prise, pas fonctionnalité implémentée ou testée.

| ADR | Sujet | Statut |
| --- | --- | --- |
| [0001](0001-dotnet-et-plateformes.md) | .NET 10 et plateformes | Acceptée pour la cible ; modalités de packaging proposées |
| [0002](0002-persistance.md) | SQLite et Oracle 19c minimum | Acceptée pour les SGBD ; architecture EF proposée |
| [0003](0003-securite-extensible.md) | Sécurité par extensions et administration intégrée | Acceptée pour le principe ; obligation AD remplacée par 0018 |
| [0004](0004-perimetre-mvp.md) | Moteur, Kernel et CLI avant Studio | Acceptée pour le séquencement ; détails du moteur proposés |
| [0005](0005-isolation-organisation.md) | Une installation par organisation | Acceptée |
| [0006](0006-frontieres-architecture.md) | Runtime bibliothèque et Kernel de composition | Acceptée |
| [0007](0007-modele-canonique.md) | Modèle canonique et graphe MVP borné | Acceptée |
| [0008](0008-durabilite.md) | Transitions atomiques et exécution au moins une fois | Acceptée |
| [0009](0009-modules-et-versions.md) | Artefacts immuables et protection des anciennes instances | Acceptée — G3-A, coexistence nominale |
| [0010](0010-distribution.md) | Distribution par RID et qualification explicite | Proposée pour les modalités de packaging ; plateformes et publication NuGet.org en L10 acceptées |
| [0011](0011-auto-approbation.md) | Auto-approbation configurable, interdite par défaut | Acceptée |
| [0012](0012-ui-razor-blazor.md) | UI Razor/Blazor et formulaires extensibles | Acceptée |
| [0013](0013-contrats-l2.md) | Conventions et contrats du lot L2 | Acceptée |
| [0014](0014-binding-handlers-l4a.md) | Identité, résultats et validation des handlers L4a | Acceptée |
| [0015](0015-worker-retry-outbox-l4b.md) | Worker, retries, état et outbox L4b | Acceptée |
| [0016](0016-securite-profils-l5.md) | Sécurité L5, sessions configurables et profils internes | Acceptée ; détails techniques signalés comme proposés |
| [0017](0017-designation-approbateur.md) | Désignation explicite de l’approbateur ; Q11 résolue | Acceptée |
| [0018](0018-ldap-generique.md) | LDAP indépendant d’AD ; remplace obligation AD de 0003/0016 | Acceptée, implémentée en L5a et qualifiée sur OpenLDAP/LDAPS Linux CI |
| [0019](0019-workflow-stages.md) | Stage = nœud ; droits workflow/nœud/action | Acceptée R2-A/R3-A/R5-A/R6-A/R7-A ; périmètre L5b implémenté |
| [0020](0020-attentes-metier-l6.md) | Tâches humaines, timers, idempotence, séparation et inbox | Acceptée D1-B/D2-A à D7-A ; L6 implémenté et qualifié SQLite/Oracle 19.19 |
| [0021](0021-prototype-modules-ui-l7a.md) | Modules versionnés, rendu Blazor et ressources UI | D1-A à D4-A et G3-A acceptées |
| [0022](0022-loader-definitif-et-migrations-etat-l7b.md) | Loader définitif, rétention des artefacts et migrations d’état | G3-A, M1-A et M2-A acceptées ; L7b terminé localement |
| [0023](0023-host-formulaires-et-administration-l8.md) | Host Blazor, formulaires, projections, administration et qualification navigateur | L8-D1-A à L8-D8-A acceptées ; implémentation L8 en cours |
| [0024](0024-sdk-fluent-permissions-affectation.md) | SDK fluent, révisions héritées, permissions métier, affectation amont et formulaires standard | Principes et D1–D9 acceptés le 20 octobre 2026 ; L8b implémenté, 116 tests légers réussis ; qualification externe restante |
| [0025](0025-runtime-developpement-l9.md) | `ew dev`, SQLite local, configuration JSON, identités de test et reconstruction contrôlée | Périmètre et orientations acceptés le 20 octobre 2026 ; implémentation locale L9 livrée, qualification finale restante |

| [0026](0026-etat-metier-type-et-validation.md) | DTO d’état annoté, version explicite, mapping des formulaires et validation contextuelle | Acceptée le 25 octobre 2026 ; implémentée localement, qualification L9b restante |
| [0027](0027-consultation-medias-audit-tat-l9b.md) | Lecture/recherche autorisée, audit/export, médias et projections TAT | A1–A7 acceptés le 25 octobre 2026 ; implémentation locale et limites suivies en L9b |

- [ADR 0028 — État typé obligatoire et commentaires déclaratifs d’approbation](0028-etat-type-obligatoire-et-commentaires-approbation.md) — accepté, implémenté localement, qualification en cours.
