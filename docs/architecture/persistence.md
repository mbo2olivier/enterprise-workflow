# Persistance SQLite et Oracle

Choix des SGBD accepté : SQLite et Oracle à partir de 19c. Les opérations atomiques et règles pures du contrat sont implémentées en L2. Les providers SQLite L3a et Oracle L3b sont implémentés et éprouvés sur des bases réelles ; Oracle Free couvre la boucle de développement et Oracle Enterprise 19.19 qualifie le minimum produit pour les scénarios de persistance L3b.

## Composition

Le Core reste indépendant d’EF et les contrats du store sont purs. Chaque adaptateur expose sa configuration de modèle, son `DbContext` dédié et un objet `*WorkflowDatabase` dont `MigrateAsync` applique explicitement les migrations. Le store ne migre jamais implicitement au démarrage. L’application embarquée reste propriétaire des migrations de son contexte ; le Kernel possède celles du sien.

Un modèle commun ne suffit pas à rendre le SQL portable. La transaction d’écriture immédiate SQLite et le verrouillage `FOR UPDATE ... SKIP LOCKED` Oracle, leurs horloges SQL et leurs conversions restent donc dans leurs adaptateurs respectifs. Les entités physiques ne sont pas factorisées artificiellement.

## Opérations atomiques proposées

| Opération | Conditions | Écritures atomiques et résultat |
| --- | --- | --- |
| PublishDefinition | Identité/version libre, ou contenu identique | Définition + références ; conflit si hash différent |
| StartInstance | Autorisation établie, définition disponible, clé de commande cohérente | Instance, activation/travail, reçu, audit ; résultat nouveau ou connu |
| ClaimDueWork | Échéance passée, instance exécutable, libre ou bail expiré | Propriétaire, génération, expiration, état d’activation ; au plus un gagnant |
| RenewLease | Même génération, bail valide, instance exécutable | Nouvelle expiration ; conflit sinon |
| CommitNodeResult | Bail et génération valides, révision attendue | État, suite/attente, audit/outbox, travail terminé |
| CompleteHumanTask | Tâche ouverte, révision/affectation attendues et commande autorisée | Résultat, fermeture, suite, reçu et audit |
| FireTimer | Échéance passée, attente active | Timer consommé, activation résolue, suite et audit |
| CancelInstance | État non terminal et révision attendue | Instance annulée, invalidation travaux/tâches/timers, audit |
| Claim/CompleteOutbox | Message disponible, génération de livraison valide | Bail puis résultat de livraison, sans double clôture logique |

Chaque méthode renvoie un résultat structuré : réussite, résultat idempotent, conflit, absence ou indisponibilité. Un échec SQL avant commit annule toutes les écritures. Une réponse perdue après commit est récupérée via idempotence ou état durable.

Le projet `EnterpriseWorkflow.Persistence.Abstractions` expose publication immuable, démarrage idempotent, claim avec snapshot d’exécution, renouvellement, commit conditionnel, annulation et claim/commit fenced de l’outbox L4b. L6 ajoute les attentes humaines et timers durables, les affectations désignées, les reçus de soumission et la lecture bornée de l’inbox. Les règles contrôlent répétition, token + génération + expiration, UTC à la milliseconde et transitions terminales.

La révocation de droits externes ne peut pas être rendue atomique avec une transaction SQL locale. Recontrôler l’autorisation au moment de la commande ; protéger aussi l’affectation et la révision persistées au commit. Les garanties de fraîcheur sont documentées dans le contrat de sécurité.

## Modèle logique en L2 ; modèle physique en L3

Contraintes uniques : définition/version, reçu/portée/clé, travail logique de suite, résolution d’attente. Révision abstraite entière mise à jour conditionnellement ; ne pas imposer `rowversion` SQL Server.

Index attendus : travaux par état/échéance ; baux expirés ; tâches ouvertes par sujet ou groupe ; instances par définition/version/statut ; outbox par état/échéance. Valider les tailles, collations et plans réels avant de promettre un débit.

UTC au contrat ; encodage SQL explicite. JSON versionné stockable en texte, avec limites de taille définies avant exposition réseau ; pas de dépendance fonctionnelle au type JSON natif d’une version Oracle récente. Les conversions doivent préserver les identifiants, décimaux, dates et valeurs vides selon leur sémantique déclarée.

## SQLite — implémenté en L3a

