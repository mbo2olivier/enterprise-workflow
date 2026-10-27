# Stratégie de validation

Statut : stratégie de validation. Au 7 octobre 2026, la suite complète de L8, le smoke Playwright Linux, la qualification Oracle Enterprise 19.19 et la matrice GitHub Actions sont confirmés avec succès par le porteur selon la [procédure de validation L8](l8-validation.md). La dernière suite locale complète intermédiaire comptait **100 tests : 99 réussis, 1 Playwright ignoré explicitement, 0 échec** avant l'exécution dédiée du smoke navigateur. Elle couvre architecture, modèle/DSL v1, SQLite, runtime, sécurité, tâches, modules, Kernel, contrats de présentation et composants web.

## Pyramide

Tests purs pour graphe, DSL, transitions et décisions de retry ; tests de contrat partagés pour chaque store et extension de sécurité ; intégration sur base réelle ; parcours Host et packaging. EF InMemory et mocks sont insuffisants pour revendiquer atomicité, verrouillage ou migrations.

| ID | Test | Résultat attendu | Environnement |
| --- | --- | --- | --- |
| T01 | Graphe invalide : doublon, cycle, sortie inconnue, référence absente | Diagnostic stable et localisé ; publication refusée | Pur |
| T02 | Deux constructions équivalentes | Même modèle normalisé/hash | Pur |
| T03 | Création idempotente, réponse perdue puis répétition | Même instance ; autre contenu sous même clé refusé | SQLite + Oracle |
| T04 | Deux connexions claim le même travail | Un seul propriétaire valide ; génération avancée à la reprise | SQLite + Oracle |
| T05 | Expiration/renouvellement et commit ancien | Ancien token refusé ; aucune écriture partielle | SQLite + Oracle |
| T06 | Arrêt brutal aux frontières transactionnelles | Reprise depuis dernier commit ; aucun réveil perdu | Processus + chaque base |
| T07 | Effet externe puis crash avant commit | Même clé au retry ; faux destinataire idempotent n’applique qu’une fois | Worker + destinataire simulé |
| T08 | Tâche, double soumission et deux décisions concurrentes | Une seule décision et une seule suite ; résultat connu ou conflit | Host + chaque base |
| T09 | Timer dépassé, arrêt, reprise et annulation concurrente | Aucun réveil anticipé ; pas de suite après annulation gagnante | Horloge contrôlée + bases |
| T10 | Permissions et auto-approbation | Refus par défaut de sa propre approbation ; exception workflow respectant droits/affectation ; saisie non bloquée | Host + fournisseurs |
| T11 | Local/LDAP/API et erreur fournisseur | Identité stable, capacités respectées, panne sans autorisation implicite | Contrats + LDAP non AD réel pour L5a ; AD si revendiqué |
| T12 | Modules A/B, dépendances privées, version/hash changés | Anciennes instances inchangées ou déploiement bloqué ; assets disponibles | Loader + UI |
| T13 | Création/migration/restauration | Instances, tâches, artefacts et audits cohérents après restauration | Chaque base |
| T14 | Templates, CLI, `ew dev` et archive RID | Copie propre → génération → runtime SQLite/UI multi-acteurs → reprise/reconstruction contrôlée → validation/package/vérification → chargement Kernel ; archives RID en L10 | Runners cibles ; critères [L9](l9-spec.md) |
| T15 | Export Studio | C# compilable, modèle équivalent, échappement, pas d’écrasement implicite | Après MVP |
| T16 | Absence du Studio et simulateur de sécurité en production | Routes non exposées ; mauvaise configuration refusée | Host production |
| T17 | Réutilisation inter-organisations | Même artefact, données/identités/configurations indépendantes | Deux installations |
| T18 | Frontières d’architecture | Références interdites absentes | Analyse des projets |
| T19 | Outbox : crash après envoi avant acquittement | Redelivery avec même clé ; état cohérent et retries bornés | Chaque base + destinataire |
| T20 | UI Razor/Blazor, formulaires, accessibilité et thèmes | Déclaratif et composant personnalisé ; validation serveur ; clavier, labels, focus, erreurs et contraste | UI navigateur |
| T21 | Charge et logs | Mesures reproductibles, payloads/secrets masqués | Environnement décrit |

T22 est acquis par L5a. T23 est acquis par L5b/L6, branchement sur tâche active compris. L6 couvre les primitives runtime de T24 (destinataire, claim concurrent, révocation relue avant première complétion, séparation et version de définition) et L8 en fournit le parcours UI ; la qualification navigateur et Oracle finale suit `docs/l8-validation.md`.

