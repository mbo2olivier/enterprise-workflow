# PRD — Enterprise Workflow

Version 0.2 — recadrage du 3 octobre 2026. Périmètre MVP confirmé ; critères détaillés proposés. Les [questions ouvertes](cadrage.md) s’appliquent.

## Problème et résultat

Les projets d’automatisation ont besoin de composants réutilisables pour décrire les processus, les exécuter durablement et présenter les tâches. Enterprise Workflow doit éviter de réimplémenter ces mécanismes dans chaque application, tout en restant intégrable à l’environnement .NET de l’organisation.

Un développeur décrit un workflow et ses nœuds métier, compile un module et le déploie dans le Kernel. Après redémarrage, les processus autorisés apparaissent à l’accueil. Les instances survivent aux arrêts. Une autre organisation réutilise les modules dans sa propre installation et avec sa propre administration.

La distribution visée est open source. Le MVP ne revendique ni équivalence avec une suite BPM complète ni conformité BPMN.

## Utilisateurs

| Utilisateur | Besoin | Parcours MVP |
| --- | --- | --- |
| Auteur de workflow | Décrire et tester un processus sans modifier le moteur | C#, `ew dev`, navigateur, validation, package |
| Auteur de nœud | Réutiliser une intégration métier | Contrat, DI, résultat explicite |
| Demandeur | Démarrer et suivre un processus autorisé | Accueil, formulaire, suivi |
| Approbateur | Trouver et réaliser une tâche | Inbox, formulaire, décision |
| Administrateur | Gérer les accès de son installation | Administration et audit |
| Exploitant | Installer, diagnostiquer et restaurer | Configuration, migrations, runbook |
| Intégrateur | Embarquer le moteur | Packages et intégration DbContext |

## Périmètre

MVP : moteur durable, SDK minimal, SQLite et Oracle, tâches humaines et formulaires simples, Kernel modulaire, administration et sécurité extensible, CLI et modèles de projet. Le graphe séquentiel à décisions exclusives, sans boucles ni parallélisme, est accepté dans l’ADR 0007 ; la durabilité et les retries suivent l’ADR 0008 accepté. Les paramètres concrets et contrats de timers restent à préciser. Le Kernel porte la démonstration ; le mode embarqué utilise le même moteur.

Le Studio appartient au produit et à la livraison suivante, sans conditionner les workflows écrits en C#. La qualification Oracle et LDAP exige des environnements correspondants ; un domaine AD n’est plus un prérequis du MVP LDAP.

## Exigences fonctionnelles

« Source » désigne une exigence rapportée par le contexte initial ; « confirmé » une réponse actuelle ; « proposé » un détail recommandé. La validation d’un besoin ne valide pas automatiquement son mécanisme technique.

