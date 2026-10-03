# Glossaire

| Terme | Signification |
| --- | --- |
| Runtime | Bibliothèque d’ordonnancement et d’exécution durable Enterprise Workflow |
| Kernel / Host | Application d’accueil : configuration, DI, sécurité, HTTP, UI, modules et worker |
| Runtime .NET | Environnement d’exécution Microsoft, distinct du moteur de workflows |
| Embedded | Moteur intégré à une application propriétaire de son hébergement |
| SDK | Contrats et construction des définitions en C# |
| Studio | Concepteur visuel réservé au développement, après le MVP |
| Module | Code métier, déclarations, dépendances et ressources packagés |
| Définition | Graphe identifié, validé et versionné |
| Instance | Exécution durable d’une définition et de ses artefacts précis |
| Exécution de nœud | Activation logique pouvant comporter plusieurs tentatives |
| Travail | Unité persistée réclamée par un worker |
| Bail | Possession temporaire vérifiée au commit |
| Fencing token | Génération de possession permettant de rejeter un ancien worker |
| Idempotence | Répétition d’une opération logique sans duplication de son résultat métier |
| Outbox | Intentions de livraison persistées dans la transaction du moteur |
| RID | Identifiant .NET de système et architecture, par exemple win-x64 |
| Organisation | Périmètre d’une installation indépendante |
| Authentification | Établissement de l’identité d’un acteur |
| Autorisation | Permission ou refus d’une action sur une ressource |
| Affectation | Désignation des candidats à une tâche |
| Qualification | Preuves sur une combinaison explicite de versions et plateformes |
| Stage | Nœud du workflow vu dans l’administration ; étape avec participants habilités et actions permises, sans entité distincte |
| Habilitation de nœud | Droit d’une identité ou d’un profil sur une action précise d’un nœud et d’une version de workflow |
| LDAP | Protocole d’accès à un annuaire ; schéma et capacités configurés par extension, indépendamment d’AD |