| ID | Test | Résultat attendu | Environnement |
| --- | --- | --- | --- |
| T22 | LDAP générique : bind, schéma, codecs, renommage, recherche, groupes absents/présents, statut, TLS et panne | Identités stables ; profils directs utilisables sans groupes ; capacités honnêtes ; certificat avec mauvais nom refusé ; clair uniquement par sélection explicite, aucun downgrade automatique | LDAP non AD réel sur le job Linux ; contrats sans instance sur Windows/macOS |
| T23 | Grants par workflow/version/nœud/action ; visibilité et candidats ; faux contexte API ; révocation | Refus hors contexte même avec permission globale ; profil admin sans droit métier ; droits relus ; contexte dérivé côté serveur | Services/API L5b + SQLite/Oracle |
| T24 | Clientèle → superviseur → maker → validateur ; affectation, concurrence, reprise et versions | Destinataire conservé ; un claim/une complétion gagnants ; droits retirés refusés ; ancien nœud/ancienne activation rejeté ; séparation maker/checker si R4 retenue | Runtime L6 + SQLite/Oracle puis UI L8 |

## Injection d’incidents

Placer des points d’arrêt contrôlés avant/après claim, avant l’appel métier, après effet externe, avant/après commit et après envoi outbox avant marquage livré. Terminer le processus worker réellement pour une partie des cas ; une exception attrapée n’équivaut pas toujours à un arrêt brutal.

Inspecter après reprise le nombre d’instances, activations, travaux et réveils, la révision et l’audit. Le test T07 démontre l’idempotence du destinataire simulé, pas l’exécution unique de tous les systèmes externes.

Tester également rollback lors d’une erreur au milieu du commit : aucun état partiellement mis à jour, aucune suite orpheline, audit cohérent. Contrôler la cohérence du temps des baux sur les deux providers et la precision des échéances.

## Sécurité

Local : mot de passe invalide, compte désactivé, verrouillage, sessions révoquées et absence de mot de passe par défaut. LDAP : compte inconnu/désactivé selon capacité déclarée, mot de passe vide, certificat invalide, timeout, groupes et résolution du sujet stable. API : réponse invalide, identité manquante, erreurs HTTP, timeout et schéma non supporté.

Tester l’auto-approbation sans configuration, avec interdiction explicite et avec autorisation explicite ; vérifier les appels directs à l’API, les droits ordinaires et la non-régression des tâches de saisie. Pour le versionnement proposé, une nouvelle politique ne modifie pas les instances existantes.

Tester même nom affiché dans deux fournisseurs, tentative de fusion par email, suppression/retrait des accès avant complétion, accès direct aux routes administratives et droits d’une tâche d’autrui. Ne pas utiliser de credentials corporate réels dans les fixtures ou rapports.

## CI — GitHub Actions confirmé jusqu'à L8

La première CI utilise GitHub Actions. Les commandes de compilation et de test restent utilisables hors Actions pour faciliter une future adaptation GitLab ; aucune pipeline GitLab n’est requise au premier lot.

Sur chaque changement : restore verrouillé, build, tests purs et architecture, tests SQLite, templates et packaging pertinents. Oracle doit avoir une exécution d’intégration disponible avec identifiants injectés de manière sûre ; si elle ne peut tourner sur une contribution externe, le résultat requis doit être fourni dans une pipeline de confiance avant release. Une étape ignorée ne vaut pas succès.

Avant release : Oracle 19c et autres versions revendiquées, LDAP non AD réel (AD uniquement si revendiqué), archives sur OS/CPU annoncés, migration et restauration, UI et exemple embedded. Les tests de contrats sont mutualisés, les assertions spécifiques aux providers restent visibles.

## Performance et rapport

Mesurer latence des commandes et timers, débit de travaux, contention, retries, mémoire et croissance du stockage. Rapporter OS/CPU, version de base/provider, nombre de workers, taille d’état et durée des essais. Seuils à convenir via Q06 ; sans eux, rapport descriptif seulement.

Chaque preuve de lot comprend commandes, commit, environnement, résultat, limites et tests non exécutés. La couverture de code ne remplace pas les scénarios d’incident.

## Qualification L9 — à exécuter

La [spécification L9](l9-spec.md) détaille les scénarios `ew dev`, CLI, packaging et embedded. Couvrir SQLite réel, navigateur LeaveRequest multi-acteurs, refus serveur, reprise, seed idempotent conservant les révocations, configuration invalide, surveillance sans boucle, hash modifié sous même release et coexistence A/B. T16 doit prouver l’absence du sélecteur et du bootstrap de développement dans le Kernel de production ; vérifier aussi leur absence des packages métier avec les bases, seeds et secrets locaux. Exécuter la CLI et les reconstructions sur les trois OS cibles, notamment la libération des DLL Windows. Aucun de ces tests n’est acquis par les preuves L8/L8b.

