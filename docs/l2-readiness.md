# Préparation du lot L2

Date : 1er octobre 2026. Statut : décisions L2-D1 à L2-D6 acceptées par le porteur le 1er octobre 2026 ; aucune implémentation L2 engagée.

## Point de départ

L1 fournit solution, Abstractions/Core/SDK, règles de compilation, SDK 10.0.102 épinglé, tests d’architecture et exemple de chargement des assemblies. Le porteur confirme la réussite de GitHub Actions ; cette confirmation n’a pas été remplacée par une vérification indépendante du run.

Les ADR 0006, 0007 et 0008 sont acceptés : frontières séparées, modèle canonique, parcours séquentiel avec décisions exclusives, pas de cycles/parallélisme, transitions durables et reprise au moins une fois. Ces décisions ne sont pas à rouvrir.

## Livrables et frontières

L2 transforme le socle en bibliothèque capable de construire, normaliser et valider une définition. Il livre le modèle canonique immuable, un DSL minimal, les diagnostics, la sérialisation déterministe, les contrats de stockage et leurs scénarios de conformité.

Le modèle concret couvre Start, Service, Decision et End ; HumanTask et Timer seront ajoutés avec leurs contrats fonctionnels au lot L6. Le modèle et le catalogue de types doivent rester extensibles pour cela. Le DSL référence des exécuteurs/évaluateurs nommés et versionnés ; L2 ne les exécute pas. Les types personnalisés sont décrits par un catalogue fourni par l’application, sans chargement de DLL automatique.

Ajouter Persistence.Abstractions au moment des contrats purs et étendre les tests d’architecture qui attendent actuellement exactement trois projets. Les entités EF, migrations, SQL, worker, UI, connexion AD et loader restent dans leurs lots respectifs. Les types de nœuds représentables ne doivent pas être présentés comme déjà exécutables.

## Décisions acceptées

Le porteur a explicitement validé D1 à D6. Ces décisions constituent le cadre de réalisation de L2 ; voir également l’[ADR 0013](adr/0013-contrats-l2.md).

### L2-D1 — Identités et versions

Décision : identifiants techniques stables et sensibles à la casse selon une comparaison ordinale, limités à 128 caractères ASCII parmi lettres, chiffres, point, tiret et underscore, avec premier caractère alphanumérique. Labels d’affichage libres et distincts. Identifiant de nœud unique dans une définition ; identifiant de définition stable dans l’installation.

Version de définition : entier strictement positif (1, 2, 3…), sans remplacer le contenu d’une version publiée. Version de schéma de sérialisation distincte, initialement 1. Les versions des packages/modules restent indépendantes. L2 ne promet pas de migration automatique de schémas futurs.

Conséquence : changer le code ou le graphe d’une définition publiée exige le traitement prévu par les références d’artefacts ; ne pas confondre hash du graphe et hash du module. Sémantique des packages inchangée par ce choix.

### L2-D2 — Branches exclusives et diagnostic

Décision : exactement un Start, une ou plusieurs fins End, tous les chemins atteignables doivent pouvoir se terminer. Start et Service possèdent une seule transition normale. Decision retourne une issue nommée ; une seule transition est sélectionnée.

Chaque issue déclarée doit être raccordée ou couverte par une branche par défaut explicitement déclarée. Une issue inconnue utilise ce défaut s’il existe ; sinon, elle produit une erreur explicite. Aucune branche de secours créée implicitement. Plusieurs issues peuvent rejoindre le même nœud ; cette convergence exclusive n’est pas une jointure parallèle.

Validation en deux niveaux : structure pure, puis références/types/configurations via un catalogue fourni. Un brouillon incomplet peut être inspecté avec diagnostics ; il n’est jamais déclaré publiable. Le DSL possède une construction inspectable et une validation explicite ; pas de publication automatique.

### L2-D3 — Données et normalisation

Décision : état de l’instance représenté par un objet JSON versionné. Le contrat d’un résultat de nœud exprime soit « état inchangé », soit un nouvel état complet ; aucun merge implicite. Les DTO C# peuvent servir à l’écriture métier, puis sont convertis explicitement aux frontières durables.

Entrées et configurations exposées comme valeurs immuables. Propriétés d’objet à noms uniques ; rejeter les doublons, commentaires et nombres non JSON. Représentation normalisée déterministe : ordre ordinal des propriétés, nœuds et transitions par identifiants ; tableaux métier conservés dans leur ordre ; normalisation des nombres documentée et testée sans conversion avec perte de précision. L’ordre d’insertion du builder ne change pas l’empreinte du graphe. Une version du format canonique accompagne cette règle.

La normalisation et le hash SHA-256 sont des choix d’implémentation acceptés, pas une garantie d’équivalence de tout JSON mathématiquement similaire. Les références d’artefacts restent distinctes de l’empreinte de définition. Aucun objet CLR arbitraire ou delegate n’est persisté.

### L2-D4 — Limites initiales configurables

Valeurs initiales acceptées, configurables et à ajuster sur cas réels : définition complète de 1 Mio maximum et 1 000 nœuds maximum ; état d’instance de 1 Mio maximum ; configuration de chaque nœud de 256 Kio maximum. Tailles mesurées en octets UTF-8 sérialisés (1 Mio = 1 048 576 octets). Profondeur JSON maximale : 64.

