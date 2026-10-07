# Plan d’implémentation

Statut : plan proposé, sans engagement de calendrier. Le périmètre est confirmé ; chaque mécanisme dépend de son ADR. Les lots L1 à L4b sont **terminés localement**. L5 est implémenté et qualifié localement sur SQLite et Oracle Enterprise 19.19. L5a est terminé et qualifié sur OpenLDAP/LDAPS Linux CI. L5b est terminé et qualifié sur SQLite et Oracle Enterprise 19.19. L6 est terminé et qualifié sur SQLite, Oracle Enterprise 19.19 et la matrice GitHub Actions Linux/macOS/Windows. L7a et L7b sont terminés et qualifiés par cette même matrice ; L7b est également qualifié localement sur SQLite et Oracle Enterprise 19.19 après acceptation de G3-A/M1-A/M2-A. L8 est terminé et qualifié sur la solution complète, Playwright Linux, Oracle Enterprise 19.19 et GitHub Actions. Les lots L9 à L11 ne sont pas démarrés.

Les prérequis et arbitrages de L2 sont détaillés dans la [préparation du lot L2](l2-readiness.md). Les décisions D1 à D6 sont acceptées (ADR 0013) ; aucun arbitrage bloquant ne reste pour démarrer L2.

## Portes de décision

G0 — franchie : ADR 0006 à 0008 acceptés, avec retour confirmé au MVP séquentiel sans parallélisme. `global.json` accepte les SDK stables à partir de `10.0.100` dans les feature bands .NET 10 ; les preuves locales utilisent `10.0.102`. GitHub Actions utilise les runners `ubuntu-24.04`, `windows-2025` et `macos-15`. La réponse sur la licence peut attendre la publication.

G1 — franchie pour L4b : contrats L2 cadrés par D1 à D6 ; encodage et atomicité qualifiés en L3 ; identité globale `(HandlerId, HandlerVersion)`, résultats bornés et validation avant publication acceptés dans l’ADR 0014. Claim atomique, paramètres configurables, classification des erreurs, outbox et conservation de version d’état sont acceptés dans l’ADR 0015.

G1a — avant tout workflow dépendant d’une réponse distante : décider du modèle durable requête/attente/callback, notamment corrélation, authentification du callback, idempotence, timeout, annulation et reprise. Une intention outbox L4b convient aux commandes asynchrones ; elle ne prétend pas fournir une réponse synchrone. L6 confirme que ses attentes humaines/temporelles ne sont pas réutilisées comme faux callback technique ; cette porte reste à franchir lorsqu'un connecteur demandera une réponse distante.

G2 — franchie pour L5a : les décisions historiques L5 sont consignées par l’ADR 0016. L’ADR 0018 remplace l’obligation AD par une extension LDAP générique, en lecture seule, avec profils directs et groupes facultatifs. La qualification OpenLDAP réelle est acquise localement en clair explicite et sur Linux CI en LDAPS ; elle ne constitue pas une revendication Active Directory ni une qualification LDAP réelle sur Windows/macOS.

G2s — franchie : besoins LDAP et stage = nœud acceptés (S12/S13), R1 qualifié par L5a, R2-A/R3-A/R5-A/R6-A/R7-A appliqués par L5b et R4 matérialisé par D6-A en L6. Contraintes et décisions : [recadrage](spec-ldap-stages.md). Aucune entité Stage supplémentaire n’est introduite.

G2a — franchie pour L6 : Q11 est résolue par l'ADR 0017 et D1-B/D2-A à D7-A sont acceptées dans l'ADR 0020. L'administration habilite les approbateurs via les profils internes ; au démarrage ou à l'activité concernée, l'utilisateur désigne explicitement une identité parmi les personnes autorisées. Aucun recours obligatoire à l'attribut AD `manager`. Les contrats typés `HumanTask`/`Timer`, formulaire et handler versionnés, idempotence liée à l'acteur et au contenu, séparation versionnée et inbox bornée constituent désormais la cible d'implémentation. Auto-approbation configurable et interdite par défaut reste conforme à l'ADR 0011.

G2b — franchie pour L7a/L8 : D1-A retient Blazor Web App, SSR statique par défaut et Interactive Server par composant ; D2-A retient composants compilés et ressources manifestées/hachées sous URL immuable. Bibliothèque graphique du Studio et outillage spécifique à celui-ci attendent L11. Voir ADR 0021.

G3 — franchie pour L7b : le porteur confirme **G3-A, coexistence nominale**. Deux versions de module, de composant Razor et de dépendance privée coexistent dans des contextes distincts ; l'artefact exact est retenu et son retrait est bloqué tant qu'il reste référencé. Le drainage demeure une opération explicite, pas le modèle nominal.

G3-M — franchie pour L7b (anciennement « G3a », renommée pour éviter la confusion avec le choix G3-A) : M1-A retient une migration explicite source → cible fournie par l’artefact exact, exécutée sans effet externe et committée atomiquement sur une instance sans bail actif. Un échec ne modifie rien et la commande reste relançable. M2-A retient un inventaire durable et le retrait bloqué tant qu’une définition persistée référence l’artefact.

G2c — franchie pour L8 : L8-D1-A à L8-D8-A arrêtent la topologie du Host et de la bibliothèque UI, le catalogue versionné de processus/formulaires, la projection des données visibles, l’intégration session/cookie, le mode d’application des migrations SQL, la mutabilité autorisée dans l’administration, le stockage des réglages et la qualification navigateur. Voir l'[ADR 0023](adr/0023-host-formulaires-et-administration-l8.md) et la [préparation du lot L8](l8-readiness.md). Le dossier `prototype/` est la référence UI et ne fait plus l’objet d’un arbitrage visuel.

G4 — avant diffusion : matrice testée, licence/noms publics, objectifs mesurables Q06, migrations/restauration et connecteurs qualifiés. Pas de revendication de MVP complet sans Oracle et sécurité livrés.

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
| L9 | CLI, templates, pack et exemple embedded | L7b, L8 | Depuis copie propre : génération, build, validation, package, chargement |
| L10 | Qualification, distribution et runbooks | L3b-Q, L9, G4 | Matrice publiée sur preuves, restauration exécutée, artefacts vérifiés ; MVP livrable |
| L11 | Studio initial et export C# | MVP, choix bibliothèque/Q09b | Export compilable et modèle équivalent ; absent du Host production |

L’audit et l’instrumentation commencent avec L3/L4 ; ils ne sont pas reportés à L10. Le prototype Oracle doit commencer dès L2 pour détecter une divergence avant de stabiliser le contrat SQLite. Le prototype des modules précède la stabilisation des API de plugins.

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
| Charge cible inconnue | Mesures reproductibles puis seuils convenus | G4 |
| Multiplication des distributions | Matrice explicite avec niveaux de qualification | G4 |

## Terminé pour chaque lot

Code compilable, tests pertinents exécutés, commandes de reproduction, documentation mise à jour et limites connues. Tout sample distingue explicitement définition, exécution, interaction humaine et rendu UI ; il ne présente pas une référence textuelle ou un JSON libre comme une fonctionnalité opérationnelle. Les tests non exécutés sont signalés avec motif. Toute nouvelle ambiguïté structurante est remontée avant de poursuivre la partie concernée.
