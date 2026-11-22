# L12 — arbitrages avant spécification des mécanismes

Date : 19 novembre 2026. Statut : D12-1 à D12-5 décidés par le porteur ; spécification L12 et ADR 0030 formalisés ; implémentation autorisée et qualification lourde déléguée. L11 est clos sur sa confirmation de réussite locale et CI. Les orientations du [plan L12](l12-cli-distributions-branding-plan.md) restent acquises ; les propositions ci-dessous ne rouvrent pas ces choix.

## Acquis

Builder CLI configuration/construction/exécution séparées ; extensions compilées ; même socle pour `ew` et les CLI organisationnelles ; refus des collisions ordinaires ; services métier partagés entre dev et Kernel ; aucune authentification réelle en dev ; une marque par installation ; aucun composant privé dans les packages publics. Le remplacement d’une commande doit être explicite, sans permettre le remplacement des mécanismes de sécurité réservés de dev.

## D12-1 — compatibilité du runtime complet

**Décision du porteur : compatibilité déclarée par le module au niveau du runtime complet, avec versions sémantiques et mécanismes existants.** Aucun catalogue de compatibilité par service ou contrat individuel. L’auteur du module déclare la plage de versions du runtime qu’il cible et assume la qualification de son module sur les versions annoncées.

Pour un module bancaire, la cible est le runtime de la distribution bancaire, dont la release fixe l’ensemble cohérent des composants conformément à D12-4. Exemple illustratif avec la syntaxe NuGet : runtime `Bank.Workflow`, plage `[2.3.0,3.0.0)` ; l’hôte 2.4.1 satisfait cette contrainte, l’hôte 3.0.0 est refusé au chargement avec un diagnostic. Le format exact de la déclaration sera défini en spécification ; l’identité de la cible évite de comparer les versions de deux runtimes différents.

Réutiliser les types et comparateurs existants de l’écosystème .NET/NuGet, notamment `NuGet.Versioning`, plutôt qu’inventer une syntaxe ou un moteur de comparaison. Préciser en spécification le traitement des préversions et des artefacts historiques dépourvus de cette information. Une plage déclarée est un engagement de compatibilité de l’auteur, pas une preuve produite automatiquement par le loader.

Le partage des assemblies reste nécessaire pour que module et hôte utilisent les mêmes types .NET. Il s’agit d’un mécanisme de chargement, sans politique de compatibilité granulaire supplémentaire exposée à l’auteur. La proposition précédente de contrôle exact par contrat comme politique produit est abandonnée.

## D12-2 — consommation des services par les modules

**Décision du porteur : injection .NET ordinaire, sans déclaration supplémentaire.** Le développeur consomme une interface dans son handler ; l’hôte de développement et le Kernel fournissent son implémentation. Aucun inventaire de services requis ni déclaration de capacités dans le manifeste du module n’est imposé.

Un service non inscrit provoque une erreur lors de sa résolution ou de l’appel qui la déclenche, avec le contexte disponible pour comprendre le problème. Aucune validation préalable exhaustive ni instanciation anticipée des handlers n’est requise. Le partage des types reste une responsabilité du loader ; la compatibilité annoncée par le module porte uniquement sur le runtime complet (D12-1).

## D12-3 — installation des templates

**Décision approuvée par le porteur : préparation explicite, puis génération sans installation réseau implicite.** Après installation de la CLI, une commande de préparation installe les templates aux versions exactes de sa distribution. La création de projet vérifie leur présence et indique la commande de préparation si nécessaire ; elle ne les télécharge pas automatiquement.

Parcours illustratif : installation de la CLI → `few setup` → `few new`. Les noms restent indicatifs. La préparation intervient à l’installation ou à la mise à jour de la distribution, pas à chaque création de projet. L’organisation et les mécanismes NuGet gèrent l’authentification au feed ; aucun secret n’est embarqué dans la CLI. Ce choix concerne les templates et n’interdit pas une restauration NuGet explicite du projet généré.

Alternative non retenue : installation automatique des templates manquants lors de la première création.

## D12-4 — versionnement d’une distribution

**Décision approuvée par le porteur : ensemble de versions exactes publié par chaque release de distribution.** La distribution référence précisément CLI, SDK, contrats, intégrations, Kernel et templates ; les projets générés utilisent ces versions. La mise à jour vers un nouvel ensemble est explicite. Ne pas imposer la dernière version disponible sur le feed ni une recompilation différente selon l’environnement.

Alternative non retenue : versions flottantes/plages pour certains composants. Plus souple à mettre à jour, mais deux développeurs pourraient recevoir des compositions différentes au même moment. Le feed privé reste choisi par l’organisation ; Azure Artifacts n’est pas imposé au framework.

## D12-5 — réglages de marque administrables et priorité

**Décision approuvée par le porteur : pour un hôte personnalisé, paramètres de marque verrouillés par défaut, déverrouillés explicitement dans sa composition.** Ordre de résolution : défauts framework → défauts de l’hôte → réglages persistés autorisés. Pour un paramètre verrouillé, la valeur d’hôte s’impose même si une ancienne valeur existe en base ; une modification serveur non autorisée est refusée.

Les logos, favicon et styles additionnels sont fournis par l’hôte ; l’organisation autorise nom/couleurs/autres réglages selon ses besoins. La composition du kernel standard autorise explicitement ses réglages historiques pour préserver leur fonctionnement.

Alternative non retenue : tous les réglages de marque administrables par défaut, avec interdictions explicites de l’hôte. Ce choix donne plus d’autonomie aux administrateurs d’installation mais fait du verrouillage de marque une démarche volontaire.

## Travaux indépendants de ces réponses

Préparer la cartographie des commandes existantes et du cycle enfant de `dev`, définir les points de composition compilés, proposer le découpage des bibliothèques CLI/développement et inventorier les services réservés. Le redémarrage dev devra utiliser la composition de la CLI organisationnelle, et non perdre ses inscriptions en relançant systématiquement la CLI standard. La parité des services ne modifie pas les identités de test L11.

Les cinq arbitrages étant décidés : consigner les décisions en ADR, stabiliser signatures/manifestes/politique de thème, puis implémenter les sous-lots du plan. Les campagnes lourdes restent à déléguer au porteur avec une recette détaillée, conformément à sa préférence de consommation de tokens.
