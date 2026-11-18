# L11 — hôtes personnalisés, extensions de sécurité et localisation

Date : 18 novembre 2026. Nomenclature décidée par le porteur : ce lot porte le numéro L11, le Studio, initialement repoussé à L12 lors de l’ajout de L11, est désormais prévu en L13 après validation du lot L12 et la série L10 reste close. Statut : plan intégré à la demande du porteur ; implémentation non démarrée. Les orientations produit ci-dessous reprennent les arbitrages de la conversation. Les noms d’API, le découpage des packages et les mécanismes détaillés restent à formaliser dans les spécifications et ADR avant implémentation. Ce plan ne modifie pas les contrats actuellement livrés et ne constitue pas une preuve de qualification.

## Périmètre et limites

- Fournir dans le SDK un builder de composition d’un hôte/kernel personnalisé, analogue aux builders d’hébergement .NET.
- Fournir les contrats et abstractions d’authentification externe, de résolution/provisionnement d’identité et de seconde authentification ; les implémentations tierces appartiennent aux hôtes personnalisés.
- Conserver les rôles, profils, permissions et habilitations workflow localement dans EW.
- Conserver les providers actuels du kernel standard, en les alignant sur les nouveaux contrats lorsque nécessaire. Celui-ci ne reçoit ni provider bancaire, ni maker/checker, ni handler de second facteur.
- Ajouter le chargement `.env` à `ew dev`, sans modifier le fonctionnement de configuration .NET du kernel déployé.
- Localiser le socle et les modules métier ; rendre configurables les langues par défaut et de repli de l’instance et proposer une préférence utilisateur.
- Ne pas intégrer la logique de seconde authentification dans le moteur de workflows. Ne pas utiliser un handler de nœud métier pour cette phase de connexion.
- Garder `ew dev` limité aux identités de `ew.dev.json`, sans provider externe, maker/checker, seconde authentification ni lancement d’un kernel personnalisé.

## Builder d’hôte personnalisé

Le point d’entrée proposé est `EnterpriseWorkflowApplication.CreateBuilder(args)`. Le nom reste indicatif. Il s’appuie sur l’hébergement .NET et expose `Configuration`, `Services`, `Logging` et les possibilités d’hébergement web, sans réimplémenter leurs conventions.

### Fonctions intégrées

| Fonction | Responsabilité du builder |
| --- | --- |
| Configuration et cycle de vie | Conventions .NET, environnement, options EW validées, démarrage et arrêt |
| Modules | Chargement, compatibilité, catalogues, binding des handlers et ressources exactes |
| Exécution | Composition des services et workers existants, scopes DI et cycle de vie |
| Persistance | Enregistrement des stores et validation des schémas ; migrations explicites |
| Autorisation locale | Profils, permissions et habilitations contextualisées, sans droits implicites provenant du provider |
| Sessions | Composition des services locaux de session et raccordement au parcours d’authentification |
| Web optionnel | Présentation, endpoints, ressources, cookies, antiforgery et Data Protection |
| Localisation | Catalogues, langues de l’instance et résolution de la préférence utilisateur |
| Capacités | Détection des interfaces enregistrées et exposition cohérente des opérations UI/serveur |
| Exploitation | Points de branchement logs, audit et contrôles de santé |
| Validation | Services obligatoires manquants, enregistrements ambigus et options incompatibles |

L’activation du web est explicite : les consommateurs sans UI ne doivent pas être obligés d’enregistrer la présentation. Les migrations, le bootstrap administrateur et la récupération d’accès restent des opérations explicites. La composition se valide à la construction ; les contrôles nécessitant la base ou une dépendance distante relèvent d’une phase de démarrage explicite.

### Capacités d’extension

L’hôte peut enregistrer dans son code ses providers d’authentification, de résolution/recherche/statut d’identité, de provisionnement et d’approbation, son handler de second facteur, ses providers de stockage, ses transports d’effets externes et notifications, ses modules, composants, pages, endpoints, sources de configuration et services applicatifs DI. Les intégrations bancaires et coffres de secrets ne sont pas fournis implicitement par le builder.

Le kernel standard devient une composition de référence de ce socle avec ses comportements actuels. Prévoir un exemple compilable d’hôte personnalisé utilisant un double de service tiers explicitement identifié comme démonstration, sans prétendre qualifier une intégration bancaire réelle.

Le package éventuel `EnterpriseWorkflow.Hosting`, la surface exacte du builder et la cardinalité des providers sont à arbitrer. Préserver des contrats légers et éviter d’imposer les dépendances web à un module métier.

## Contrats d’identité et authentification

Séparer les capacités d’authentification primaire, résolution, recherche, statut du compte et provisionnement. Un provider n’implémente que les capacités qu’il propose ; les services nécessaires sont enregistrés à la compilation dans l’hôte. Une configuration ne peut pas créer une capacité absente.

