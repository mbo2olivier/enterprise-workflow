# Traçabilité

Les liens associent besoin, décision, lot et preuve attendue. Sauf mention explicite dans une preuve de lot, les tests ci-dessous sont **à exécuter**. T01, T02 et T18 sont acquis au niveau L2. T03 à T05 et la création/migration initiale de T13 sont acquis en L3. L4a/L4b acquièrent binding, worker, retries, fencing et outbox de T06/T07/T19. L5/L5a acquièrent le socle sécurité et T22. L5b puis L6 acquièrent T23 : portée exacte, candidats, révocation, tâche active, affectation, claim, inbox et complétion. L6 acquiert T08, le cœur durable de T09/T10 et le socle runtime de T24 ; le parcours bancaire/UI complet de T24 reste à L8. Voir les preuves sous [`docs/evidence`](evidence/).

| Exigence | ADR / conception | Lots | Tests |
| --- | --- | --- | --- |
| EF-01 Bibliothèques et Kernel | 0001, 0006, 0022 | L1, L7b, L9 | T14, T18 |
| EF-02 C# et nœuds | 0007 ; extensions | L2, L4 | T01, T02, T12 |
| EF-03 Durabilité | 0008 | L3a/b, L4 | T03–T07 |
| EF-04 SQLite/Oracle | 0002 ; compatibilité | L3a/b, L10 | T03–T09, T13 |
| EF-05 Tâches/formulaires | 0004 ; runtime | L6, L8 | T08, T10, T20 |
| EF-06 Timers/retries/annulation | 0008 | L4, L6 | T05–T09 |
| EF-07 Modules | 0009, 0021, 0022 | L7a/b | T12, T14 |
| EF-08 Administration et sécurité extensible LDAP | 0003, 0018 ; sécurité | L5, L5a, L8 | T10, T11, T22 |
| EF-09 Permissions et auto-approbation | 0003, 0011, 0019 ; sécurité | L5, L5b, L6, L8 | T08, T10, T11, T23, T24 |
| EF-16 Stages = nœuds et actions habilitées | 0019 ; recadrage | L5b, L6, L8 | T23, T24 |
| EF-10 Installations indépendantes | 0005 | L7b, L10 | T17 |
| EF-11 UI/thèmes | 0004, 0012 ; extensions | L8 | T10, T20 |
| EF-12 CLI/templates et `ew dev` | 0010, 0025 ; [spécification L9](l9-spec.md) | L9 | T14, T16, T12, T18, T20 |
| EF-13 Versions | 0009, 0022 | L7a/b | T12, T13 |
| EF-14 Audit/diagnostic | 0008 ; exploitation | L3 à L10 | T06, T10, T13, T21 |
| EF-15 Studio différé | 0004, 0007 | L15 | T15, T16 |
| ENF-01 Plateformes | 0001, 0010 | L1, L10 | T14 |
| ENF-02 Infrastructure minimale | 0006, 0008 | L4, L7b | T14, T17 |
| ENF-03 Concurrence | 0008 | L3a/b, L4, L6 | T04–T09, T19 |
| ENF-04 Extensibilité | 0006 | L1 à L9 | T11, T12, T18 |
| ENF-05 Données/droits | 0003 | L5, L8 | T10, T11, T16, T21 |
| ENF-06 Utilisabilité | Extensions/UI | L8 | T20 |
| ENF-07 Exploitabilité | 0009, 0010 ; exploitation | L10 | T13, T14 |
| ENF-08 Dimensionnement | Q06 clos sur décision du porteur le 27 octobre 2026 | L10 | Aucun seuil ni test de charge/performance exigé ; aucune garantie chiffrée |

## Décisions utilisateur

