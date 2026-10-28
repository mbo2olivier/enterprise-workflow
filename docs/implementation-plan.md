# Plan d’implémentation

Statut : plan proposé, sans engagement de calendrier. Le périmètre est confirmé ; chaque mécanisme dépend de son ADR. Les lots L1 à L4b sont **terminés localement**. L5 est implémenté et qualifié localement sur SQLite et Oracle Enterprise 19.19. L5a est terminé et qualifié sur OpenLDAP/LDAPS Linux CI. L5b est terminé et qualifié sur SQLite et Oracle Enterprise 19.19. L6 est terminé et qualifié sur SQLite, Oracle Enterprise 19.19 et la matrice GitHub Actions Linux/macOS/Windows. L7a et L7b sont terminés et qualifiés par cette même matrice ; L7b est également qualifié localement sur SQLite et Oracle Enterprise 19.19 après acceptation de G3-A/M1-A/M2-A. L8 est terminé et qualifié sur la solution complète, Playwright Linux, Oracle Enterprise 19.19 et GitHub Actions. L8b est implémenté et sa matrice CI, Playwright Linux et OpenLDAP/LDAPS paginé sont confirmés ; Oracle 19 est confirmé par le porteur (13 tests réussis, aucun échec ni ignoré). La qualification intégrée LeaveRequest et SMTP est reportée explicitement à la sortie de L9. L9 est implémenté et vérifié localement ; le porteur confirme le 23 octobre 2026 la validation de sa pipeline GitHub. Les compléments fonctionnels listés dans la preuve restent distincts de cette confirmation. L9b est **terminé le 27 octobre 2026** : implémentation et corrections livrées, tests lourds locaux et pipeline GitHub confirmés réussis par le porteur. Le gate de sortie L9b est fermé ; L10 est **clos le 28 octobre 2026**, sur confirmation du porteur : implémentation réussie, release créée et packages publiés sur NuGet. L10b est **démarré le 28 octobre 2026**, après validation par le porteur de la structure du wiki et de LeaveRequest comme fil rouge ; L11 n’est pas démarré.

Les prérequis et arbitrages de L2 sont détaillés dans la [préparation du lot L2](l2-readiness.md). Les décisions D1 à D6 sont acceptées (ADR 0013) ; aucun arbitrage bloquant ne reste pour démarrer L2.

## Portes de décision

G0 — franchie : ADR 0006 à 0008 acceptés, avec retour confirmé au MVP séquentiel sans parallélisme. `global.json` épingle strictement le SDK `10.0.401` afin que les dépendances ASP.NET implicites restent cohérentes avec les lock files sur chaque OS. Toute montée de SDK implique leur régénération contrôlée. GitHub Actions utilise les runners `ubuntu-24.04`, `windows-2025` et `macos-15`. La réponse sur la licence peut attendre la publication.

G1 — franchie pour L4b : contrats L2 cadrés par D1 à D6 ; encodage et atomicité qualifiés en L3 ; identité globale `(HandlerId, HandlerVersion)`, résultats bornés et validation avant publication acceptés dans l’ADR 0014. Claim atomique, paramètres configurables, classification des erreurs, outbox et conservation de version d’état sont acceptés dans l’ADR 0015.

G1a — avant tout workflow dépendant d’une réponse distante : décider du modèle durable requête/attente/callback, notamment corrélation, authentification du callback, idempotence, timeout, annulation et reprise. Une intention outbox L4b convient aux commandes asynchrones ; elle ne prétend pas fournir une réponse synchrone. L6 confirme que ses attentes humaines/temporelles ne sont pas réutilisées comme faux callback technique ; cette porte reste à franchir lorsqu'un connecteur demandera une réponse distante.

G2 — franchie pour L5a : les décisions historiques L5 sont consignées par l’ADR 0016. L’ADR 0018 remplace l’obligation AD par une extension LDAP générique, en lecture seule, avec profils directs et groupes facultatifs. La qualification OpenLDAP réelle est acquise localement en clair explicite et sur Linux CI en LDAPS ; elle ne constitue pas une revendication Active Directory ni une qualification LDAP réelle sur Windows/macOS.

G2s — franchie : besoins LDAP et stage = nœud acceptés (S12/S13), R1 qualifié par L5a, R2-A/R3-A/R5-A/R6-A/R7-A appliqués par L5b et R4 matérialisé par D6-A en L6. Contraintes et décisions : [recadrage](spec-ldap-stages.md). Aucune entité Stage supplémentaire n’est introduite.

G2a — franchie pour L6 : Q11 est résolue par l'ADR 0017 et D1-B/D2-A à D7-A sont acceptées dans l'ADR 0020. L'administration habilite les approbateurs via les profils internes ; au démarrage ou à l'activité concernée, l'utilisateur désigne explicitement une identité parmi les personnes autorisées. Aucun recours obligatoire à l'attribut AD `manager`. Les contrats typés `HumanTask`/`Timer`, formulaire et handler versionnés, idempotence liée à l'acteur et au contenu, séparation versionnée et inbox bornée constituent désormais la cible d'implémentation. Auto-approbation configurable et interdite par défaut reste conforme à l'ADR 0011.

G2b — franchie pour L7a/L8 : D1-A retient Blazor Web App, SSR statique par défaut et Interactive Server par composant ; D2-A retient composants compilés et ressources manifestées/hachées sous URL immuable. Bibliothèque graphique du Studio et outillage spécifique à celui-ci attendent L11. Voir ADR 0021.

G3 — franchie pour L7b : le porteur confirme **G3-A, coexistence nominale**. Deux versions de module, de composant Razor et de dépendance privée coexistent dans des contextes distincts ; l'artefact exact est retenu et son retrait est bloqué tant qu'il reste référencé. Le drainage demeure une opération explicite, pas le modèle nominal.

