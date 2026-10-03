# Exploitation — procédures cibles

Statut : runbook de conception. Les migrations SQLite L3a et Oracle L3b sont exécutables par API ; les commandes Host, sauvegarde et restauration restent à livrer avec leurs lots.

## Installation et configuration

Choisir l’archive correspondant au RID, à l’OS et aux providers qualifiés. Vérifier empreinte et version. Préparer un répertoire de configuration et un emplacement de données accessibles au compte de service, avec les droits nécessaires seulement.

Configurer stockage, catalogue de modules, fournisseur d’authentification/autorisation, adresse d’écoute, logs et paramètres worker. Les secrets proviennent du mécanisme approuvé de l’installation, sans présence dans les modules ni dans le dépôt. Les paramètres ont un schéma et une validation au démarrage.

Construire le registre L4a en un seul passage avec `AddEnterpriseWorkflowHandlers`. Une clé globale dupliquée ou une seconde configuration du registre doit arrêter la composition. Avant toute publication exécutable, appeler `WorkflowDefinitionBindingValidator.Validate(...).EnsureValid()` ; une clé absente ou un rôle incompatible empêche la readiness. Chaque tentative résout ensuite son handler exact dans un scope DI court, disposé après l’appel métier.

Lancer les migrations explicitement avant le worker. Pour SQLite, la racine de composition construit `SqliteWorkflowDatabase` avec sa chaîne de connexion et attend `MigrateAsync`, puis construit `SqliteWorkflowStore`. Oracle suit le même cycle avec `OracleWorkflowDatabase` et `OracleWorkflowStore`; le compte de migration doit pouvoir créer les objets décrits dans le dictionnaire Oracle. Aucun store ne crée ni ne modifie le schéma implicitement. Démarrer le Host et vérifier readiness, accès au catalogue et extension de sécurité active. Une base inaccessible, un schéma incompatible ou un module requis absent ne doit pas produire un service déclaré prêt.

Les dictionnaires des schémas [SQLite](architecture/sqlite-schema.md) et [Oracle](architecture/oracle-schema.md) permettent d’identifier les tables, clés et index lors d’un diagnostic. Ils ne constituent pas une autorisation de modifier les lignes manuellement.

Le processus peut être intégré à l’hébergement choisi par l’utilisateur. Les exemples de service Windows/Linux/macOS seront livrés après qualification ; ni IIS ni Docker n’est un prérequis général.

## Premier administrateur

Configurer le fournisseur avant création du premier droit d’administration. Bootstrap local explicite avec sujet stable ; pas de compte universel précréé. Une extension locale gère son credential ; AD reste propriétaire du mot de passe distant. Les détails seront fixés au lot L5 avec tests de réexécution et d’impossibilité de bootstrap public permanent.

## Ajouter ou mettre à jour un module

1. Vérifier manifeste, empreinte, contrats et dépendances ; préparer configuration locale et droits.
2. Identifier les instances et formulaires qui utilisent les anciennes versions.
3. Vérifier coexistence qualifiée ; à défaut, bloquer l’opération incompatible et suivre le drainage décidé.
4. Sauvegarder état et artefacts cohérents ; arrêter le Host selon procédure.
5. Déployer dans un répertoire de version immuable ; conserver les versions encore requises.
6. Redémarrer, vérifier registre/readiness puis exécuter un démarrage de contrôle autorisé.

La copie entre organisations utilise le package complet mais aucune donnée, session ou configuration secrète source. Les mappings d’identité et services métier doivent être fournis dans l’installation cible.

## Sauvegarde et restauration

Ensemble cohérent : base, artefacts de modules et formulaires, configuration non secrète, versions du Host/contrats et accès aux secrets nécessaires. Les clés de protection des sessions demandent une politique explicite ; leur perte peut imposer une reconnexion sans autoriser de contournement.

Pour SQLite, utiliser une sauvegarde cohérente avec son mode de journalisation ; une simple copie du fichier actif ne constitue pas une procédure validée. Pour Oracle, coordonner la sauvegarde avec l’exploitation et vérifier son point de restauration. L’outil et les objectifs RPO/RTO sont à décider avec Q06.

Restaurer d’abord dans un environnement isolé ; empêcher les appels aux systèmes métier réels pendant la vérification. Contrôler schéma, versions requises, tâches ouvertes et outbox avant activation du worker. Des messages déjà délivrés après le point de sauvegarde peuvent être relivrés : les destinataires doivent conserver leurs garanties d’idempotence.

Une restauration n’est validée qu’après reprise d’une instance en attente avec son code exact. Un retour à une ancienne version binaire n’est pas automatiquement possible après migration de schéma ; restaurer un ensemble cohérent ou suivre une migration de retour explicitement testée.

## Diagnostic

| Symptôme | Vérifications | Action sûre prévue |
| --- | --- | --- |
| Host non prêt | Base, version de schéma, module requis, fournisseur | Corriger la cause ; pas de suppression silencieuse de module |
| Travail bloqué | Échéance, état, bail/génération, santé worker | Attendre/reprendre via protocole de bail ; pas de modification SQL manuelle |
| Tâche non visible | Identité fournisseur/sujet, affectation, permission, statut | Corriger le mapping par procédure auditée |
| Connexion AD en échec | TLS, disponibilité, politiques, configuration | Diagnostiquer sans log de mot de passe ; pas de repli local automatique |
| Message non livré | État outbox, délai, erreur et destination | Retry borné prévu ; visibilité de l’échec final |
| Module ancien manquant | Références des instances et catalogue d’artefacts | Restaurer l’artefact exact ; ne pas substituer la dernière DLL |

Le MVP n’offre pas de bouton générique modifiant librement un état Failed ni de reprise manuelle non spécifiée. Toute commande de reprise future doit avoir préconditions, audit et tests.

## Observabilité

Logs structurés : définition/version, module/version, instance, activation, tentative, corrélation et code d’erreur. Audit fonctionnel séparé, persisté avec les mutations critiques. Pas de promesse d’inviolabilité cryptographique.

Métriques : profondeur/âge de file, taux et durée d’exécution, conflits, baux expirés, erreurs, délais de timers et outbox. Aucun identifiant d’instance dans les labels à forte cardinalité. Traces courtes liées par corrélation ; pas de span maintenu pendant des jours.

Liveness : processus actif. Readiness : capacité à accepter/exécuter avec base, registre et dépendances indispensables disponibles. Export OpenTelemetry optionnel, sans backend obligatoire. Éviter secrets et payloads complets dans traces et logs.

## Rétention et mise hors service

Rétention à définir avant données réelles ; pas de purge automatique implicite. Avant suppression d’un module, compte ou fournisseur : examiner instances actives, tâches affectées et audit à conserver. Avant retrait d’une installation : traiter les processus en cours et préserver les éléments exigés par sa politique locale.