| ID | Exigence | Statut | Acceptation proposée |
| --- | --- | --- | --- |
| EF-01 | Bibliothèques .NET 10 et Kernel | Confirmé / source | Même définition exécutée dans Kernel et exemple embarqué |
| EF-02 | C# sans Studio et nœuds métier extensibles | Source | Nœud extérieur au moteur, définition validée avant publication |
| EF-03 | Exécution durable | MVP et principes ADR 0008 confirmés | Reprise après arrêt brutal ; aucun commit par worker périmé |
| EF-04 | SQLite et Oracle 19c minimum | Confirmé | Suite de conformité réussie sur chaque provider revendiqué |
| EF-05 | Tâches humaines, formulaires et décisions exclusives | MVP et décisions exclusives confirmés | Approbation après redémarrage ; double soumission sans double progression |
| EF-06 | Timers, retries bornés et annulation | Proposé | Échéance passée reprise ; retries plafonnés ; aucune suite après annulation validée |
| EF-07 | Modules découverts au redémarrage | Source / confirmé | Nouveau module sans modification du Host ; incompatibilité diagnostiquée |
| EF-08 | Administration et sécurité extensibles, LDAP/API | Confirmé | Administration protégée ; contrats et connecteurs qualifiés |
| EF-09 | Permissions par ressource ; auto-approbation configurable par workflow, interdite par défaut | Source / auto-approbation confirmée | Refus serveur, y compris auto-approbation sans exception explicite ; exception soumise aux autres droits |
| EF-10 | Une installation par organisation | Confirmé | Même module dans deux installations sans partage implicite de données ou d’accès |
| EF-11 | Accueil, inbox, formulaires, suivi et thème ; UI métier/administration Razor/Blazor | Source / MVP et technologie confirmés | Formulaires déclaratifs et composants Razor personnalisés ; parcours complet ; logo, titre et couleurs configurables |
| EF-12 | CLI, scaffolding et runtime de développement `ew dev` | MVP confirmé ; extension du 20 octobre 2026, ADR 0025 | Depuis un répertoire propre : génération, rendu et parcours navigateur sans projet Kernel à écrire, SQLite par défaut, configuration locale, identités de test, reprise, reconstruction contrôlée, validation, packaging et chargement |
| EF-13 | Préservation des versions d’instances et de modules | Proposé | Coexistence prouvée ou remplacement incompatible bloqué |
| EF-14 | Audit et diagnostic | Proposé | Acteur, action et corrélation disponibles sans secret |
| EF-16 | Stages administratifs : habilitations par workflow, nœud et action ; stage = nœud | Confirmé S12/S13 ; mécanismes ADR 0019 proposés | Parcours clientèle → superviseur → back-office maker → validateur ; refus serveur des actions hors nœud ou non habilitées ; reprise et concurrence sur les deux bases |
| EF-17 | Détail d’instance depuis tous les écrans présentant une demande | Confirmé le 23 octobre 2026 ; L9b | Vue indépendante des tâches actives, state DTO annoté/versionné, mapping explicite des formulaires et validation contextuelle ; accès et champs contrôlés côté serveur (ADR 0026) |
| EF-18 | Recherche globale de demandes autorisées | Confirmé le 23 octobre 2026 ; L9b | Loupe à gauche du profil, modale filtrée et SERP ; statut, dates, processus, initiateur réservé aux admins habilités ; aucune fuite de ressources interdites |
| EF-19 | Recherche et export de l’audit admin | Confirmé le 23 octobre 2026 ; L9b | Filtres serveur, pagination et extraction cohérente des résultats autorisés ; audit de sécurité et CSV en flux acceptés en A2/A3 |
| EF-20 | API médias commune et bibliothèque d’instance | Confirmé le 23 octobre 2026 ; L9b | Upload/génération, lecture, remplacement, suppression et validation par SDK/API ; bibliothèque filtrée, reprise et sauvegarde qualifiées |
| EF-21 | Suivi et rapports des temps de traitement | Confirmé le 23 octobre 2026 ; L9b | Projections normalisées dédiées, rapports admin/export et SQL documenté par analyste/processus/étape ; durées et couverture historique explicites |
| EF-15 | Studio et export C# | Source / différé confirmé | Export compilable, modèle équivalent, absence du Studio en production |

EF-08 utilise des extensions : comptes locaux ou identités distantes selon l’installation, sans incidence sur le cœur. L’extension LDAP vérifie un identifiant et un mot de passe sur LDAPS par défaut ; LDAP clair exige une sélection explicite de l’installation et n’est jamais utilisé comme downgrade automatique. Son schéma et ses capacités sont configurés. AD est un cas possible, soumis à qualification propre. L’administration expose seulement les capacités de gestion prises en charge par l’extension. Aucune API tierce universelle n’est présumée existante.

## Stages et administration granulaire

Un stage correspond au nœud dans l’administration (S13). L’administrateur habilite une identité ou un profil pour les actions déclarées de ce nœud dans un workflow précis. Un droit de démarrage, de consultation ou d’administration n’accorde pas implicitement le droit de traiter tous ses nœuds. Les mappings de groupes LDAP vers les profils restent facultatifs.

Le serveur déduit le nœud actif depuis l’instance durable et contrôle action, habilitation, affectation, état et révision à chaque commande. L’inbox et la recherche des candidats appliquent les mêmes droits ; une ancienne sélection ne survit pas au retrait de l’habilitation. Le choix d’un destinataire ou d’un pool est distinct de l’éligibilité. Voir la [spécification de recadrage et les arbitrages](spec-ldap-stages.md).

## Exigences non fonctionnelles