G3-M — franchie pour L7b (anciennement « G3a », renommée pour éviter la confusion avec le choix G3-A) : M1-A retient une migration explicite source → cible fournie par l’artefact exact, exécutée sans effet externe et committée atomiquement sur une instance sans bail actif. Un échec ne modifie rien et la commande reste relançable. M2-A retient un inventaire durable et le retrait bloqué tant qu’une définition persistée référence l’artefact.

G2c — franchie pour L8 : L8-D1-A à L8-D8-A arrêtent la topologie du Host et de la bibliothèque UI, le catalogue versionné de processus/formulaires, la projection des données visibles, l’intégration session/cookie, le mode d’application des migrations SQL, la mutabilité autorisée dans l’administration, le stockage des réglages et la qualification navigateur. Voir l'[ADR 0023](adr/0023-host-formulaires-et-administration-l8.md) et la [préparation du lot L8](l8-readiness.md). Le dossier `prototype/` est la référence UI et ne fait plus l’objet d’un arbitrage visuel.

G4 — avant diffusion : matrice testée, licence/noms publics, Q06 clos sans seuil contractuel (décision du porteur le 27 octobre 2026), migrations/restauration et connecteurs qualifiés. Pas de revendication de MVP complet sans Oracle et sécurité livrés.

## Lots et dépendances

| Lot | Travail et livrables | Dépendances | Critère de sortie |
| --- | --- | --- | --- |
| L0 | Cadrage, PRD, ADR, architecture, plan et revue des décisions | Aucune | Dossier cohérent, inconnues tracées, mécanismes du lot suivant acceptés |
| L1 — terminé | Solution minimale, Core/Abstractions/SDK, analyseurs et GitHub Actions | G0 | Build verrouillé réussi, références unidirectionnelles testées, exemple C# exécuté ; réussite GitHub confirmée par le porteur |
| L2 — terminé localement | Modèle canonique, validateur, DSL et contrats de store | L1, G1 pour store final | Graphe normalisé et hashé ; erreurs localisées ; table de transitions, déduplication et fencing testés au niveau contrat |
| L3a — terminé localement | Adaptateur SQLite et migrations | L2 | Création/claim/commit/reprise sur base fichier réelle |
| L3b-I — terminé localement | Adaptateur Oracle, mappings, migration et composition Oracle Free légère | L2, Docker | Même suite de conformité réussie sur Oracle Free réel ; différences documentées ; aucun test ignoré présenté comme réussi |
| L3b-Q — terminé localement | Qualification du minimum produit sur l’image officielle Oracle Enterprise 19.19 | L3b-I, accès registre | Migration et six tests d’intégration réussis sur 19.19 ; version et digest consignés dans la preuve |
| L4a — terminé localement | Contrats d'exécution, résultats explicites, contexte borné, registre versionné initial et résolution par scope DI | L2 ; ADR 0014 | Un handler concret est enregistré, validé puis résolu par sa clé durable ; configuration invalide, clé absente ou ambiguë refusée explicitement |
| L4b — terminé localement | Worker, Start/Service/End, retries configurables, idempotence et outbox bornée, utilisant exclusivement le registre L4a | L3a, L3b-I, L4a ; qualification finale avec L3b-Q ; ADR 0015 | Start → Service concret → End ; arrêt/reprise par expiration ; fencing et effet externe simulé dédupliqué sur SQLite, parcours complet qualifié sur Oracle 19.19 |
| L5 — socle historique implémenté | Contrats sécurité, extension locale, AD et exemple API ; administration des capacités et profils internes, associations directes et mappings AD facultatifs | L1, G2 ; L3 pour les stores concrets | Code, comptes locaux, sessions, endpoints, profils et stores SQLite/Oracle qualifiés ; login AD réel historiquement non exécuté ; cible remplacée par qualification LDAP L5a |
| L5a — terminé et qualifié Linux CI | Extension LDAP générique : schéma, codecs, capacités, documentation et annuaire OpenLDAP réel ; rupture assumée de l’ancien connecteur AD | L5 ; ADR 0018 ; R1 décidé | Bind, identité stable après renommage, recherche, groupes, statut, panne et clair explicite qualifiés localement ; LDAPS et refus du mauvais nom qualifiés par le job Linux GitHub Actions ; pas de revendication AD ni de LDAP réel Windows/macOS |
| L5b — terminé et qualifié SQLite/Oracle 19.19 | Habilitations workflow/version/nœud/action (stage = nœud), contexte serveur, catalogue typé, migrations, services/endpoints admin, candidats éligibles, révision et audit ; aucune inbox ni claim durable simulés avant L6 | L5a ; ADR 0019 ; R2-A/R3-A/R5-A/R6-A/R7-A | Grants exacts positifs et révocables, refus hors contexte ; permissions globales sans accès métier implicite ; catalogue validé ; migrations et tests SQLite/Oracle réussis |
| L6 — terminé et qualifié CI/SQLite/Oracle 19.19 | `HumanTask`/`Timer` typés dans le schéma canonique v1 pré-release, affectation/claim/release, formulaire et handler versionnés, complétion idempotente, séparation d'acteurs, inbox bornée, timer et annulation ; aucun rendu UI | L4b, L5a, L5b, G2a ; ADR 0020 | Approbation durable, acteur non autorisé et auto-approbation refusés, double soumission contrôlée, courses claim et annulation/timer testées ; migrations SQLite/Oracle, sample et snapshots v1 à jour ; matrice GitHub Actions confirmée |
| L7a — terminé et qualifié CI | Prototype loader expérimental, versions simultanées, dépendances privées, alimentation du registre L4a, composants Razor SSR et ressources UI manifestées/hachées | L2, L4b ; D1-A à D4-A ; ADR 0021 | Coexistence A/B et dépendances de même nom prouvées sur Linux/macOS/Windows ; collision exacte et ressource altérée refusées ; G3-A confirmée pour L7b |
| L7b — terminé et qualifié CI/SQLite/Oracle 19.19 | Kernel, loader et registre définitifs ; inventaire durable et migrations d’état explicites | L7a, G3, G3-M, L5 ; ADR 0022 | Nouveau module chargé au redémarrage ; retrait incompatible bloqué ; migration atomique qualifiée SQLite et Oracle Enterprise 19.19 ; matrice GitHub Actions confirmée, verrouillage Windows des DLL pris en compte |
| L8 — terminé et qualifié solution/Playwright Linux/Oracle 19.19/CI | UI métier, schémas et rendu des formulaires, composants Razor personnalisés, thèmes et administration complète ; traduction fidèle et réutilisation du système visuel et des parcours déjà validés dans `prototype/` | L6, L7b, G2b, G2c ; ADR 0023 | Parcours et tests de la solution réussis ; smoke navigateur Linux, Oracle Enterprise 19.19 et matrice GitHub Actions confirmés le 7 octobre 2026 |
| L8b — implémenté, qualifié CI/Playwright Linux/OpenLDAP-LDAPS/Oracle 19 ; validation intégrée en sortie L9 | SDK fluent, catalogue de permissions métier, affectation depuis l’activité précédente, formulaires standard et notifications de pool | L8 ; ADR 0024 ; points ouverts levés par sous-lot | Matrice, smoke navigateur, pagination LDAP et Oracle 19 acquis ; parcours métier complet et SMTP réservés à la qualification conjointe de sortie L9 |
| L9 — implémenté, pipeline GitHub validée par le porteur ; compléments tracés | CLI, templates, `ew dev` (runtime local SQLite + UI), pack et exemple embedded | L7b, L8, L8b ; ADR 0025 | Depuis copie propre : génération, développement navigateur multi-acteurs, reprise et reconstruction contrôlée, validation, package et chargement ; parcours LeaveRequest complet et SMTP Docker Compose qualifiés avant clôture ; [spécification](l9-spec.md) |
| L9b — terminé le 27 octobre 2026 | Détail universel des instances, recherche autorisée, recherche/export audit, API médias SDK/framework et bibliothèque, projections et rapports TAT | L9 ; ADR 0026/0027 et A1–A7 acceptés dans la [spécification L9b](l9b-spec.md) | Contrats et implémentation qualifiés ; tests lourds locaux et pipeline GitHub validés par le porteur le 27 octobre 2026 ; [clôture et preuves](evidence/l9b.md) |
| L10 — clos le 28 octobre 2026 | Qualification, distribution, publication des packages publics sur NuGet.org et runbooks | L3b-Q, L9, L9b, G4 | Matrice publiée sur preuves, restauration exécutée, artefacts vérifiés, packages publics publiés et consommés depuis NuGet.org sur environnement propre ; MVP livrable |
| L10b — démarré le 28 octobre 2026 | Site documentaire public Astro/Starlight en anglais dans `wiki/`, publié sur GitHub Pages, et README racine en anglais pointant vers ce wiki ; `docs/` conservé pour les spécifications et le travail porteur/agent | L10 | Site construit et déployé via GitHub Actions, parcours de prise en main et d’extension vérifiés avec les packages NuGet publics ; README sans liens vers les spécifications internes |
| L11 | Studio initial et export C# | MVP, L10b, choix bibliothèque/Q09b | Export compilable et modèle équivalent ; absent du Host production |

