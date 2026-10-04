# Stratégie de validation

Statut : stratégie de validation. Les **62 tests locaux** des lots L1 à L4b passent le 3 octobre 2026 : architecture, modèle, DSL, SQLite, registre, worker, retries, état et outbox. Les **8 tests Oracle explicites** passent sur Oracle Enterprise `19.19.0.0.0`, dont migration outbox, fencing de livraison et parcours runtime `Start → Service → End`. Oracle Free reste la qualification L3b-I historique ; Docker et une chaîne injectée restent requis pour la suite Oracle.

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
| T14 | Templates, CLI et archive RID | Build propre, validation, chargement et exécution du module | Runners cibles |
| T15 | Export Studio | C# compilable, modèle équivalent, échappement, pas d’écrasement implicite | Après MVP |
| T16 | Absence du Studio et simulateur de sécurité en production | Routes non exposées ; mauvaise configuration refusée | Host production |
| T17 | Réutilisation inter-organisations | Même artefact, données/identités/configurations indépendantes | Deux installations |
| T18 | Frontières d’architecture | Références interdites absentes | Analyse des projets |
| T19 | Outbox : crash après envoi avant acquittement | Redelivery avec même clé ; état cohérent et retries bornés | Chaque base + destinataire |
| T20 | UI Razor/Blazor, formulaires, accessibilité et thèmes | Déclaratif et composant personnalisé ; validation serveur ; clavier, labels, focus, erreurs et contraste | UI navigateur |
| T21 | Charge et logs | Mesures reproductibles, payloads/secrets masqués | Environnement décrit |

T22 est acquis par L5a. La partie habilitation/catalogue/candidats/persistance de T23 est acquise par L5b ; son branchement sur une tâche active relève de L6. T24 reste non exécuté. Ces critères ne sont pas couverts par les preuves historiques L5.

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

## CI — GitHub Actions confirmé

La première CI utilise GitHub Actions. Les commandes de compilation et de test restent utilisables hors Actions pour faciliter une future adaptation GitLab ; aucune pipeline GitLab n’est requise au premier lot.

Sur chaque changement : restore verrouillé, build, tests purs et architecture, tests SQLite, templates et packaging pertinents. Oracle doit avoir une exécution d’intégration disponible avec identifiants injectés de manière sûre ; si elle ne peut tourner sur une contribution externe, le résultat requis doit être fourni dans une pipeline de confiance avant release. Une étape ignorée ne vaut pas succès.

Avant release : Oracle 19c et autres versions revendiquées, LDAP non AD réel (AD uniquement si revendiqué), archives sur OS/CPU annoncés, migration et restauration, UI et exemple embedded. Les tests de contrats sont mutualisés, les assertions spécifiques aux providers restent visibles.

## Performance et rapport

Mesurer latence des commandes et timers, débit de travaux, contention, retries, mémoire et croissance du stockage. Rapporter OS/CPU, version de base/provider, nombre de workers, taille d’état et durée des essais. Seuils à convenir via Q06 ; sans eux, rapport descriptif seulement.

Chaque preuve de lot comprend commandes, commit, environnement, résultat, limites et tests non exécutés. La couverture de code ne remplace pas les scénarios d’incident.
