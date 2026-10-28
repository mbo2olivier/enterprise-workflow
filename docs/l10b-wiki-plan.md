# L10b — proposition de structure du wiki public

Date : 28 octobre 2026. **Structure et place de LeaveRequest comme fil rouge validées par le porteur le 28 octobre 2026.** L10 est clos sur sa confirmation : release créée et packages publiés sur NuGet. Ce document cadre l’implémentation L10b désormais autorisée ; la validation du plan ne dispense pas de vérifier le contenu et les exemples.

## Objectif et lecteurs

Permettre à un développeur .NET de découvrir Enterprise Workflow, exécuter un premier workflow, construire un module métier, intégrer le moteur dans une application et exploiter une installation. Le wiki public sera entièrement en anglais. Les intitulés ci-dessous sont ceux de sa navigation cible.

Organisation déjà retenue : Astro/Starlight dans `wiki/`, sources publiques dans `wiki/src/content/docs/`, publication GitHub Pages par workflow dédié. `docs/` reste l’espace interne des spécifications, ADR, plans et preuves ; aucun import automatique dans le site public.

## Navigation proposée et contenu

### Home

Présentation du produit, cas d’usage, capacités livrées et limites, aperçu du parcours d’un workflow. Trois entrées : créer un module, intégrer en embedded, déployer le Kernel. Liens vers démarrage rapide, exemples, NuGet et releases.

### 1. Getting Started — obtenir un premier résultat

- **Overview & prerequisites** : public cible, SDK/runtime requis, plateformes et providers effectivement qualifiés, choix entre Kernel modulaire et embedded.
- **Install** : versions publiques exactes, installation CLI/templates depuis NuGet, vérification de l’installation et packages nécessaires à chaque mode.
- **Your first workflow** : générer un module, décrire un processus minimal, lancer `ew dev`, démarrer une instance et terminer une tâche humaine ; résultat attendu à chaque étape.
- **Embed in an application** : application minimale distincte, DI et persistance, démarrage du moteur, lancement et suivi d’une instance.
- **Next steps** : aiguillage vers tutoriel métier, concepts et déploiement.

### 2. Basics — travailler au quotidien

- Anatomie d’un projet généré et rôle des fichiers.
- Cycle local : build, `ew dev`, environnements nommés, reconstruction et redémarrage.
- Définir un processus, son état métier et ses handlers.
- Ajouter une tâche humaine et un formulaire ; exécuter le parcours avec plusieurs acteurs.
- Valider, inspecter, packager et vérifier un module ; passage du développement au Kernel.

### 3. Core Concepts — comprendre le modèle

- Définitions, instances et identité durable.
- Modules, releases, versions de processus et versions de schéma d’état.
- Nœuds, transitions, outcomes et handlers versionnés.
- État métier typé : annotations, initialisation, validation et commit.
- Exécution durable : transactions, attente/reprise, retry et concurrence ; responsabilités du moteur et du code métier.
- Tâches humaines, affectation et autorisations.
- Formulaires, composants et présentation.
- Persistance SQLite/Oracle et différences documentées.

Chaque article explique le mécanisme, ses conséquences concrètes et un petit exemple, puis renvoie au guide et à la référence correspondants.

### 4. Guides — réaliser une fonctionnalité

- Écrire des handlers et injecter des services métier.
- Construire et charger un module ; enregistrer ses extensions.
- Personnaliser les formulaires, composants et sélecteurs distants.
- Configurer identité, permissions, fournisseurs et intégration LDAP/LDAPS livrée.
- Consulter et rechercher les instances selon les droits.
- Gérer pièces jointes, génération de documents et bibliothèque médias.
- Exploiter l’audit et ses exports.
- Construire les rapports TAT et comprendre leurs limites calendaires.
- Configurer notifications et SMTP.
- Configurer les adaptateurs et étendre les points d’extension publics existants.

### 5. Tutorials & Examples — apprendre sur des cas complets