L’audit et l’instrumentation commencent avec L3/L4 ; ils ne sont pas reportés à L10. Le prototype Oracle doit commencer dès L2 pour détecter une divergence avant de stabiliser le contrat SQLite. Le prototype des modules précède la stabilisation des API de plugins.

## Lot L8b — expérience auteur et contrats métier

Priorité confirmée le 20 octobre 2026 : réaliser L8b avant de figer les templates et le packaging L9. Les principes et arbitrages D1–D9 de l’[ADR 0024](adr/0024-sdk-fluent-permissions-affectation.md) sont acceptés le 20 octobre 2026 ; O1–O5 sont résolus par ces décisions. L’implémentation est livrée. Le porteur confirme la matrice GitHub Actions, Playwright Linux et OpenLDAP/LDAPS paginé. Le porteur fournit ensuite la preuve Oracle 19 : 13 tests réussis, aucun échec ni ignoré. Il décide de reporter le parcours métier LeaveRequest complet et le SMTP à la sortie de L9 afin de les qualifier avec les nouvelles commandes. Le gate d’entrée L9 est donc levé ; la validation intégrée reste obligatoire pour clôturer L9. Cette décision remplace la condition précédente exigeant ces deux preuves avant son démarrage.

1. **L8b-0 — contrats et exemple de référence** : stabiliser une déclaration complète LeaveRequest et son aperçu résolu ; appliquer D1/D2 au binding et à la sécurité, D8 à la distribution SMTP, D6/D7 à la confirmation et aux formulaires standard. Définir le format durable et les migrations sans altérer les hashes historiques ; vérifier la politique de compatibilité au moment de l’implémentation.
2. **L8b-1 — façade SDK et métadonnées** : release textuelle, `ContractRevision` héritée et override explicite, schéma d’état obligatoire ; qualification des IDs ; classes de processus séparées ; builders de champs/stages/actions ; handlers enregistrés séparément et `.HandledBy<THandler>()` sur chaque formulaire métier. Prévoir compilation globale, diagnostics et aperçu inspectable. Le modèle canonique reste indépendant de la syntaxe.
3. **L8b-2 — permissions métier** : point `ConfigurePermissions`, catalogue ID/libellé, exigences par nœud et action ; implémenter le modèle retenu en O2 dans autorisation, administration et stores SQLite/Oracle. Ne pas cumuler silencieusement deux systèmes redondants de grants. Qualifier révocation, anciennes définitions et séparation lecture/action.
4. **L8b-3 — affectation et sélecteur** : relation source/action/cible déclarée, candidats autorisés, sélection conditionnelle selon la branche, formulaire standard/personnalisé, validation serveur et persistance atomique ; réutiliser `DesignatedAssignments` sans supposer l’UI déjà livrée. Couvrir identité arbitraire, idempotence incluant le destinataire et droits retirés. Implémenter la confirmation par l’initiateur selon O4 ; aucun mode `None` générique.
5. **L8b-4 — formulaires standard et notifications** : approbation/décision humaine avec formulaire et handler standard versionnés, remplacement explicite et projections autorisées ; pool notifié si SMTP configuré, distribution durable et bornée selon O3. Une panne de livraison ne doit pas annuler une tâche déjà activée. Aucun email requis pour utiliser l’inbox.
6. **L8b-5 — qualification et adoption** : migrer le sample LeaveRequest vers la nouvelle syntaxe ; formulaire initial créant l’instance, confirmation métier, RH, manager désigné depuis l’activité précédente, clôture Service puis End. Valider branches, anciennes révisions, reprise, concurrence, sécurité, Oracle/SQLite et navigateur. Mettre à jour documentation, snapshots et preuves ; la validation navigateur métier complète et SMTP est mutualisée avec la sortie L9. Les templates et le packaging seront figés après cette qualification intégrée.