L’identité durable reste le couple `ProviderId + SubjectId`, indépendant d’un login modifiable. Les rôles externes ne donnent aucun droit local automatiquement. Pour les identités soumises à approbation, le succès de la preuve distante ne suffit pas : l’identité EW doit être approuvée et active.

Définir explicitement la politique de validation des sessions selon les capacités disponibles : le comportement actuel qui attend un annuaire pour toute identité non locale doit être traité, sans inventer un endpoint de statut que le tiers ne fournit pas. Définir également la suspension locale, la révocation, les noms affichés, la recherche des destinataires et les notifications. Le contrat HTTP réel et les secrets d’accès au système bancaire relèvent du projet d’hôte personnalisé.

## Provisionnement optionnel et maker/checker

La présence des interfaces de résolution pour création et de provisionnement soumis à approbation détermine la disponibilité de ce parcours dans l’hôte. Les contrats SDK définissent commandes, états, résultats, révisions et invariants ; le provider personnalisé fournit l’implémentation et sa persistance, en réutilisant les services locaux EW lorsque pertinent. Le provider standard ne déclare pas cette capacité.

Parcours : identifiant soumis par le maker → récupération des détails par le provider depuis sa source tierce → création locale en attente → revue d’un checker distinct → approbation/activation ou rejet.

Règles validées :

- Aucune création de compte dans le système tiers et aucun mot de passe local pour cette identité externe.
- Approbation conjointe de l’identité et des profils initiaux ; aucune affectation active avant approbation.
- Permissions distinctes pour soumettre et approuver ; interdiction serveur d’approuver sa propre demande.
- Décision sur une révision précise, unicité de l’identité et audit de la demande et de la décision.
- Relecture de la source avant activation ; une modification importante impose une nouvelle revue. Les attributs concernés sont à spécifier.
- Une identité en attente ne peut ni ouvrir une session complète ni être attribuée comme destinataire opérationnel.
- Vérification du statut local à la connexion et pendant la validation des sessions existantes.

Prévoir une suite de conformité réutilisable par les implémentations personnalisées. Masquer les écrans ne remplace pas le contrôle des commandes côté serveur.

## Seconde authentification entièrement optionnelle

Tout kernel peut fonctionner sans implémenter ni enregistrer de handler. Le SDK fournit uniquement les contrats et abstractions pour que l’hôte sollicite un handler compilé et enregistré dans son code. La configuration pilote uniquement l’activation ou la désactivation ; aucune sélection de classe, découverte ou chargement de handler par configuration.

| Situation | Résultat attendu |
| --- | --- |
| Désactivée, avec ou sans handler | Authentification simple ; aucun handler requis |
| Activée et handler enregistré | Seconde preuve du même utilisateur obligatoire avant session complète |
| Activée sans handler | Configuration incohérente refusée au démarrage |
| Seconde preuve refusée, expirée ou indisponible | Aucune session complète ; aucun contournement silencieux |

La valeur par défaut est désactivée. Le kernel standard ne fournit aucun handler et fonctionne normalement ainsi. `ew dev` ne prend pas en charge cette fonctionnalité.

Formaliser les échanges de démarrage, vérification/consultation de statut, expiration, annulation et résultats. Le handler et l’intégration hôte gèrent le mécanisme concret : OTP, validation mobile, stockage des challenges, appels tiers et règles associées. Le contrat doit permettre de lier la preuve à la tentative et à l’identité, borner les essais et empêcher le rejeu. Aucun mot de passe primaire conservé pendant cette phase et aucune session complète avant sa réussite. Distinguer ce parcours du maker/checker et des attentes durables du moteur de workflows.

## `.env` dans `ew dev`

Chargement automatique du seul `.env` facultatif à la racine du projet ou de l’artefact. Aucun `.env.<environnement>` implicite, même avec `--environment`. Ajouter `--env-file <chemin>` (absence du fichier explicite = erreur) et `--no-env-file`. Spécifier la résolution des chemins et refuser la combinaison de ces options.

Priorité : options CLI > variables du processus > `.env` > `ew.dev.json` > valeurs par défaut. Conserver la fusion existante des sections communes et de l’environnement nommé dans `ew.dev.json`. Les noms utilisent la convention hiérarchique .NET `__` ; établir le mapping des clés de développement et des sections hôte avant implémentation.

Analyser le fichier sans exécuter de shell. Appliquer les valeurs avant la validation finale, puis les transmettre au processus hôte seulement, sans propagation inutile à la compilation. Ne pas sérialiser les secrets résolus dans le fichier intermédiaire de configuration, les artefacts, les arguments ou les logs. Vérifier également les erreurs et diagnostics.

Une modification du fichier nécessite de relancer `ew dev` dans cette première version. Ajouter `.env.example` sans secrets et les règles Git nécessaires en préservant les modifications existantes. Le kernel déployé continue d’utiliser ses sources .NET actuelles ; aucun chargement `.env` automatique en production.