S3 confirme .NET/plateformes, les deux providers, l’administration extensible, le Studio différé et une organisation par installation. S4 précise Oracle 19c minimum, local/distant selon extension, AD par identifiant/mot de passe et matrice distincte par provider. S5 confirme l’auto-approbation configurable et interdite par défaut (0011), et Razor/Blazor avec formulaires déclaratifs et composants personnalisés (0012). S6 accepte les ADR 0006 à 0008, confirme le retour au MVP séquentiel sans parallélisme et retient GitHub Actions. S7 valide D1 à D6 pour L2 (ADR 0013) et autorise Docker Compose. S8 valide 1A, 2A et 3A pour L4a (ADR 0014). S9 valide 1A à 5A puis 6A pour L4b : configuration intégrale, outbox bornée/dead letter, jalon attente/callback et jalon migration d’état (ADR 0015). Voir [cadrage](cadrage.md) pour les limites d’autorité de la source initiale.

S12 remplace la cible obligatoire AD par LDAP et introduit les stages ; S13 précise stage = nœud ; S14 confirme la réussite de la qualification LDAP Linux GitHub Actions. Voir ADR 0018/0019. R1 est clos ; les arbitrages techniques L5b/L6 restants sont proposés dans la spécification de recadrage.

S16 confirme D1-B et D2-A à D7-A. L6 corrige le schéma canonique v1 avant le premier tag officiel et livre les attentes décrites par l'ADR 0020 ; toute rupture postérieure au premier tag devra être versionnée explicitement.

## Préconditions encore ouvertes

Q06 est clos sans seuils ni tests de performance requis (décision du porteur le 27 octobre 2026). Q08 est arrêté pour licence, propriétaire NuGet `mukadi.sheba`, IDs et première préversion ; la disponibilité et la publication restent à vérifier. Q07 est résolue par la preuve de coexistence L7a et la confirmation de G3-A pour L7b. Q09b est résolue pour le mode de rendu et les ressources par D1-A/D2-A ; seule la bibliothèque du Studio reste ouverte. Q11 est résolue par l’ADR 0017. Q10 est résolue pour L1 et sera revue à la qualification de distribution. Un lot dépendant ne doit pas choisir silencieusement à la place du porteur.

## L8b — implémenté, qualification intégrée parcours métier/SMTP restante en sortie L9

| Besoin confirmé le 20 octobre 2026 | Décision | Lot | Preuve attendue |
| --- | --- | --- | --- |
| SDK lisible, release/révisions héritées, schéma explicite et IDs locaux | ADR 0024 A1–A4 ; O1 | L8b-0/1/5 | Déclaration LeaveRequest et aperçu déterministe ; anciennes références exactes préservées |
| Permissions métier déclarées par module, exigences stage/action | ADR 0024 A5 ; P2/O2 | L8b-2/5 | Attribution administrative, contexte exact, révocation et appels directs ; migrations SQLite/Oracle |
| Désignation sur l’activité précédente et confirmation du demandeur | ADR 0024 A6 ; O4 | L8b-3/5 | Branches conditionnelles, candidats, idempotence incluant destinataire, reprise et refus des identités arbitraires |
| Formulaires standard et notification SMTP des pools | ADR 0024 A6–A7 ; P3/O3/O5 | L8b-4/5 | Rendu standard/personnalisé, projections autorisées, distribution bornée, panne SMTP et claim concurrent |

La [preuve locale L8b](evidence/l8b.md) consigne 116 tests légers réussis. La [validation restante](l8b-validation.md) conserve explicitement les qualifications externes non acquises.

## L9 — implémentation et preuves locales

EF-12 inclut désormais CLI installable, templates, runtime `ew dev`, packaging et vérification d’artefacts. La [preuve L9](evidence/l9.md) consigne 70 tests ciblés, quatre parcours Chromium avec SMTP réel et panne/reprise, génération/embedded hors dépôt, conservation des anciennes instances et vérification du package dans le Kernel de production. Le porteur confirme la validation de la pipeline GitHub L9 le 23 octobre 2026 ; les compléments fonctionnels listés dans la preuve restent tracés séparément.

