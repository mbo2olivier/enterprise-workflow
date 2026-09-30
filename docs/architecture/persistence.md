# Persistance SQLite et Oracle

Choix des SGBD accepté : SQLite et Oracle à partir de 19c. Contrats et détails EF proposés, à éprouver avant stabilisation.

## Composition

Le Core reste indépendant d’EF. Les contrats du store sont purs ; l’adaptateur EF expose des mappings intégrables à un DbContext applicatif. Le Kernel utilise un contexte dédié. L’application embarquée possède les migrations de son contexte ; le Kernel possède celles du sien.

Un modèle commun ne suffit pas à rendre le SQL portable. Deux adaptateurs concrets et deux jeux de migrations sont proposés, avec contrats atomiques et tests partagés. Les opérations nécessitant du SQL spécifique restent à l’intérieur du provider.

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

La révocation de droits externes ne peut pas être rendue atomique avec une transaction SQL locale. Recontrôler l’autorisation au moment de la commande ; protéger aussi l’affectation et la révision persistées au commit. Les garanties de fraîcheur sont documentées dans le contrat de sécurité.

## Modèle physique à produire au lot L2

Contraintes uniques : définition/version, reçu/portée/clé, travail logique de suite, résolution d’attente. Révision abstraite entière mise à jour conditionnellement ; ne pas imposer `rowversion` SQL Server.

Index attendus : travaux par état/échéance ; baux expirés ; tâches ouvertes par sujet ou groupe ; instances par définition/version/statut ; outbox par état/échéance. Valider les tailles, collations et plans réels avant de promettre un débit.

UTC au contrat ; encodage SQL explicite. JSON versionné stockable en texte, avec limites de taille définies avant exposition réseau ; pas de dépendance fonctionnelle au type JSON natif d’une version Oracle récente. Les conversions doivent préserver les identifiants, décimaux, dates et valeurs vides selon leur sémantique déclarée.

## SQLite

Cible initiale proposée : base fichier locale, processus Host unique et transactions courtes ; aucun partage du fichier via stockage réseau ni HA revendiquée. Tester la contention et les reprises avec plusieurs connexions, et pas seulement en mémoire.

Le provider a des limites de types, de migrations et de jetons de concurrence générés par la base. Choix proposé : conversions explicites des dates UTC pour les comparaisons SQL, révision gérée par l’application et migrations testées sur copie. [Limites officielles](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations).

Claim : mise à jour conditionnelle et transaction courte, avec stratégie de verrouillage/retry à valider par la suite de conformité. WAL et paramètres de synchronisation seront évalués avec les objectifs de durabilité ; aucun réglage de performance ne doit affaiblir silencieusement la garantie de reprise.

## Oracle

Minimum produit : 19c. Tester réellement 19c pour toute déclaration de compatibilité minimale. Une version plus récente n’est pas qualifiée automatiquement ; inscrire les versions et patchs testés dans le manifeste de release.

La documentation Oracle annonce EF Core 10 à partir d’Oracle Entity Framework Core 23.26.0. La version exacte du package sera épinglée après restauration et tests. [Prérequis EF Oracle](https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/InstallEFCoreRequirements.html).

Points à éprouver : transactions de claim, fencing, chaîne vide/null, booléens et nombres, précision UTC, LOB/JSON texte, identifiants et noms de contraintes, limites d’index, migrations 19c et privilèges minimaux. Choisir le SQL de claim à partir d’un prototype concurrent, sans transposer un SQL SQLite.

Compte de migration distinct du compte d’exécution lorsque l’environnement l’exige ; pas de privilège DBA nécessaire au moteur. La topologie d’accès et le provisionnement de la base de test doivent être convenus, sans installer Oracle ou Docker implicitement.

## Transaction métier et transaction moteur

Le partage d’un DbContext ne signifie pas que tous les effets du nœud participent au commit du moteur. Par défaut, l’exécution métier se fait hors transaction moteur longue. Une atomicité métier commune nécessite un contrat explicite de contribution transactionnelle, non promis au MVP. Pour les systèmes externes : idempotence et/ou outbox, pas de transaction distribuée.

## Migrations et rétention

Schéma versionné ; vérification au démarrage et migration explicite avant démarrage du worker. Pas de DDL destructif automatique. Tester création à vide et montée de version sur un état contenant instances et tâches actives. Restaurer la base et ses artefacts compatibles ensemble.

Politique de rétention en attente de Q06 : aucun purgeur automatique au MVP initial. Les reçus d’idempotence, modules requis et audits ne doivent pas être supprimés sans vérifier leurs dépendances.
