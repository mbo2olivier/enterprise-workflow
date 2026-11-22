# L12 — spécification des distributions et de la CLI

Statut : implémentation autorisée le 19 novembre 2026. Qualification lourde réservée au porteur. Décisions D12-1 à D12-5 : [arbitrages](l12-arbitrages.md).

## CLI compilée

`EnterpriseWorkflowCli.CreateBuilder(args)` retourne un builder configurable ; `Build()` fige la composition, `RunAsync()` exécute une commande. `ew` consomme le même socle public. Une CLI organisationnelle est un outil .NET autonome ; son point d’entrée est réutilisé pour les sous-processus de préparation, vérification et développement. Les callbacks restent du code compilé, jamais du JSON ou une assembly choisie par l’utilisateur.

Le builder expose configuration, services, nom de commande, commandes standards, commandes supplémentaires avec options déclarées, et remplacement explicite des commandes publiques. Les commandes internes `__*` et les mécanismes de sécurité de développement ne sont pas remplaçables. Les collisions et options inconnues sont refusées. Le contexte commande fournit arguments, DI, configuration, sorties, logger et annulation. Codes : 0 succès, 1 échec, 130 annulation.

## Développement

La composition reçoit la configuration du DevHost, y compris les valeurs `.env` L11. Les mêmes services métier compilés peuvent être enregistrés dans le Kernel. Les services existants et les contrats de sécurité réservés ne peuvent pas être remplacés par le callback de développement. Aucun provider réel, maker/checker ou second facteur n’est activé en dev. Les identités restent celles de `ew.dev.json`.

## Runtime et modules

Un module peut annoncer une cible de runtime (identifiant stable) et une plage NuGet/SemVer, par une propriété d’auteur, sérialisée dans `module.json`. Exemple : `Bank.Workflow`, `[2.3.0,3.0.0)`. L’hôte annonce son identifiant et sa version. La comparaison réutilise `NuGet.Versioning` ; une mauvaise cible ou une version hors plage refuse le chargement avant activation. Les préversions suivent la comparaison de cette bibliothèque, sans sélection automatique de release.

Les artefacts historiques sans cible restent acceptés par défaut pour préserver L11 ; un hôte organisationnel peut exiger une déclaration. Le hash historique est inchangé pour les manifestes sans cible ; la cible et sa plage participent au hash des nouveaux artefacts. L’identité runtime de la distribution correspond à la release de son ensemble exact de composants. Aucun inventaire par service n’est requis ; les erreurs DI restent à la résolution.

L’hôte désigne les assemblies de contrats partagées. Le loader les partage avec les modules pour conserver l’identité des types, sans politique produit de version par service. Les dépendances privées restent isolées. L’inspection pure d’un artefact ne présume pas un runtime cible ; son exécution et sa vérification par la CLI utilisent celui de la CLI.

## Templates et versions

La distribution fixe les versions exactes des packages. Une commande explicite `setup` installe le package de templates à la version prévue, via NuGet et ses mécanismes d’authentification. `new` ne télécharge pas les templates manquants : elle demande `setup`. La restauration ultérieure du projet est distincte. Aucun secret n’est embarqué. Une distribution fictive démontre CLI, contrats, services partagés, Kernel et templates ; aucune intégration bancaire réelle.

## Marque

Une marque par instance. L’hôte fournit les valeurs par défaut, assets et styles. Les champs administrables sont verrouillés par défaut pour un hôte personnalisé ; le Kernel standard autorise explicitement ses champs historiques. Priorité : framework, hôte, valeurs persistées autorisées. Les valeurs persistées verrouillées ne prennent pas effet et les mutations serveur sont refusées. Les éléments de marque s’appliquent au portail, à la connexion et à l’administration, avec les langues L11. Éviter une migration de stockage si les ajouts peuvent rester dans la composition de l’hôte.

## Validation

Tests légers : builder/collisions, options, sécurité de composition, plages runtime et hash historique, politique de marque. Compilation des hôtes et de la distribution de référence. Recette lourde : packaging local, installation de l’outil/templates, module généré, dev avec reload et configuration, même artefact dans le Kernel, tests d’incompatibilité, contrôles navigateur et matrice CI. Le porteur exécute ces campagnes ; aucune réussite n’est présumée.