## L9b — besoins du 23 octobre, décisions acceptées le 25 octobre 2026

| Exigence | Conception / arbitrage | Sous-lots clôturés | Couverture qualifiée ([preuve L9b](evidence/l9b.md)) |
| --- | --- | --- | --- |
| EF-17 Détail universel | [Spécification §1](l9b-spec.md), A1 | L9b-0/1/5 | Liens de tous écrans, instance terminale, projections et accès direct refusé sans droits |
| EF-18 Recherche autorisée | Spécification §2, A1 | L9b-0/1/5 | Modale/SERP, filtres/dates, curseurs, révocation, absence de fuite dans résultats et compteurs |
| EF-19 Audit filtré/exporté | Spécification §3, A2/A3 | L9b-0/2/5 | Pagination au-delà de 250, export filtré complet ou limite explicite, droit distinct, audit d’extraction, CSV protégé |
| EF-20 Médias SDK/API/UI | Spécification §4, A4/A5 | L9b-0/3/5 | Upload et génération, validation, révisions, suppression, autorisation, crash/retry, restauration des fichiers et métadonnées |
| EF-21 TAT | Spécification §5, A6/A7 | L9b-0/4/5 | Transactions et replay sans doublon, intervalles d’affectation, rapports et SQL SQLite/Oracle, historique incomplet marqué |

L9b étend aussi EF-04/09/11/12/14 et ENF-03/05/07/08. L9b est terminé le 27 octobre 2026 : preuves locales consignées, tests lourds locaux et pipeline GitHub confirmés réussis par le porteur. Le prérequis L9b de L10 est satisfait ; la publication publique reste dans L10.

### Décision état métier — 25 octobre 2026

EF-05/09/11/17 et ENF-04/05 sont complétées par l’[ADR 0026](adr/0026-etat-metier-type-et-validation.md), acceptée : DTO d’état annoté, version explicite/migrations, formulaires indépendants et mapping initial/humain/service, validation permanente et groupes contextuels, filtrage serveur des champs. Implémentation : L9b-0/1/5. Couverture qualifiée à la clôture L9b : noms formulaire/state différents, état initial produit explicitement, validation avant commit sans effets transactionnels en cas de rejet, contexte serveur, masquage imbriqué et permissions any/all, anciennes versions et migration cible. La décision documentaire est complétée par la [preuve d’implémentation et de clôture](evidence/l9b.md).

A1–A7 sont acceptés le 25 octobre 2026 dans l’[ADR 0027](adr/0027-consultation-medias-audit-tat-l9b.md) : lecture sans privilège métier implicite, audit de sécurité, CSV en flux borné, stockage disque privé extensible et révisions immuables, hook antivirus selon installation, TAT calendaire avec historique partiel marqué, projections transactionnelles reconstructibles. Les preuves du tableau restent à acquérir.

### Dette UX destinataires — 25 octobre 2026

EF-05/09/11, L9b-1/5 : [sélecteur distant de candidats éligibles](l9b-spec.md), §2.1. Preuves attendues : recherche partielle par nom, identité stable explicite, filtrage et revalidation serveur, séparation par action/cible, réponses obsolètes ignorées, formulaire préservé, clavier, panne et révocation ; parcours démarrage/désignation suivante/récupération. Réutiliser les services de recherche existants sans rendre l’annuaire librement consultable. Non implémenté et non qualifié à ce stade.

## Implémentation et qualification locale L9b

La [preuve du 25 octobre 2026](evidence/l9b.md) remplace le statut « non implémenté » des sections de cadrage précédentes pour EF-17 à EF-21, le state DTO et la dette UX des destinataires. Elle associe aux tests les contrats/migrations SQLite/Oracle, le parcours Chromium et les limites encore ouvertes. Le [guide](l9b-sdk.md) documente DTO, validation/migrations, médias, capacités, TAT et sauvegarde. Les critères non couverts ne sont pas déclarés acquis.