| ID | Exigence | Preuve |
| --- | --- | --- |
| ENF-01 | Multiplateforme .NET 10 | Matrice OS/CPU/provider explicite et testée |
| ENF-02 | Infrastructure minimale | Kernel et base suffisants ; pas de broker, conteneur ou backend de monitoring obligatoire |
| ENF-03 | Atomicité et concurrence | Crashs, double claim et double complétion |
| ENF-04 | Contrats extensibles bornés | Core indépendant d’EF, HTTP, LDAP et UI |
| ENF-05 | Données et droits protégés | Tests négatifs, absence de secrets dans les logs/artefacts |
| ENF-06 | Utilisabilité | Clavier, focus, labels et erreurs ; textes français externalisés |
| ENF-07 | Exploitabilité | Migrations documentées et restauration exécutée |
| ENF-08 | Performance mesurable | Rapport matériel, données, workers et percentiles ; seuils à fixer via Q06 |

La latence d’un timer se mesure depuis son échéance, sans garantie temps réel. Débit, utilisateurs simultanés, rétention et disponibilité restent non chiffrés ; aucun SLA ne peut être déclaré satisfait avant arbitrage et mesure.

## Parcours de référence : demande de congé

1. Le demandeur authentifié voit les processus qu’il peut démarrer.
2. Il saisit dates et motif ; le serveur valide puis crée l’instance de manière idempotente.
3. Une tâche est affectée à l’approbateur explicitement choisi parmi les candidats habilités pour ce nœud et cette action (ADR 0017/0019).
4. Le Host est arrêté puis relancé pendant l’attente : tâche et historique persistent.
5. Le responsable approuve ou refuse ; une double soumission ne produit qu’une seule suite.
6. Une notification simulée avec clé d’idempotence stable termine la branche choisie.
7. Une nouvelle version est déployée : les anciennes instances conservent leur code, ou le déploiement est refusé selon la stratégie validée.

Cas négatifs : accès non autorisé, tâche d’autrui, formulaire invalide, module absent, provider indisponible et décisions concurrentes. L’auto-approbation est interdite par défaut et configurable par workflow (ADR 0011). Une autorisation explicite ne remplace ni les droits ni l’affectation ; les tâches de saisie du demandeur restent autorisées selon leurs permissions.

## Non-objectifs du MVP

Cycles et parallélisme/jointures sont exclus du MVP par l’ADR 0007 accepté. Les autres exclusions proposées sont : sous-workflows, compensation automatique, migration d’instances, événements externes avant contrat explicite, édition arbitraire de l’état SQL, hot reload, plugins non fiables, round-trip de C# libre, gestion documentaire avancée (coédition, OCR, recherche plein texte), éditeur de formulaires complet et administration centrale multi-organisations.

## Acceptation du MVP

- Exigences couvertes par la [traçabilité](traceability.md).
- Parcours de congé prouvé sur SQLite et Oracle qualifié.
- Connecteurs de sécurité retenus éprouvés sur leurs systèmes réels et par tests de contrat.
- Module réutilisable dans deux installations indépendantes, sans secrets embarqués.
- Versions protégées, restauration exécutée et distributions annoncées testées.
- Packages publics publiés sur NuGet.org en L10 (bibliothèques/adaptateurs retenus, CLI et templates), avec installation et consommation des versions publiées vérifiées depuis un environnement propre.
- Décisions bloquant la livraison résolues ; licence choisie avant publication.

Après le MVP : Studio avec palette, graphe, propriétés, diagnostics, sauvegarde et export. Les autres extensions du moteur seront priorisées sur besoins identifiés.

## Évolution confirmée le 20 octobre 2026 — expérience développeur L8b

Le SDK doit permettre une déclaration fluente lisible en revue : métadonnées uniques, révision de contrats héritée avec override explicite, schéma de données obligatoire, processus séparés et identifiants locaux stables. Les formulaires métier déclarent leur handler par défaut, les enregistrements restant indépendants. Le module déclare ses permissions métier et les exigences stage/action sans connaître les profils de l’installation.