## Localisation du socle et du métier

La configuration d’instance déclare les langues disponibles, la langue par défaut et la langue de repli, toutes validées. Proposer dans le profil une préférence persistée localement ou « Utiliser la langue de l’instance », y compris pour les identités externes.

Résolution : préférence utilisateur > langue par défaut de l’instance. Pour une traduction absente : langue de repli configurée, puis libellé de secours déclaré. L’anglais est une traduction prioritaire du produit, pas une langue d’instance imposée. Fuseau horaire et langue restent distincts.

Localiser navigation, pages, administration, validations, messages utilisateurs CLI et notifications. Appliquer la langue au HTML et aux formats d’affichage, sans modifier IDs, permissions, codes d’erreur, JSON, valeurs métier ou formats persistés.

Le SDK métier propose des textes structurés clé/repli, des catalogues embarqués rattachés au module, un localiseur pour composants personnalisés et des erreurs métier clé/paramètres. Exemple indicatif : `fields.Date("startDate", Text("fields.startDate", "Start date"))`. Pour les DTO, une forme telle que `LabelKey` reste à spécifier. Les libellés littéraux existants restent acceptés.

Résoudre à l’affichage, jamais pendant la compilation ou le chargement unique du module : deux utilisateurs doivent pouvoir lire la même instance dans des langues différentes. Fournir explicitement la langue aux notifications et documents générés en arrière-plan. Préserver les artefacts exacts, les hashes historiques et les contrats persistés ; spécifier toute évolution de format et migration nécessaire. JSON pour les catalogues et les noms d’API restent des propositions à formaliser.

## Séquence et critères de sortie

| Sous-lot | Livrables | Dépendances et validation |
| --- | --- | --- |
| L11-0 — spécifications | Inventaire des contrats existants, ADR, compatibilité des packages publiés, contrat du builder, capacités et responsabilité de persistance | Avant toute implémentation ; arbitrages détaillés acceptés et aucune API illustrative présentée comme livrée |
| L11-1 — composition | Builder, validation et adaptation du kernel standard ; exemple d’hôte personnalisé | L11-0 ; démarrage standard/personnalisé, DI, modules, cycles de vie, migrations explicites et absence de dépendance web imposée |
| L11-2 — capacités d’identité | Contrats séparés, adaptation des providers actuels, sessions et UI selon capacités | L11-1 ; authentification/autorisation séparées, absence de droits implicites, statut distant facultatif explicite, révocation et destinataires |
| L11-3 — approbation optionnelle | Contrats de création/maker-checker, points d’intégration et suite de conformité avec provider de démonstration | L11-2 ; refus auto-approbation, doublons et révisions concurrentes, revalidation source, identité en attente inutilisable ; parcours absent du provider standard |
| L11-4 — second facteur optionnel | Contrats SDK, intégration hôte et exemple de handler compilé | L11-2 ; désactivation sans handler fonctionnelle, activation sans handler refusée, refus/expiration/rejeu/panne sans session complète ; aucun ajout à `ew dev` |
| L11-5 — secrets dev | Lecture `.env`, options CLI, mapping et transmission au seul hôte, exemple et documentation | Indépendant des capacités de sécurité après L11-0 ; priorités, chemins, erreur explicite, redémarrage et absence de secrets dans les sorties/fichiers générés |
| L11-6 — localisation | Ressources socle, configuration et préférence utilisateur, contrats métier et exemple LeaveRequest bilingue | L11-0 et intégration au builder ; deux langues sur une même instance, replis configurables, formats, validations et notifications |
| L11-7 — qualification et distribution | Tests appropriés, wiki, templates, exemples, compatibilité et preuves | Sous-lots précédents ; SQLite/Oracle pour toute évolution de persistance, matrice plateforme et navigateur pour les parcours concernés |

Les implémentations réelles du provider et du second facteur bancaires constituent un projet consommateur distinct. Leur validation ne peut pas être revendiquée sur la seule base des doubles du framework. Le lot [L12](l12-cli-distributions-branding-plan.md) réutilise ces fondations pour les CLI et distributions organisationnelles ; il conserve les identités de développement et exclut l’authentification réelle de la commande dev. Le Studio L13 reste distinct et non démarré ; la séquence est L11 → L12 → L13.

## Documents à mettre à jour ensuite

Après formalisation des décisions : PRD, architecture de l’hébergement/sécurité/localisation, spécifications et guides SDK/CLI, schéma `ew.dev.json` si nécessaire, compatibilité, opérations, stratégie de tests et traçabilité. Ajouter les ADR nécessaires sans réécrire les preuves des lots clos. Mettre à jour le wiki public lors de la livraison et distinguer jusque-là les propositions des fonctionnalités disponibles.
