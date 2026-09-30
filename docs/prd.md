# PRD — Enterprise Workflow

Version 0.1 — 30 septembre 2026. Périmètre MVP confirmé ; critères détaillés proposés. Les [questions ouvertes](cadrage.md) s’appliquent.

## Problème et résultat

Les projets d’automatisation ont besoin de composants réutilisables pour décrire les processus, les exécuter durablement et présenter les tâches. Enterprise Workflow doit éviter de réimplémenter ces mécanismes dans chaque application, tout en restant intégrable à l’environnement .NET de l’organisation.

Un développeur décrit un workflow et ses nœuds métier, compile un module et le déploie dans le Kernel. Après redémarrage, les processus autorisés apparaissent à l’accueil. Les instances survivent aux arrêts. Une autre organisation réutilise les modules dans sa propre installation et avec sa propre administration.

La distribution visée est open source. Le MVP ne revendique ni équivalence avec une suite BPM complète ni conformité BPMN.

## Utilisateurs

| Utilisateur | Besoin | Parcours MVP |
| --- | --- | --- |
| Auteur de workflow | Décrire un processus sans modifier le moteur | C#, validation, package |
| Auteur de nœud | Réutiliser une intégration métier | Contrat, DI, résultat explicite |
| Demandeur | Démarrer et suivre un processus autorisé | Accueil, formulaire, suivi |
| Approbateur | Trouver et réaliser une tâche | Inbox, formulaire, décision |
| Administrateur | Gérer les accès de son installation | Administration et audit |
| Exploitant | Installer, diagnostiquer et restaurer | Configuration, migrations, runbook |
| Intégrateur | Embarquer le moteur | Packages et intégration DbContext |

## Périmètre

MVP : moteur durable, SDK minimal, SQLite et Oracle, tâches humaines et formulaires simples, Kernel modulaire, administration et sécurité extensible, CLI et modèles de projet. Décisions exclusives, timers et retries constituent les détails techniques proposés. Le Kernel porte la démonstration ; le mode embarqué utilise le même moteur.

Le Studio appartient au produit et à la livraison suivante, sans conditionner les workflows écrits en C#. La qualification Oracle et AD exige des environnements correspondants.

## Exigences fonctionnelles

« Source » désigne une exigence rapportée par le contexte initial ; « confirmé » une réponse actuelle ; « proposé » un détail recommandé. La validation d’un besoin ne valide pas automatiquement son mécanisme technique.

| ID | Exigence | Statut | Acceptation proposée |
| --- | --- | --- | --- |
| EF-01 | Bibliothèques .NET 10 et Kernel | Confirmé / source | Même définition exécutée dans Kernel et exemple embarqué |
| EF-02 | C# sans Studio et nœuds métier extensibles | Source | Nœud extérieur au moteur, définition validée avant publication |
| EF-03 | Exécution durable | MVP confirmé | Reprise après arrêt brutal ; aucun commit par worker périmé |
| EF-04 | SQLite et Oracle 19c minimum | Confirmé | Suite de conformité réussie sur chaque provider revendiqué |
| EF-05 | Tâches humaines, formulaires et décisions exclusives | MVP confirmé / détail proposé | Approbation après redémarrage ; double soumission sans double progression |
| EF-06 | Timers, retries bornés et annulation | Proposé | Échéance passée reprise ; retries plafonnés ; aucune suite après annulation validée |
| EF-07 | Modules découverts au redémarrage | Source / confirmé | Nouveau module sans modification du Host ; incompatibilité diagnostiquée |
| EF-08 | Administration et sécurité extensibles, AD/API | Confirmé | Administration protégée ; contrats et connecteurs qualifiés |
| EF-09 | Permissions par ressource ; auto-approbation configurable par workflow, interdite par défaut | Source / auto-approbation confirmée | Refus serveur, y compris auto-approbation sans exception explicite ; exception soumise aux autres droits |
| EF-10 | Une installation par organisation | Confirmé | Même module dans deux installations sans partage implicite de données ou d’accès |
| EF-11 | Accueil, inbox, formulaires, suivi et thème ; UI métier/administration Razor/Blazor | Source / MVP et technologie confirmés | Formulaires déclaratifs et composants Razor personnalisés ; parcours complet ; logo, titre et couleurs configurables |
| EF-12 | CLI et scaffolding | Source / MVP confirmé | Projet généré, compilé, validé, packagé et chargé depuis un répertoire propre |
| EF-13 | Préservation des versions d’instances et de modules | Proposé | Coexistence prouvée ou remplacement incompatible bloqué |
| EF-14 | Audit et diagnostic | Proposé | Acteur, action et corrélation disponibles sans secret |
| EF-15 | Studio et export C# | Source / différé confirmé | Export compilable, modèle équivalent, absence du Studio en production |

