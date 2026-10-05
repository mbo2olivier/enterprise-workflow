# Registre des ADR

Les ADR 0001 à 0009 et 0011 à 0022 consignent les décisions explicites du porteur ; leurs détails encore proposés sont signalés. L'ADR 0010 n'est pas acceptée implicitement par sa rédaction. Un ADR proposé doit être validé avant le lot qu’il engage.

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
| [0010](0010-distribution.md) | Distribution par RID et qualification explicite | Proposée pour le packaging ; périmètre plateformes accepté |
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