Cible initiale proposée : base fichier locale, processus Host unique et transactions courtes ; aucun partage du fichier via stockage réseau ni HA revendiquée. Tester la contention et les reprises avec plusieurs connexions, et pas seulement en mémoire.

Le provider utilise EF Core SQLite 10.0.12. Les identifiants `Guid` sont stockés en texte hexadécimal fixe, les identifiants techniques avec collation `BINARY`, les statuts comme entiers, les JSON canoniques comme texte et les instants UTC comme millisecondes Unix entières. La révision est gérée par l’application. La migration initiale crée définitions, instances, activations, travaux, reçus idempotents et audit, avec clés étrangères et index de claim.

Le [dictionnaire du schéma physique SQLite](sqlite-schema.md) documente chaque table et colonne, les relations, index, codes d’état, règles de conservation et opérations du store associées.

Chaque mutation ouvre une connexion et une transaction `BEGIN IMMEDIATE` courte. Claim, renouvellement, commit, annulation et audit partagent cette transaction. L’heure du bail vient de SQLite avec précision milliseconde ; le claim accepte un travail Ready dû ou un bail expiré, puis avance génération et token. Les tests utilisent plusieurs connexions contre un fichier, prouvent un seul gagnant et refusent le commit de l’ancien propriétaire après reprise. Aucun mode WAL ni affaiblissement de `synchronous` n’est imposé ; ces réglages restent à mesurer avec Q06.

## Oracle — implémenté et qualifié en L3b

Minimum produit : 19c. Tester réellement 19c pour toute déclaration de compatibilité minimale. Une version plus récente n’est pas qualifiée automatiquement ; inscrire les versions et patchs testés dans le manifeste de release.

Le provider utilise Oracle Entity Framework Core `10.23.26301`, avec compatibilité SQL configurée pour Oracle Database 19c. [Prérequis EF Oracle](https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/InstallEFCoreRequirements.html).

Les chaînes obligatoires, notamment le sujet d’acteur, refusent `null` et la chaîne vide ; les textes facultatifs vides sont normalisés en `NULL` pour une sémantique identique à Oracle. Les reçus utilisent une empreinte SHA-256 de composantes UTF-8 préfixées par leur longueur, tout en stockant et revérifiant les composantes originales. Le claim ordonne les candidats et verrouille le premier disponible avec `FOR UPDATE ... SKIP LOCKED`; génération et token assurent le fencing. Les instants proviennent de l’horloge UTC Oracle à la milliseconde, les GUID sont des `CHAR(32)` et les JSON des `CLOB`.

Le [dictionnaire du schéma physique Oracle](oracle-schema.md) décrit les tables, types, clés, index, règles de chaînes vides et privilèges de migration retenus en L3b.

Compte de migration distinct du compte d’exécution lorsque l’environnement l’exige ; pas de privilège DBA nécessaire au moteur. La topologie d’accès et le provisionnement de la base de test doivent être convenus, sans installer Oracle ou Docker implicitement.

## Transaction métier et transaction moteur

Le partage d’un DbContext ne signifie pas que tous les effets du nœud participent au commit du moteur. Par défaut, l’exécution métier se fait hors transaction moteur longue. Une atomicité métier commune nécessite un contrat explicite de contribution transactionnelle, non promis au MVP. Pour les systèmes externes : idempotence et/ou outbox, pas de transaction distribuée.

## Migrations et rétention

Schéma versionné ; vérification au démarrage et migration explicite avant démarrage du worker. Pas de DDL destructif automatique. Tester création à vide et montée de version sur un état contenant instances et tâches actives. Restaurer la base et ses artefacts compatibles ensemble.

D5 acceptée : aucun purgeur automatique des reçus au MVP initial ; la politique complète de rétention reste Q06. Les reçus d’idempotence, modules requis et audits ne doivent pas être supprimés sans vérifier leurs dépendances.

## Environnement de test

Deux compositions Docker reproductibles sont fournies et exécutées : Oracle Free publique pour la boucle ARM64 rapide et l’image Oracle Enterprise officielle `19.19.0.0` pour la qualification du minimum produit. Le healthcheck officiel exécute une requête SQL avec sortie non nulle sur erreur afin de ne pas confondre listener démarré et PDB réellement disponible. Cette infrastructure sert uniquement aux tests et n’est pas une dépendance du framework. Voir les [commandes d’intégration](../../tests/oracle/README.md) et la [preuve L3b](../evidence/l3b.md).