Le formulaire initial démarre l’instance ; une confirmation ultérieure est une étape métier. Une tâche peut demander la désignation de son destinataire depuis l’activité précédente, parmi ses candidats éligibles, selon la branche choisie. Les pools notifient les personnes éligibles lorsque SMTP est configuré. Les approbations/décisions humaines simples disposent d’un formulaire intégré remplaçable. Aucun `AssignmentMode.None` n’est requis par ces cas.

Implémentation L8b livrée, tests légers locaux réussis ; qualification intégrée parcours métier/SMTP restante en sortie L9. Les décisions de portée des permissions, réutilisation de révision, notification, affectation à l’initiateur et contenu des formulaires standard sont tracées dans l’[ADR 0024](adr/0024-sdk-fluent-permissions-affectation.md). Le lot est détaillé dans le [plan L8b](implementation-plan.md).

## Évolution confirmée le 20 octobre 2026 — runtime de développement L9

`ew dev` permet à l’auteur d’exécuter son module et d’explorer son rendu dans un navigateur avant packaging, sans écrire de Kernel ou d’intégration embedded. L’hôte de développement réutilise le runtime et l’UI, avec SQLite persistant, configuration JSON facultative, identités de test et surveillance par reconstruction/redémarrage. Il conserve autorisation, durabilité et artefacts immuables ; les fonctionnalités de test restent propres au développement. Voir la [spécification L9](l9-spec.md) et l’[ADR 0025](adr/0025-runtime-developpement-l9.md). Cette extension est implémentée localement ; voir le [guide CLI](l9-cli.md) et la [preuve L9](evidence/l9.md). La pipeline GitHub est confirmée par le porteur le 23 octobre 2026 ; les compléments fonctionnels restent tracés dans la preuve L9.

## Évolution confirmée le 23 octobre 2026 — lot intermédiaire L9b

Les exigences EF-17 à EF-21 entrent dans le périmètre avant publication L10. La [spécification L9b](l9b-spec.md) confronte les besoins au code et propose les contrats, règles de sécurité, cycle des médias, projections TAT, critères de sortie et arbitrages A1–A7. Les recommandations A1–A7 sont acceptées le 25 octobre 2026 dans l’[ADR 0027](adr/0027-consultation-medias-audit-tat-l9b.md) ; leur implémentation et qualification restent à réaliser. La gestion documentaire avancée reste hors périmètre ; les pièces jointes usuelles font désormais partie du socle. Le TAT ne peut être assimilé à du travail humain actif sans instrumentation complémentaire.

## Décision du 25 octobre 2026 — état métier du processus

Le processus déclare son état métier par DTO annoté, distinct des métadonnées framework et des contrats de formulaires. Les handlers assurent le mapping explicite au démarrage, dans les tâches humaines et les services. Version de schéma explicite et migrations existantes sont conservées. Les annotations centralisent libellés, visibilité du détail (champs visibles par défaut, masquage/restrictions par permissions) et validation permanente/contextuelle avant commit. Cette décision est acceptée dans l’[ADR 0026](adr/0026-etat-metier-type-et-validation.md) et détaillée dans la [spécification L9b](l9b-spec.md) ; les arbitrages A1–A7 font l’objet d’une acceptation distincte le même jour (ADR 0027).

## Complément L9b du 25 octobre 2026 — sélection des destinataires

EF-05/09/11 incluent un sélecteur réutilisable avec recherche distante partielle par nom, sans connaissance de l’identifiant technique ni chargement exhaustif de l’annuaire. Couverture : démarrage, désignation pour l’étape suivante et récupération/réaffectation autorisée. Recherche sans rechargement/perte de saisie ; sélection explicite d’une identité stable parmi les seuls candidats éligibles, puis revalidation serveur. Voir [L9b §2.1](l9b-spec.md). L’ajout concerne la préparation du lot ; aucun composant n’est encore implémenté par cette décision.

## Implémentation locale L9b — 25 octobre 2026

Les exigences EF-17 à EF-21, le state DTO et le sélecteur distant sont implémentés localement. Le [guide SDK/exploitation](l9b-sdk.md) décrit leurs contrats effectifs ; la [preuve L9b](evidence/l9b.md) distingue qualifications locales et limites. Le lot reste soumis à son gate intégré avant L10 ; les résultats L9 ne sont pas étendus par déduction.
