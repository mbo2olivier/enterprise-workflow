# Example.Workflow — distribution fictive L12

Exemple public, sans dépendance bancaire ni secret. Version de distribution : **1.0.0-rc.1** ; framework source **1.0.0-rc.1**. Les packages locaux sont à construire depuis cette révision ; les nouveautés L12 ne sont pas supposées publiées.

- `Contracts` : interface `IApprovalPolicy` consommée par le module.
- `Sdk` : classe de base annonçant la compatibilité du runtime complet.
- `Hosting` : implémentation HTTP et composition commune CLI/Kernel.
- `Cli` : outil `few-example`, commandes standard, setup/new et commande compilée `distribution`.
- `Templates` : module préconfiguré avec référence SDK exacte, configuration dev et ressources.
- `Module` : compilation du code exact du template contre les projets source.
- `Kernel` : mêmes contrats/services, sécurité locale standard, marque verrouillée sauf fuseau.
- `PolicyApi` : endpoint HTTP fictif permettant de constater les appels réels.

`distribution.json` fixe la release et les versions de ses composants. Les dépendances sont des ProjectReference pour développer la distribution elle-même ; le module généré utilise un PackageReference exact. Une organisation remplace ces packages par ses packages privés et son feed NuGet, sans fork du framework.

Parcours complet : [recette L12](../../docs/validation/l12-recette-porteur.md). Les phases sont lancées séparément avec `scripts/qualify-l12.py`. Aucun script ne publie de package ou ne qualifie une intégration bancaire réelle.

L13 : le Kernel accepte aussi `Modules:RootDirectory` (prioritaire sur la liste `Modules:Directories`). La phase `kernel` du script L12 utilise désormais cette découverte et un chemin individuel invalide pour vérifier la priorité. La CLI dev et la compatibilité du runtime complet restent inchangées.