## L12 — périmètre planifié, non implémenté

Le [plan L12](l12-cli-distributions-branding-plan.md), approuvé le 18 novembre 2026, complète les exigences d’outillage, d’extension et de présentation ; les rattachements PRD et ADR détaillés seront formalisés en L12-0. Aucun résultat de qualification n’est acquis.

| Besoin accepté | Sous-lots | Preuve attendue |
| --- | --- | --- |
| Builder CLI et extensions compilées sans collisions | L12-0/1/6 | ew et CLI personnalisée sur le même socle, conventions communes et collisions refusées |
| Services métier communs dev/Kernel, configuration distincte | L12-2/6 | Même implémentation versionnée ; identités dev exclusivement ; aucun secret exposé |
| Contrats partagés et compatibilité | L12-3/6 | Même artefact dans les deux hôtes ; absence/incompatibilité diagnostiquée |
| Templates et distribution organisationnelle privée | L12-4/6 | Installation feed de test, commande nommée et module préconfiguré |
| Identité visuelle et localisation L11 | L12-5/6 | Marque/palette cohérentes, réglages contrôlés et rendu bilingue |

## L11 — hôtes, capacités de sécurité et langues

| Besoin | Décision | Implémentation / preuve attendue |
| --- | --- | --- |
| Composition d’un kernel personnalisé sans recopier le programme standard | ADR 0029 ; L11-1 | Hosting SDK, adaptation du kernel et exemple `CustomKernel` |
| Capacités d’identité et maker/checker optionnels | ADR 0029 ; L11-2/3 | Contrats, contrôles serveur et scénarios de conformité des providers |
| Second facteur optionnel avec écran générique | Arbitrages porteur du 18 novembre ; L11-4 | Handler compilé, absence nominale, challenge sans session et assurance persistée |
| Secrets de développement hors artefacts et compilations | L11-5 | Tests `.env`, priorité des sources, snapshot et recette native |
| Langues d’instance et préférence locale ; modules bilingues | L11-6 | Catalogues immuables, migrations SQLite/Oracle, projections et navigateur |

Statut et limites de qualification : [preuve L11](evidence/l11.md). Le plan et les spécifications ne constituent pas une preuve d’exécution.

L12 : décisions formalisées dans l’[ADR 0030](adr/0030-distributions-cli-runtime-et-marque.md), [spécification](l12-spec.md), [guide SDK CLI](l12-cli-sdk.md), [preuve](evidence/l12.md) et [recette déléguée](validation/l12-recette-porteur.md). La compatibilité vise une plage du runtime complet ; un service absent échoue à sa résolution normale.

## L13 implémenté localement / L14 planifié — décision du 24 novembre 2026

| Besoin | Spécification et décision | Lots / validation attendue |
| --- | --- | --- |
| Découverte d’une racine et fallback de la liste historique | [L13](l13-spec.md), ADR 0031 | L13 ; T12/T14/T18 ; priorité sans fusion, scan borné, intégrité et compatibilité |
| Retrait du code devenu inutile malgré des définitions historiques conservées | [L14](l14-spec.md), ADR 0031 | L14 ; T07/T12/T13 ; traitements exécutables protégés, retrait possible après préparation |
| Lecture historique autorisée sans ancien module | L14, politiques de visibilité existantes | L14 ; T10/T20/T23 ; restitution générique, médias et champs filtrés, migration des historiques |
| Studio et export C# | Périmètre antérieur, renumérotation du 24 novembre | L15 ; T15/T16 |

L13 conserve le contrôle de rétention actuel ; seule la livraison de L14 change cette politique. Aucune preuve d’exécution n’est inférée de ces spécifications.

L13 : API Extensions/Hosting, priorité de configuration et découverte sont implémentées. Résultats ciblés et limites dans la [preuve L13](evidence/l13.md) ; campagnes lourdes dans la [recette](validation/l13-recette-porteur.md). L14 reste non démarré.