- **Minimal approval** : approbation simple, state, formulaire et décision humaine.
- **Leave request walkthrough** : exemple fil rouge fondé sur le sample livré ; acteurs, règles métier, consultation, médias, audit et TAT.
- **Service integration** : appel à un service métier, résultats, erreurs et reprise ; idempotence du service externe explicitée.
- **Embedded application** : intégration complète sans dépendance au Host modulaire.
- **Evolving a running workflow** : nouvelle version, instances existantes et migration d’état.

Chaque tutoriel fixe une version du framework, indique les fichiers à créer, fournit les commandes et résultats attendus, et pointe vers un exemple exécutable. Les exemples publics consomment NuGet sans références projet au dépôt.

### 6. Deployment & Operations — installer et maintenir

- Choisir une archive Kernel et son mode de distribution ; prérequis runtime.
- Configurer une installation : paramètres, secrets, base et bootstrap.
- Déployer des modules et gérer leur compatibilité.
- Installer/configurer SQLite ou Oracle ; initialisation et migrations.
- Sauvegarder, restaurer et vérifier la reprise.
- Observer et diagnostiquer : logs, audit, erreurs et incidents courants.
- Sécuriser une installation et configurer les intégrations externes.
- Mettre à jour framework/modules et traiter les échecs de mise à jour selon les procédures réellement supportées.

### 7. Advanced Topics — approfondir les contrats sensibles

- Versionnement des handlers et conservation des comportements nécessaires aux instances existantes.
- Migrations d’état et compatibilité des releases.
- Transactions, concurrence, fencing et reprise après panne.
- Chargement des modules et limites d’isolation/dépendances.
- Extension des providers et adaptateurs : contrats publics et contraintes.

### 8. Reference — retrouver une information précise

- Catalogue des packages publics : rôle, ID, version et lien NuGet.
- Référence CLI : commandes, options, codes de sortie et exemples vérifiés.
- Templates et fichiers de configuration, notamment `ew.dev.json`.
- DSL/builder, contrats de handlers, DI et points d’extension publics.
- Annotations d’état et contrats de formulaires/médias/recherche/audit/TAT.
- Matrice de compatibilité, restrictions et fonctionnalités non disponibles.
- Glossaire et liens vers notes de release, licence et dépôt.

La référence sera structurée par usage et alignée sur les signatures publiques de la release. Une génération exhaustive de toutes les API .NET n’est pas proposée comme prérequis de ce lot.

## Règles éditoriales

- Séparer apprentissage guidé, recettes pratiques, explications et référence ; relier les pages plutôt que répéter leur contenu.
- Un tutoriel minimal court d’abord, puis LeaveRequest comme fil rouge plus complet.
- Chaque page indique les prérequis utiles, les étapes, le résultat observable et les erreurs courantes lorsque cela s’applique.
- Vérifier commandes, signatures et configuration contre le code livré et les packages publiés ; ne pas recopier comme acquis les anciennes propositions internes.
- Présenter les limites près de la fonctionnalité concernée ; ne pas annoncer le Studio L11 comme disponible.
- Le README racine deviendra une entrée courte en anglais : présentation, installation rapide, liens vers les trois parcours et le wiki ; sa réécriture relève de l’implémentation après validation.

## Séquence de réalisation après validation

1. Fixer les versions et liens publiés utilisés par tous les parcours ; préparer l’ossature et la navigation Starlight.
2. Rédiger et exécuter les parcours Getting Started, Basics et exemples minimaux dans les deux modes.
3. Développer Core Concepts, Guides et les tutoriels métier, puis les sujets d’exploitation et avancés.
4. Compléter Reference et le README public ; contrôler la couverture avec les contrats livrés.
5. Vérifier build, liens internes, navigation et consommation NuGet des exemples ; ajouter la CI documentaire et déployer sur GitHub Pages, puis vérifier le site publié.

## Validation acquise

Le porteur valide cette organisation et la place de LeaveRequest comme fil rouge le 28 octobre 2026. Il confirme ensuite que le README racine doit être en anglais et renvoyer vers le wiki, tandis que `docs/` reste l’espace de maintenance des agents de codage. L10b ne sera déclaré terminé qu’après rédaction, vérification des parcours et publication effective du site, conformément au plan principal.