## Qualification L10 — packages publics NuGet.org

La publication publique est un livrable obligatoire de L10. Consigner IDs, versions et liens des packages publiés et vérifier leur consommation avec cache isolé et source NuGet.org explicite, sans références projet au dépôt : restauration des bibliothèques, installation des versions publiées de la CLI et des templates, génération/build, `ew dev`, validation/package, chargement Kernel et exemple embedded. Vérifier métadonnées, licence, dépendances et exclusion des secrets/données locales avant publication. Une réussite depuis les seuls `.nupkg` locaux ou une publication partielle ne satisfait pas T14 ni le critère de sortie L10. Ces preuves restent à acquérir.

## Qualification intégrée L8b/L9 — séquencement confirmé

Oracle 19 L8b est acquis sur preuve du porteur (13/13 tests, aucun ignoré). Parcours LeaveRequest complet et SMTP sont réservés à la sortie L9, sans bloquer son entrée. Utiliser les nouvelles commandes de packaging/vérification et le rendu `ew dev`, puis prouver séparément le chargement Kernel. Prévoir une stack Docker Compose avec serveur SMTP de capture réel, interface de consultation, disponibilité et ports locaux ; qualifier destinataires, liens, contenu minimal, pagination, panne/reprise et retries durables. Documenter la portée du serveur de capture : pas de preuve de délivrabilité Internet ni de TLS/authentification de production. Ces scénarios restent à exécuter avant clôture L9.

## Exécution locale L9 — 20 octobre 2026

Voir la [preuve L9](evidence/l9.md) : build Release sans avertissement, 70 tests ciblés (Core, Extensions, CLI, Web), quatre scénarios Chromium incluant refus/commentaire et annulation, capture SMTP réelle avec panne/reprise, templates et outil installés depuis un flux local, exemple embedded et ancienne instance complétée après redémarrage versionné. Le package LeaveRequest est également vérifié dans le Kernel, avec absence des routes de développement.

La CI exécute `scripts/qualify-l9.sh` sur les trois OS et `scripts/qualify-l9-browser.sh` sur Linux. Le script navigateur consomme un sample isolé à partir des packages locaux, arrête explicitement le runtime via stdin et nettoie la stack SMTP. Ces nouvelles exécutions CI restent à confirmer ; les compléments navigateur et imports historiques sont suivis dans la preuve. Aucun test ignoré ne vaut réussite.

## Qualification locale L9b — 25 octobre 2026

Les contrats DTO/migrations, projections sécurisées, recherche/candidats, audit/export, médias/quarantaine/restauration et TAT sont couverts par la [preuve locale L9b](evidence/l9b.md). Résultats finaux : 140 tests Release des neuf suites locales, 14 tests Oracle Enterprise 19.19 et cinq cas Chromium sans SMTP ; quatre cas métier sont également rejoués avec SMTP réel/panne-reprise et package chargé dans le Kernel. Aucun échec ni ignoré dans ces campagnes finales. Les tentatives intermédiaires et limites sont consignées dans la preuve ; la CI multiplateforme et LDAP réel L9b ne sont pas présentés comme acquis.

## L9b — ultime itération

Les tests locaux rapides couvrent le fluent obligatoire typé, les lectures/remplacements DTO, les commentaires d’approbation atomiques et idempotents, les formats intégrés et les validateurs personnalisés liés à l’artefact exact. La [recette finale du porteur](validation/l9b-final-recette.md) détaille les essais longs délégués, avec commandes, résultats attendus et tableau de confirmation : documents réels, UI/permissions, Chromium/SMTP/packages, Oracle 19.19, restauration et CI/LDAP. Ne pas assimiler un test Oracle compilé à un test Oracle exécuté.


### Cycle natif `ew dev` (L9b)

Le test `DevelopmentLifecycleTests.SourceGenerationsPreserveInstancesAndNamedEnvironmentsAreIsolated` est opt-in localement via `EW_DEV_LIFECYCLE_TESTS=1`. Il compile une copie du sample en C#, sans Python, et conserve son dossier temporaire comme preuve. Il vérifie changement/réutilisation de génération, instance historique traitable, environnements isolés, reset archivé, release ciblée, cache après déplacement et diagnostic d’artefact requis manquant. La matrice CI active ce test ; une compilation locale ne vaut pas qualification de cette matrice. La [recette N1–N6](validation/l9b-final-recette.md) décrit les parcours longs complémentaires.