Critère de sortie : la déclaration est lisible sans répéter les références techniques ; chaque convention est vérifiable dans l’aperçu résolu. Les permissions et politiques d’affectation restent explicites. Les tests de L8 ne sont pas réutilisés comme preuve automatique des extensions L8b.

## Lot L9 — outillage et runtime local

Périmètre enrichi le 20 octobre 2026 : `ew dev` fournit un hôte de développement sans projet Kernel ni composition embedded à écrire, SQLite persistant par défaut, configuration facultative `ew.dev.json`, identités de test et UI réutilisée. La surveillance reconstruit et redémarre en respectant les versions immuables. CLI `validate`/`inspect`/`pack`/`verify`, templates et exemple embedded complètent le lot. Voir la [spécification et ses critères de sortie](l9-spec.md) et l’[ADR 0025](adr/0025-runtime-developpement-l9.md). L9 est implémenté localement : CLI outil .NET, templates, `ew dev`, packaging, exemple embedded et stack SMTP. Voir le [guide](l9-cli.md) et la [preuve locale](evidence/l9.md). La clôture reste soumise aux preuves manquantes ; la pipeline GitHub L9 est confirmée par le porteur le 23 octobre 2026 ; cette confirmation ne remplace pas les compléments fonctionnels encore tracés dans la preuve.

### Qualification intégrée de sortie L9 — décision du porteur

Après implémentation des commandes, packager LeaveRequest avec `ew pack`, vérifier l’artefact avec `ew verify`, lancer le projet avec `ew dev` et exécuter le parcours navigateur complet sur ce rendu. Vérifier aussi le chargement du package dans le Kernel : `ew dev` prépare son propre artefact et ne remplace pas cette preuve du package produit. Couvrir confirmation, pool RH, désignation manager, approbation/refus, commentaires, droits négatifs, reprise, ressources et anciennes révisions.

L9 doit livrer une infrastructure Docker Compose de qualification SMTP : serveur de capture local avec vrai protocole SMTP et consultation des messages, image/version fixées lors de l’implémentation, ports locaux, contrôle de disponibilité, commandes de démarrage/arrêt et procédure de nettoyage. Durant le même parcours, activer explicitement SMTP dans le runtime de développement, vérifier destinataires éligibles, liens et contenu minimal, fan-out paginé, puis arrêt/reprise du SMTP et du runtime avec retries durables. L’inbox doit rester utilisable pendant la panne. Conserver aussi le scénario sans SMTP.

Cette infrastructure est un moyen de test facultatif ; Docker n’est pas requis pour `ew dev` ordinaire. Le transport SMTP est réel vers un serveur de capture, sans prouver la délivrabilité Internet ni qualifier TLS/authentification d’un serveur de production. Consigner commandes, versions, environnement, résultats et limites. La [preuve locale L9](evidence/l9.md) consigne désormais quatre parcours Chromium réussis, la panne/reprise SMTP réelle et le chargement du package dans le Kernel. La pipeline GitHub est confirmée par le porteur le 23 octobre 2026. Les compléments de qualification restent tracés avant clôture L9 ; le gel final des templates/packages est désormais reporté après L9b.

## Lot L9b — enrichissements avant publication

Introduit le 23 octobre 2026 à la demande du porteur. Les cinq besoins sont confirmés ; le DTO d’état annoté, sa version explicite, les migrations et le mapping initial/humain/service sont acceptés le 25 octobre 2026 ([ADR 0026](adr/0026-etat-metier-type-et-validation.md)). Les recommandations A1–A7 sont également acceptées le 25 octobre 2026 ([ADR 0027](adr/0027-consultation-medias-audit-tat-l9b.md)) et détaillées dans la [spécification L9b](l9b-spec.md). Ce lot ajoute les exigences EF-17 à EF-21 au PRD sans renuméroter L10/L11.

Ordre : L9b-0 contrats/arbitrages, L9b-1 état métier DTO/versionné, validation, mapping et détail/recherche sécurisés, sélecteur distant des destinataires (dette UX du 25 octobre 2026, §2.1 de la spécification), L9b-2 audit et export, L9b-3 médias et bibliothèque, L9b-4 projections/rapports TAT, L9b-5 qualification intégrée. Les sous-lots médias et TAT dépendent du cadrage des droits ; la qualification finale couvre leur interaction. Le format canonique du moteur et les hashes historiques restent préservés ; les nouvelles définitions portent le contrat DTO dans leur configuration. Les tables additives et les chemins de commit évoluent explicitement.

