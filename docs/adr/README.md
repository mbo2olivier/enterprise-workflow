# Registre des ADR

Les ADR 0001 à 0005, 0011 et 0012 consignent les décisions explicites du porteur ; leurs détails encore proposés sont signalés. Les ADR 0006 à 0010 ne sont pas acceptés implicitement par leur rédaction. Un ADR proposé doit être validé avant le lot qu’il engage.

Une modification significative donnera lieu à un nouvel ADR qui remplace le précédent ; conserver l’historique et le motif. « Acceptée » signifie décision prise, pas fonctionnalité implémentée ou testée.

| ADR | Sujet | Statut |
| --- | --- | --- |
| [0001](0001-dotnet-et-plateformes.md) | .NET 10 et plateformes | Acceptée pour la cible ; modalités de packaging proposées |
| [0002](0002-persistance.md) | SQLite et Oracle 19c minimum | Acceptée pour les SGBD ; architecture EF proposée |
| [0003](0003-securite-extensible.md) | Sécurité par extensions et administration intégrée | Acceptée pour le principe et le mode AD ; contrats détaillés proposés |
| [0004](0004-perimetre-mvp.md) | Moteur, Kernel et CLI avant Studio | Acceptée pour le séquencement ; détails du moteur proposés |
| [0005](0005-isolation-organisation.md) | Une installation par organisation | Acceptée |
| [0006](0006-frontieres-architecture.md) | Runtime bibliothèque et Kernel de composition | Proposée |
| [0007](0007-modele-canonique.md) | Modèle canonique et graphe MVP borné | Proposée |
| [0008](0008-durabilite.md) | Transitions atomiques et exécution au moins une fois | Proposée |
| [0009](0009-modules-et-versions.md) | Artefacts immuables et protection des anciennes instances | Proposée — choix coexistence/drainage encore ouvert |
| [0010](0010-distribution.md) | Distribution par RID et qualification explicite | Proposée pour le packaging ; périmètre plateformes accepté |
| [0011](0011-auto-approbation.md) | Auto-approbation configurable, interdite par défaut | Acceptée |
| [0012](0012-ui-razor-blazor.md) | UI Razor/Blazor et formulaires extensibles | Acceptée |