EF-08 utilise des extensions : comptes locaux ou identités distantes selon l’installation, sans incidence sur le cœur. AD vérifie un identifiant et un mot de passe. L’administration expose seulement les capacités de gestion prises en charge par l’extension. Aucune API tierce universelle n’est présumée existante.

## Exigences non fonctionnelles

| ID | Exigence | Preuve |
| --- | --- | --- |
| ENF-01 | Multiplateforme .NET 10 | Matrice OS/CPU/provider explicite et testée |
| ENF-02 | Infrastructure minimale | Kernel et base suffisants ; pas de broker, conteneur ou backend de monitoring obligatoire |
| ENF-03 | Atomicité et concurrence | Crashs, double claim et double complétion |
| ENF-04 | Contrats extensibles bornés | Core indépendant d’EF, HTTP, AD et UI |
| ENF-05 | Données et droits protégés | Tests négatifs, absence de secrets dans les logs/artefacts |
| ENF-06 | Utilisabilité | Clavier, focus, labels et erreurs ; textes français externalisés |
| ENF-07 | Exploitabilité | Migrations documentées et restauration exécutée |
| ENF-08 | Performance mesurable | Rapport matériel, données, workers et percentiles ; seuils à fixer via Q06 |

La latence d’un timer se mesure depuis son échéance, sans garantie temps réel. Débit, utilisateurs simultanés, rétention et disponibilité restent non chiffrés ; aucun SLA ne peut être déclaré satisfait avant arbitrage et mesure.

## Parcours de référence : demande de congé

1. Le demandeur authentifié voit les processus qu’il peut démarrer.
2. Il saisit dates et motif ; le serveur valide puis crée l’instance de manière idempotente.
3. Une tâche est affectée au responsable. L’exemple utilise une relation simulée, pas un annuaire supposé.
4. Le Host est arrêté puis relancé pendant l’attente : tâche et historique persistent.
5. Le responsable approuve ou refuse ; une double soumission ne produit qu’une seule suite.
6. Une notification simulée avec clé d’idempotence stable termine la branche choisie.
7. Une nouvelle version est déployée : les anciennes instances conservent leur code, ou le déploiement est refusé selon la stratégie validée.

Cas négatifs : accès non autorisé, tâche d’autrui, formulaire invalide, module absent, provider indisponible et décisions concurrentes. L’auto-approbation est interdite par défaut et configurable par workflow (ADR 0011). Une autorisation explicite ne remplace ni les droits ni l’affectation ; les tâches de saisie du demandeur restent autorisées selon leurs permissions.

## Non-objectifs proposés du MVP

Cycles, parallélisme/jointures, sous-workflows, compensation automatique, migration d’instances, événements externes avant contrat explicite, édition arbitraire de l’état SQL, hot reload, plugins non fiables, round-trip de C# libre, pièces jointes avancées, éditeur de formulaires complet et administration centrale multi-organisations.

## Acceptation du MVP

- Exigences couvertes par la [traçabilité](traceability.md).
- Parcours de congé prouvé sur SQLite et Oracle qualifié.
- Connecteurs de sécurité retenus éprouvés sur leurs systèmes réels et par tests de contrat.
- Module réutilisable dans deux installations indépendantes, sans secrets embarqués.
- Versions protégées, restauration exécutée et distributions annoncées testées.
- Décisions bloquant la livraison résolues ; licence choisie avant publication.

Après le MVP : Studio avec palette, graphe, propriétés, diagnostics, sauvegarde et export. Les autres extensions du moteur seront priorisées sur besoins identifiés.