Implémentation locale du 25 octobre 2026 : DTO/annotations et validation, initialiseur, lecture/recherche sécurisée, sélecteur distant, audit/export, staging/bibliothèque médias et projections/rapports TAT sont ajoutés. Le template et LeaveRequest (release 1.1.0, révision 2) les exercent. Voir le [guide SDK et exploitation](l9b-sdk.md) et la [preuve locale L9b](evidence/l9b.md). Le gate de qualification, initialement ouvert, est fermé le 27 octobre 2026 après confirmation par le porteur des tests lourds locaux et de la pipeline GitHub. L10 est démarré le 27 octobre 2026 ; voir le [runbook de release](l10-release.md).

L10 dépend désormais de la sortie de L9b : packages SDK/API, templates, samples, stockage, migrations, restauration et runbooks doivent intégrer ces nouveaux contrats avant publication. Les décisions, contrats et corrections de L9b sont stabilisés et qualifiés à sa clôture du 27 octobre 2026. La publication publique et les vérifications de distribution propres à L10 restent dans L10.

## Lot L10 — qualification et publication publique

**Clos le 28 octobre 2026 sur confirmation du porteur** : implémentation réussie, release créée et packages publiés sur NuGet. Démarré le 27 octobre : catalogue 1.0.0-rc.1, propriétaire NuGet `mukadi.sheba`, Apache-2.0, packages/archives, qualification et workflow par tags. Q06 clos sans seuils ni tests de charge/performance exigés. Voir le [runbook](l10-release.md) et la [preuve de clôture](evidence/l10.md#clôture--28-octobre-2026).

Périmètre confirmé le 20 octobre 2026 : L10 inclut explicitement la **publication effective sur NuGet.org** des packages publics du framework, en complément des archives Host qualifiées et des runbooks. Produire des fichiers `.nupkg` locaux ne satisfait pas ce livrable. La publication effective est désormais confirmée par le porteur à la clôture du 28 octobre 2026.

1. **L10-1 — qualification finale** : acquérir les preuves de compatibilité OS/CPU/provider revendiquées, sécurité, migrations/restauration ; Q06 est clos sans exigence de test de charge, performance ou SLA, sur décision du porteur le 27 octobre 2026.
2. **L10-2 — catalogue public et préparation des packages** : arrêter licence, propriétaires, noms publics et versions ; définir la liste des bibliothèques et adaptateurs destinés aux intégrateurs, ainsi que les packages de la CLI .NET et des templates L9. Distinguer les composants internes non publiés. Vérifier métadonnées, README, licence, dépendances, contenu des packages et absence de secrets ou données de développement.
3. **L10-3 — publication NuGet.org** : documenter et exécuter la procédure de release des packages retenus, avec authentification de publication protégée et versions cohérentes. Prévoir la reprise d’une publication partielle sans remplacer une version existante ; toute correction de contenu nécessite une nouvelle version. Les noms et versions publics sont arrêtés avant cette étape.
4. **L10-4 — validation après publication** : depuis un environnement propre, avec cache isolé et source NuGet.org explicite, restaurer les bibliothèques publiées, installer la CLI et les templates aux versions annoncées, générer puis compiler un module, exécuter `ew dev`, valider et packager, puis charger l’artefact dans le Kernel qualifié. Vérifier aussi l’exemple embedded contre les packages publics, sans référence projet au dépôt.
5. **L10-5 — documentation et preuves de release** : fournir commandes d’installation et de mise à jour, liens NuGet.org, inventaire exact des versions, notes de release, restrictions de compatibilité, checksums des artefacts distribués et runbooks d’exploitation/restauration.

Critère de sortie : packages effectivement disponibles sur NuGet.org, installation et consommation des versions publiées prouvées depuis un environnement propre, archives et restauration qualifiées, documentation publique alignée. Une publication partielle ou une installation depuis le seul flux local laisse L10 incomplet. À la clôture, le porteur confirme le lot terminé. La release publique vérifiée pour L10b est [`v1.0.0-rc.1`](https://github.com/mbo2olivier/enterprise-workflow/releases/tag/v1.0.0-rc.1), avec les packages NuGet `1.0.0-rc.1`.

## Lot L10b — wiki public et documentation développeurs

Introduit le 26 octobre 2026 à la demande du porteur, immédiatement après L10 et avant L11, sans renuméroter ces lots. Statut au 28 octobre 2026 : **implémenté et vérifié localement, publication GitHub Pages restant à confirmer**. Le lot reste ouvert jusqu’à la vérification du site effectivement déployé. La [proposition de structure et de contenu](l10b-wiki-plan.md) a été validée par le porteur, y compris LeaveRequest comme fil rouge ; l’implémentation est autorisée. L10 conserve la publication NuGet.org et sa documentation de release ; L10b construit le parcours documentaire complet à partir des versions effectivement publiées. Le détail éditorial proposé figure dans le document de cadrage lié ci-dessus. Organisation confirmée le 26 octobre 2026 : un seul dépôt pour le framework et sa documentation, un projet Astro avec Starlight autonome dans `wiki/`, et un déploiement documentaire dédié sur GitHub Pages via GitHub Actions. Cette organisation permet de faire évoluer code, exemples et documentation dans la même PR. État d’implémentation et vérifications : [preuve L10b](evidence/l10b.md).

1. **L10b-1 — espace documentaire public** : créer dans `wiki/` un projet Astro avec Starlight, avec un index et une navigation cohérente. Rédiger toutes les pages publiques en anglais en Markdown/MDX dans `wiki/src/content/docs/`. Conserver la configuration Astro, le manifeste et le fichier de verrouillage des dépendances Node dans `wiki/`, ainsi que les ressources publiques dans `wiki/public/`. Le site doit pouvoir être développé et construit depuis ce dossier indépendamment du build .NET.
2. **L10b-2 — Getting Started et Basics** : présenter Enterprise Workflow, ses prérequis, l’installation des packages NuGet, de la CLI et des templates, puis guider le développeur jusqu’à un premier workflow exécuté. Couvrir les usages Host modulaire et embedded, le cycle de développement local et les commandes usuelles.
3. **L10b-3 — Core Concepts** : expliquer définitions et instances, modules et versions, nœuds et transitions, état métier typé, handlers, exécution durable, tâches humaines, formulaires, permissions et persistance. Relier ces concepts aux API publiques et à des exemples concrets.
4. **L10b-4 — Examples et Advanced Topics** : proposer des exemples progressifs reproductibles et expliquer comment utiliser et étendre le framework : handlers et services métier, modules, formulaires personnalisés, sécurité et fournisseurs, adaptateurs, médias, migrations/versionnement, reprise et diagnostic. Distinguer les fonctionnalités livrées des limites et évolutions prévues.
5. **L10b-5 — README public** : réécrire le `README.md` racine en anglais comme point d’entrée du produit, avec présentation, démarrage rapide et liens vers les articles du site public généré depuis `wiki/`. Retirer de ce README les liens vers `docs/` et les parcours de lecture des spécifications internes.
6. **L10b-6 — séparation et validation** : conserver `docs/` dans le dépôt pour les spécifications, ADR, plans, preuves et échanges de travail entre le porteur et l’agent. Les instructions de l’agent restent inchangées : il continue à exploiter `docs/` comme actuellement. Vérifier les liens, la navigation et les exemples du wiki avec les versions publiques issues de L10, sans dépendance documentaire obligatoire à `docs/` pour le lecteur public.

7. **L10b-7 — publication du site** : ajouter un workflow GitHub Actions dédié à la documentation, construisant le projet depuis `wiki/` et déployant son résultat statique sur GitHub Pages. Vérifier la construction sur les PR qui touchent le site et ses dépendances ; publier depuis la branche de référence retenue. Configurer l’URL publique et le chemin de base pour GitHub Pages, puis vérifier les liens et ressources sur le site déployé. L’URL configurée est `https://mbo2olivier.github.io/enterprise-workflow/`, sans domaine personnalisé ; la publication effective devra être confirmée après exécution du workflow sur `main`.

Organisation cible :

```text
docs/                              # Spécifications, ADR, plans et preuves porteur/agent
wiki/
  astro.config.mjs
  package.json
  public/
  src/content/docs/
    index.mdx
    getting-started/
    basics/
    core-concepts/
    examples/
    guides/
    operations/
    advanced-topics/
    reference/
README.md                          # Anglais, point d’entrée vers le site public
```

Le dossier `wiki/src/content/docs/` contient exclusivement les articles publics Starlight ; il est distinct du dossier `docs/` à la racine. Le build documentaire ne doit pas importer ni publier automatiquement les spécifications de ce dernier.

Critère de sortie : projet Astro/Starlight `wiki/` versionné, construit et effectivement publié sur GitHub Pages par le workflow dédié, avec des pages en anglais couvrant prise en main, basics, core concepts, exemples et sujets avancés ; parcours permettant à un développeur d’utiliser et d’étendre Enterprise Workflow ; exemples et commandes vérifiés sur les packages NuGet publiés ; README racine en anglais orienté vers le wiki, sans exposition des spécifications internes dans sa navigation. `docs/` reste versionné et utilisable par l’agent selon les instructions existantes. L’ajout de ce lot au plan ne vaut ni création du wiki ni réécriture immédiate du README.

## Binding des comportements

Le DSL persistant ne sérialise ni type CLR, ni lambda, ni instance de service. Une référence de handler est une clé durable et versionnée. Le binding est livré en trois étapes distinctes :

1. **L2 — description seulement** : le builder conserve la clé et le compilateur en vérifie uniquement la forme. `NodeTypeCatalog` décrit les types de nœuds et valide leur configuration ; il ne résout aucun comportement métier.
2. **L4a — binding runtime explicite** : une application enregistre directement une classe concrète sous une clé de handler. À la publication ou au démarrage, le registre refuse les références absentes, incompatibles ou ambiguës. Pour chaque tentative, le worker ouvre un scope DI, résout le handler exact, lui fournit un contexte borné et consomme un résultat explicite.
3. **L7 — binding par module** : le loader découvre les déclarations approuvées, enregistre leurs services avant la construction du Host et alimente le même registre. Le chargement dynamique ne change pas la sémantique d'exécution établie en L4a.

L’ADR 0014 retient une clé globale `(HandlerId, HandlerVersion)`. Les applications et futurs modules qualifient leurs noms par domaine ; deux registrations sous une même clé sont refusées pendant la composition. Les résultats distinguent succès, échec retryable et échec permanent ; une décision réussie ajoute une issue déclarée. Le registre est figé après construction, les définitions exécutables sont validées avant publication et la résolution n’applique aucun fallback de version ou de rôle.

## Priorité du recadrage LDAP et stages

L5a, L5b et L6 sont terminés localement. L5b expose les nœuds comme stages administratifs et leurs actions permises ; L6 matérialise `HumanTask`, affectation, inbox, claim et complétion sans ajouter d'entité Stage. Les contrôles structurels L6 complètent toujours l'autorisation L5b. T22, T23 et le socle runtime de T24 sont acquis ; L8 fournit et qualifie désormais les parcours UI associés.

## Tâches humaines et formulaires

Une tâche humaine n'est pas un `Service` portant un champ JSON `form`. L6 ajoute un type de nœud et un DSL `HumanTask` explicites, avec affectation, issues autorisées, référence durable `FormId/FormVersion`, révision, données de soumission bornées et commande de complétion autorisée/idempotente. Son activation persiste une attente et ne conserve ni transaction ni worker pendant l'interaction humaine.

L6 définit le contrat de désignation explicite de l’approbateur (ADR 0017), sa validation serveur et sa persistance par identité stable. L5b fournit les permissions/profils contextualisés par workflow/version/nœud/action et la recherche des candidats habilités ; L8 fournit le sélecteur graphique.

L6 définit la sémantique durable et la validation métier indépendante de l'UI. L8 ajoute les schémas déclaratifs de champs, leur rendu Razor/Blazor, la validation serveur de présentation, l'inbox, les pages de tâche et l'échappatoire vers un composant Razor personnalisé. Une ancienne instance continue à référencer la version exacte du formulaire avec laquelle elle a été créée.

Le dossier `prototype/` constitue la référence UI de L8 : shell métier et administration, navigation, pages Today/catalogue/inbox/détail/demandes, action dock, formulaires, profils/identités/fournisseurs, habilitations par stage, exploitation, audit, réglages, responsive et tokens visuels. L8 réutilise cette anatomie, ses interactions et autant de CSS/tokens que raisonnablement possible, en les traduisant en composants Razor/Blazor connectés aux services réels. Le runtime React/Vite du prototype n'est pas ajouté au produit Blazor et les données simulées ne deviennent pas des raccourcis applicatifs.

## Samples progressifs

Chaque sample doit annoncer la capacité qu'il démontre et ne pas simuler une fonctionnalité future avec une configuration JSON sans sémantique :

- L2 : `DefinitionOnly`, construction et validation uniquement, avec handlers fictifs explicitement signalés comme non résolus ; aucun formulaire ni approbation humaine prétendus ;
- L4b : `ExecutableWorkflow`, classe de handler concrète, enregistrement DI, résolution par clé, exécution durable Start → Service → End et reprise ;
- L6 : `ExecutableWorkflow` étendu avec attente durable, affectation et complétion sécurisée sans dépendre encore d'un rendu graphique complet ;
- L8 : `LeaveRequest`, formulaire de démarrage, inbox, approbation/refus et notification dans le parcours UI complet.

Le sample exécutable utilise désormais le contrat typé `HumanTask`, une référence de formulaire versionnée et deux identités distinctes ; il est exécuté par la CI sur les trois OS.

## Tranches démontrables

T1 — tranche technique durable : L1, L2, L3a, L4a et L4b, Start → Service concret résolu par le registre → End. Fournir commandes de reproduction, mapping clé/classe, état avant/après arrêt et tests. Oracle est une qualification distincte à terminer, pas une raison de présenter T1 comme MVP complet.

T2 — tranche métier : L5, L5a, L5b, L6 et UI minimale de L8 ; demande, formulaire versionné, approbation après redémarrage et double clic. Cette UI peut utiliser un enregistrement direct L4a avant le loader complet pour tester les contrats.

T3 — produit modulaire : L7 à L10 ; module packagé, administration selon fournisseur, exemple embedded, SQLite et Oracle, distributions testées. Cette tranche atteint le MVP lorsque tous ses critères sont satisfaits.

T4 — Studio : L11, sans changer la sémantique du moteur pour contourner les limites du designer.

## Structure initiale proposée

```text
src/EnterpriseWorkflow.Abstractions/
src/EnterpriseWorkflow.Core/
src/EnterpriseWorkflow.Sdk/
src/EnterpriseWorkflow.Persistence.Abstractions/
tests/EnterpriseWorkflow.Core.Tests/
samples/ExecutableWorkflow/
docs/
```

Ajouter Runtime, adaptateurs, sécurité et Host au fil des lots ; ne pas créer d’emblée des dizaines de projets vides. L2 ajoute uniquement `Persistence.Abstractions` aux trois bibliothèques initiales ; aucun projet EF ou provider vide n’est anticipé.

## Risques et réponses

| Risque | Réponse | Décision/proof avant |
| --- | --- | --- |
| Divergence SQLite/Oracle | Suite de conformité identique sur Oracle Free, puis répétition sur le minimum 19.19 | L3b-I puis L3b-Q |
| Code ancien indisponible | Artefacts immuables, coexistence prouvée ou drainage | L7b |
| Handler textuel sans implémentation ou collision de clé | Identité arrêtée avant L4a, validation contre registre et refus des doublons | L4a |
| Sample présentant une capacité non exécutable | Samples progressifs nommés par capacité et critères de sortie exécutés | Chaque lot |
| Schéma/capacités LDAP non uniformes | Configuration explicite et annuaire non AD réel ; AD qualifié seulement si revendiqué | L5a |
| Permission globale interprétée comme droit sur tous les nœuds | Grants exacts, refus par défaut et contexte serveur fiable | L5b/L6 |
| Extension locale couplée au Core | Tests de références et fonctionnement sans comptes locaux | L5 terminé |
| UI externe ou assets Razor non chargeables | Prototype ressources et contrat UI | L7b/L8 |
| MVP annoncé sur seule base SQLite | Gate de qualification Oracle et tests end-to-end | L10 |
| Dimensionnement propre aux utilisateurs | Aucune garantie chiffrée ; pas de test de seuil Q06 exigé par le porteur (27 octobre 2026) | Q06 clos |
| Multiplication des distributions | Matrice explicite avec niveaux de qualification | G4 |

## Terminé pour chaque lot

Code compilable, tests pertinents exécutés, commandes de reproduction, documentation mise à jour et limites connues. Tout sample distingue explicitement définition, exécution, interaction humaine et rendu UI ; il ne présente pas une référence textuelle ou un JSON libre comme une fonctionnalité opérationnelle. Les tests non exécutés sont signalés avec motif. Toute nouvelle ambiguïté structurante est remontée avant de poursuivre la partie concernée.

## Historique L8b — premier incrément

D1–D9 confirmés ; aucune question produit O1–O5 en attente. Premier socle ajouté : `WorkflowModuleInfo` immuable avec ID, release et révision distincts, métadonnées de présentation, qualification validée des IDs globaux ; surcharge `WorkflowBuilder.Create(module, localDefinitionId)` héritant de la révision et conservant les IDs de nœuds locaux. Le binding exact de l’artefact reste explicitement requis et n’est pas inventé par cette surcharge.

Ce premier incrément ne livre pas encore le point de déclaration du module, le schéma d’état obligatoire dans la nouvelle façade processus, la réutilisation des anciens contrats, les builders complets, les permissions runtime, les formulaires standard ou SMTP. Ces travaux restent dans L8b-1 à L8b-5 ; aucun statut « terminé » n’est attribué à L8b.

Vérification du premier incrément : compilation SDK et tests Core sans avertissement ; 53 tests Core réussis, aucun ignoré. Le runner requiert un canal IPC local indisponible dans la sandbox ; exécution des tests hors sandbox. Cette vérification ne qualifie pas les évolutions runtime, sécurité, UI ou SMTP encore à réaliser.

## Conclusion d’implémentation L8b — 20 octobre 2026

L8b-0 à L8b-5 sont implémentés : builders de module/processus/champs/actions, schéma obligatoire et révisions héritées, références d’anciens contrats avec propriétaire exact, permissions métier cumulatives et alternatives, affectation à l’initiateur ou depuis l’action précédente, formulaires standard remplaçables, résumé autorisé, récupération explicite d’affectation et notifications SMTP par fan-out durable paginé. Le sample LeaveRequest utilise ces contrats ; le manifeste peut être produit depuis sa déclaration auteur.

Compilation Debug de la solution et 116 tests légers locaux réussis ; la cible de tests Oracle est compilée séparément. Voir la [preuve](evidence/l8b.md). Le porteur confirme ensuite Release/CI sur les trois OS, OpenLDAP/LDAPS paginé et Playwright Linux. La preuve Oracle 19 est ensuite fournie par le porteur : 13 tests réussis, aucun échec ni ignoré. La [validation restante](l8b-validation.md) porte le SMTP et le parcours métier LeaveRequest complet, explicitement mutualisés avec la qualification de sortie L9.

### Ultime itération L9b — implémentation locale du 26 octobre 2026

Périmètre consigné dans [l9b-spec.md](l9b-spec.md) : validateurs intégrés de formats bureautiques usuels, préparation de l’extension des formats, lecture générique `ReadState`/`ReadSubmission` et remplacement typé `NodeStateUpdate.Replace<T>`. Ces évolutions sont implémentées localement ; leur contrat effectif est documenté dans le guide SDK. La qualification finale, initialement déléguée selon la [recette](validation/l9b-final-recette.md), est confirmée par le porteur à la clôture du 27 octobre 2026. Le mapping déclaratif `RequireCommentAt`/`CommentAt` vers une collection de commentaires structurés et le DTO obligatoire pour tous les processus sont acceptés (ADR 0028). Propager le type du state dans le fluent ; utiliser le handler intégré aux approbations pour ajouter le commentaire et valider le state avant le commit, sans handler métier supplémentaire. Migrer les tests internes, fixtures, exemples et templates vers ce contrat, et retirer les parcours de compatibilité prépublication : aucune version du framework n’a encore été publiée. Les versions de schéma, migrations et définitions exactes par instance restent nécessaires au produit.

### Corrections après recette M1–M7

Référence publique sur dix caractères avec unicité SQLite/Oracle, composant de ligne partagé, onglets dédiés du détail, métadonnées `[CommentState]`, références médias exclues du tableau, bibliothèque indépendante du state, raccordement de la vue de formulaire et audit admin unifié : implémentés et vérifiés localement (151 tests courts, 15 Oracle et 6 Chromium réussis). Les compléments manuels R1–R5, initialement en attente, sont couverts par la validation finale du porteur le 27 octobre 2026. Les anciennes preuves M1–M7 restent acquises pour l’itération précédente ; les ajustements utilisent le sample 1.4.0 / processus 5 / schéma 4. M8 est levé par la confirmation du passage de la pipeline GitHub par le porteur le 27 octobre 2026.

### Itération L9b — développement natif et simplifications DX

Implémentés localement : environnements nommés à `ew dev`, le lancement d'un artefact précis, `ew.dev.json` v2, le chargement des seules générations requises et un cycle de développement sans changement manuel de release à chaque reconstruction. Contrat cible et contraintes d'identité durable dans [l9b-spec.md](l9b-spec.md). Le script Python de recette n'est pas une dépendance produit. Les compléments DX sont également implémentés : succès humain sans outcome fourni par le handler et façade canonique DI, avec contrôle commun au commit. Vérification locale acquise : 160 tests locaux, 15 Oracle et 6 Chromium réussis ; la recette N1–N6 porte la surveillance longue, la distribution/SMTP, la restauration et la matrice. Voir la preuve L9b.

### Clôture L9b — 27 octobre 2026

**Statut : terminé.** Le porteur confirme avoir exécuté avec succès les tests lourds locaux et confirme le passage de la pipeline GitHub après les derniers correctifs. Cette confirmation clôt la qualification intégrée L9b-5, y compris les recettes précédemment déléguées et les correctifs CI. Les corrections finales de validation utilisateur, d’harmonisation des formulaires et de fond quadrillé sont incluses. Voir la [preuve de clôture](evidence/l9b.md), la [recette finale](validation/l9b-final-recette.md) et la [recette validation/CI](validation/l9b-validation-ci-recette.md).

L9b ne porte plus de réserve de recette bloquante. Les limites fonctionnelles documentées restent applicables (notamment TAT calendaire et résultat métier non inféré sur les définitions historiques). La clôture L9b ne constituait pas une preuve de publication L10. L10 a ensuite été démarré le 27 octobre, puis clos le 28 octobre 2026 sur confirmation du porteur ; L10b a ensuite été autorisé après validation de sa proposition éditoriale.