Ces limites sont appliquées sans troncature ; un dépassement est un diagnostic/échec explicite et ne doit pas écraser l’état persistant précédent. Elles sont configurables et ne constituent pas un objectif de performance. Les pièces jointes et documents volumineux seront représentés par références, sans ajout de gestion de fichiers dans L2.

Les limites portent sur les valeurs sérialisées reçues et normalisées ; les deux représentations doivent respecter le plafond. Le plafond total de définition s’applique même lorsque chaque configuration individuelle est valide.

### L2-D5 — Contrat des commandes et du store

Décision : création idempotente, claim/renouvellement de bail, commit conditionnel et annulation décrits par opérations atomiques explicites ; pas de repository CRUD générique. Les contrats d’attentes humaines et de timers ne sont pas figés artificiellement avant L6, mais leurs frontières transactionnelles déjà documentées sont préservées.

Une clé d’idempotence de démarrage est liée à l’installation, au type de commande et à l’identité stable de l’acteur (fournisseur + sujet). Même clé et même contenu : retourner la même instance ; même clé et autre contenu : conflit. La clé métier de l’instance ne sert pas de clé de déduplication implicite. Les autres commandes définiront leur propre portée avant exposition.

Pas de purge automatique des reçus au MVP initial : la rétention effective et une éventuelle expiration seront arbitrées avant d’ajouter une purge. Conséquence assumée : croissance du stockage à surveiller. Les clés de déduplication ne redeviennent pas réutilisables silencieusement.

Révision de concurrence abstraite et jeton de possession distincts. Temps en UTC via DateTimeOffset au contrat, précision à la milliseconde, horloge cohérente fournie par le store pour les baux ; TimeProvider pour les tests purs. Encodage SQL et source d’horloge concrets éprouvés séparément sur chaque provider en L3.

Les baux sont renouvelables et configurables. Leurs durées, cadence de polling et délais de retry n’ont pas besoin d’être codés dans les DTO de L2 : ils seront arrêtés et testés avec le worker L4. Leur différé ne reporte pas la définition des préconditions d’expiration/fencing. Les états terminaux et conflits suivent le document runtime ; aucune reprise manuelle de Failed ajoutée dans ce lot.

### L2-D6 — Portée des preuves de stockage

Décision : L2 spécifie les contrats et fournit des scénarios de conformité et, si utile, un double mémoire de référence pour exercer le protocole. Cela prouve la cohérence du contrat, pas la durabilité SQL.

Le schéma logique et les contraintes uniques sont décrits en L2. Les mappings physiques, migrations et preuves d’atomicité réelles appartiennent à L3a/L3b. Ne pas bloquer le démarrage du modèle et du DSL sur l’accès à Oracle ; traiter les contrats comme révisables jusqu’au prototype des deux providers et ne pas publier une API stable avant ces preuves.

## Environnement de test Oracle

Le porteur ne possède pas d’instance Oracle. Docker est déjà installé ; il autorise la préparation d’un environnement Docker Compose pour les tests. La CLI Docker est présente sur la machine ARM64 ; le daemon et les services n’ont pas été démarrés ou qualifiés pendant ce cadrage.

La préparation du Compose et le choix de l’image seront réalisés au prototype de persistance. Docker reste un outil de développement/test facultatif pour les utilisateurs du framework, pas un prérequis d’exécution du produit. SQLite utilisera une base fichier, sans serveur dédié.

Oracle fournit une voie de construction d’image 19c ARM64 avec les binaires d’installation correspondants. L’image/version exacte et l’accès aux binaires restent à vérifier lors du provisionnement. [Instructions officielles Oracle](https://github.com/oracle/docker-images/blob/main/OracleDatabase/SingleInstance/README.md), consultées le 1er octobre 2026.

Une image Oracle Free plus récente peut être utile aux essais, mais ne remplace pas les tests réels sur 19c. Aucun téléchargement, construction d’image ou démarrage de conteneur n’a été effectué ici. Le minimum produit reste 19c ; l’absence de serveur ne bloque pas L2, conformément à D6.

## Ordre de réalisation

1. Modèle immuable, identifiants, versions et catalogue minimal.
2. Validation structurelle et des références, diagnostics stables avec localisation.
3. DSL : exemple séquentiel et exemple avec décision exclusive convergeant vers une fin.
4. Sérialisation, normalisation et empreintes déterministes ; limites et tests de mutation.
5. Nouveau projet de contrats purs du store, transitions et scénarios de conformité.
6. Mise à jour de l’exemple, des tests d’architecture, de GitHub Actions si nécessaire et de la preuve L2.

## Critères de sortie

- Exemple C# construit un modèle valide ; aucun moteur d’exécution n’est prétendu livré.
- Doublons, cycles, parallélisme implicite, nœuds inatteignables et issues ambiguës sont refusés avec diagnostics localisés.
- Catalogue inconnu ou configuration invalide interdit le statut publiable.
- Deux constructions équivalentes donnent la même représentation canonique ; un changement sémantique pertinent modifie son hash.
- Données immuables, limites explicites et branches de décision testées.
- Préconditions atomiques, conflits, déduplication et transitions documentés et testés au niveau contrat ; preuves réelles réservées aux providers.
- Build et tests passent avec les versions verrouillées sur la CI existante ; rapport de preuve distingue exécuté, simulé et différé.

L2 ne commence pas par une nouvelle décision sur .NET, GitHub Actions, SQLite/Oracle, Razor/Blazor ou l’authentification : ces choix sont déjà enregistrés ou concernent des lots ultérieurs.
