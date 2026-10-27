# Preuves et clôture L9b — 27 octobre 2026

Statut : **L9b terminé le 27 octobre 2026**. Le porteur confirme dans la conversation avoir exécuté les tests lourds locaux avec succès et confirme que la pipeline GitHub est passée après les derniers correctifs. Le gate de qualification L9b est fermé sur cette confirmation explicite, distincte de la CI L9.

Les sections ci-dessous conservent la chronologie des campagnes, résultats intermédiaires et contrôles initialement délégués. Leurs mentions de recette ou CI encore attendue décrivent l’état au moment de chaque campagne ; elles sont levées par la confirmation finale ci-dessus. Aucun nouveau lancement de tests ou de pipeline n’a été effectué pour cette mise à jour documentaire ; aucun numéro de run, commit ou résultat chiffré supplémentaire n’est inventé.

## Livrables

- State DTO annoté, version explicite, initialiseur de démarrage, mapping des formulaires indépendant, validation permanente/contextuelle et validateurs métier purs ; contrats cibles des migrations explicites.
- Lecture générale d’instance, state filtré côté serveur, progression et historique récent borné ; liens depuis Mes demandes et Today, accès depuis le détail de tâche.
- Loupe en en-tête, modale, SERP, filtres de statut/dates/processus/version/ID/référence et initiateur sous capacité explicite ; curseurs chiffrés liés à l’acteur et aux filtres.
- Combobox de candidats éligibles à distance, temporisation, clavier, réponses obsolètes ignorées, sans rechargement du formulaire ; réutilisation démarrage, désignation suivante et récupération.
- Recherche d’audit paginée et export CSV borné, droits d’export distincts, neutralisation des formules et audit de l’extraction.
- Métadonnées médias relationnelles, stockage disque privé extensible, staging, activation dans les commits source, révisions, suppression logique, expiration explicite des dépôts abandonnés ; fichiers de formulaires et bibliothèque/téléchargement filtrés.
- Tables TAT normalisées, écriture dans les transactions SQLite/Oracle, rapports/CSV admin, reconstruction par instance depuis les événements, couverture historique partielle marquée.
- Template module en DTO ; LeaveRequest release 1.1.0/révision 2/schéma 2 avec upload et fichier de clôture généré ; chemin média stable sous `.ew/dev/media` et configuration Kernel `Media:Root`.

## Campagnes locales acquises

Sur macOS arm64, SDK .NET 10.0.401 : compilation complète Debug puis Release sans avertissement. La dernière campagne Release compte neuf suites réussies, **140 tests sans échec ni ignoré** : Core 62, SQLite 12, runtime 19, présentation 10, sécurité 17, sécurité SQLite 8, extensions 7, Web 2, CLI 3. Elle inclut la projection refusant une valeur complexe sous un champ scalaire, la quarantaine liée au hash, la restauration base + médias, le remplacement et le calcul TAT après plusieurs claims d’une même tâche.

Oracle Enterprise 19.19.0.0, conteneur local officiel existant et schéma jetable EWTEST : **14 tests Release réussis, aucun ignoré**, après recompilation des assertions finales de quarantaine et du rapport par analyste. Les scénarios vérifient rollback d’une référence média invalide au démarrage, publication/activation de métadonnées, curseur de recherche et filtres multistatuts, lecture du détail, reconstruction TAT et suppression logique. Les nouveaux indexes moteur/sécurité font partie des migrations exécutées.

Un parcours Chromium complet a réussi : demande avec fichier texte, confirmation, pool RH, recherche partielle `car`, sélection explicite du manager, approbation, instance terminale, bibliothèque avec upload et fichier généré, téléchargement, loupe/SERP, filtre initiateur forgé refusé, détail et fichier interdits à l’admin sans droit métier, exports audit/TAT autorisés avec leurs capacités dédiées. Le serveur de cette première preuve n’utilisait pas SMTP.

La première campagne élargie aux cinq scénarios du runtime a détecté des changements d’identité de test intermittents (token antiforgery d’un autre acteur). Les helpers attendent désormais le retour et le bandeau d’identité, le sélecteur de développement envoie `no-store` et les URL de changement de test sont uniques. Après ces corrections, les cinq scénarios Chromium sur la copie isolée ont réussi (approbation, trois branches métier et menu). Les échecs intermédiaires restent consignés ici.

## Distribution locale et scénarios supplémentaires

`./scripts/qualify-l9.sh /private/tmp/ew-l9b-pack` a réussi : packages locaux, installation de la CLI/templates en environnement isolé, module DTO généré, validation/inspection/pack/vérification, exemple embedded, échec de compilation conservant l’ancien runtime, refus du changement sous la même release, coexistence et complétion de l’ancienne instance par ses handlers exacts. La nouvelle qualification `/private/tmp/ew-l9b-final` a également réussi après stabilisation de la quarantaine et du service utilisable sans UI.

Une première tentative SMTP utilisait des DLL de tests Release anciennes, encore dirigées vers la révision 1 ; elle n’est pas comptée comme succès. L’ensemble Release a été reconstruit puis `./scripts/qualify-l9-browser.sh /private/tmp/ew-l9b-final` a réussi : **4 cas Chromium réussis, aucun ignoré**, SMTP réel de capture avec arrêt/reprise, retries durables et inbox utilisable, module LeaveRequest 1.1.0 packagé/vérifié, puis chargement distinct dans le Kernel. Le catalogue/CSS du package sont disponibles ; les routes de développement restent absentes du Kernel. La stack SMTP est arrêtée et supprimée par le script.

Les quatre cas SMTP recouvrent les cas métier de la classe déjà qualifiée sans SMTP ; ils ne s’ajoutent pas comme quatre tests unitaires distincts. Les logs locaux de cette exécution sont conservés dans `/private/tmp/ew-l9b-final.log`, `/private/tmp/ew-l9b-final-smtp.log` et sous `/private/tmp/ew-l9b-final/`.

## Reproduction

```bash
dotnet restore EnterpriseWorkflow.slnx --locked-mode
dotnet build EnterpriseWorkflow.slnx --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
# Pour chaque projet concerné :
dotnet test --project tests/EnterpriseWorkflow.Core.Tests/EnterpriseWorkflow.Core.Tests.csproj --no-build --no-restore
# Avec le schéma Oracle de test configuré, sans afficher ses credentials :
./tests/oracle/oracle-test.sh test
./scripts/qualify-l9.sh /tmp/ew-l9b-qualification
```

Pour Chromium : installer les navigateurs avec le `playwright.ps1` du projet de tests ; lancer une copie isolée de LeaveRequest via `ew dev`, définir `EW_DEV_TEST_URL`, puis exécuter le projet navigateur avec le filtre de classe `*DevelopmentBrowserTests`. Sur macOS, utiliser un chemin canonique (`/private/tmp/...`) pour la copie du projet afin d’éviter les références MSBuild relatives incohérentes entre `/tmp` et `/private/tmp`.

## Incidents corrigés

- Les factories runtime explicites n’injectaient pas le nouveau service de validation : le state valide était refusé quand un validateur personnalisé était déclaré. Le test du module réel et le parcours navigateur couvrent ce câblage.
- Un sélecteur pouvait être utilisé avant l’installation de ses listeners JS après navigation rapide : les contrôles distants sont activés seulement après initialisation, y compris pour le DOM renouvelé.
- Un lien `?tab=library` était résolu à l’accueil à cause du `<base href="/">` : les onglets pointent désormais vers l’URL absolue de l’instance.
- L’administrateur de développement a une identité UUID, pas le sujet littéral `ew-admin` : le test le sélectionne par son libellé, puis vérifie le compte effectivement affiché.

## Limites de qualification

- Matrice GitHub Actions Linux/macOS/Windows et OpenLDAP/LDAPS réel L9b : non acquis par ces exécutions locales. Les tests de sécurité purs ne prouvent pas une recherche LDAP réelle.
- SMTP : panne/reprise et parcours intégrés sont acquis localement ci-dessus ; TLS/authentification d’un serveur de production, délivrabilité Internet et arrêt du runtime au milieu d’un fan-out ne sont pas déduits de la capture locale.
- Restauration : un test SQLite couvre base + volume médias ; la restauration Oracle et les volumes partagés restent à qualifier avec l’infrastructure cible.
- Pas d’antivirus livré, de calendrier ouvré ou de mesure de travail humain actif. Le hook de scan synchrone et un état de quarantaine durable sont disponibles ; la complétion d’une inspection en attente passe par l’API de confiance liée au hash. Aucun scanner réel ni callback HTTP public n’est livré.
- Pas de purge automatique des fichiers actifs/historiques, ni de réconciliation automatique des objets physiques écrits avant toute métadonnée. L’expiration des dépôts persistés est une opération explicite.
- Les libellés sont portés par les annotations ; le raccordement à un catalogue de traductions, les formats supplémentaires de fichiers, le sélecteur distant d’initiateur admin, les filtres complémentaires optionnels et la pagination au-delà de l’historique récent ne sont pas revendiqués.
- Les exports sont préparés en mémoire sous limites avant remise, afin de refuser sans CSV tronqué. Les curseurs de recherche expirent au redémarrage du service ; une recherche doit alors être relancée.
- Charge Q06, rétention et capacité du stockage restent des paramètres d’exploitation à arrêter/mesurer avant diffusion. Aucun SLA ou niveau de charge n’est déclaré acquis.

Le [guide SDK/exploitation](../l9b-sdk.md) décrit les API effectives, les limites techniques, le SQL TAT et la sauvegarde. Ces limites empêchent une clôture globale du gate de qualification L9b et le passage automatique à la publication L10.

### Retours de recette du 25 octobre — navigation et présentation

- « Mes demandes » : carte entière navigable au clavier et à la souris.
- Démarrage : dernière version du processus sélectionnée avant les autorisations ; aucun repli sur une ancienne version si la nouvelle est interdite. Un POST direct sur une ancienne version est refusé. Les définitions historiques restent disponibles pour leurs instances.
- Détail : métadonnées et état avec en-têtes latéraux, progression et historique en tableaux, onglets segmentés et cartes de téléchargement dans la bibliothèque.
- Tâche : lien vers la demande intégré dans la dernière ligne du tableau.
- Administration : association/retrait d’un profil depuis les identités, modales promues dans la top layer native du navigateur.
- Recherche : croix de fermeture, corps et pied d’actions distincts, menu de statuts à cases à cocher, état vide espacé.
- Export audit toujours identifiable : désactivé et expliqué lorsque `security.audit.export` manque. Les profils existants ne sont pas automatiquement élargis.
- Audit de sécurité : libellés lisibles et identifiant technique dépliable ; contexte processus/version/étape pour les droits workflow. Une révocation de session reste un événement de session, sans processus ni étape associés. L’historique des exécutions reste présenté dans la vue détaillée des demandes.

Validation : build Release sans avertissement, 12 tests de présentation (dont 2 cas dernière version/autorisation), 6 parcours Chromium L9b et 2 tests Web. Les données du serveur utilisateur sur le port 5080 n’ont pas été réinitialisées ; qualification dans un environnement isolé sur 5099. Le rechargement surveillé du serveur 5080 a ensuite été déclenché et confirmé dans le navigateur, avec les deux artefacts historiques conservés.

### Deuxième passe des 15 annotations — contrôle visuel uniquement

- 1 : carte « Mes demandes » entièrement cliquable.
- 2 : dernière définition uniquement pour démarrer ; anciennes définitions maintenues pour les instances existantes.
- 3–4 : tableaux rayés avec en-têtes latéraux, sans ligne « Champ / Valeur ».
- 5–6 : progression et historique en tableaux avec en-têtes horizontaux.
- 7 : onglets segmentés et bibliothèque avec cartes de fichiers / état vide illustré.
- 8 : lien de consultation dans la dernière ligne du tableau de tâche, y compris sans résumé métier.
- 9 : sélecteur d’association et actions de retrait de profils dans les identités.
- 10 : modales d’accès dans la top layer native, au-dessus de l’en-tête.
- 11 : commande CSV visible, accompagnée de l’explication du droit manquant le cas échéant.
- 12 : colonnes processus et étape pour les événements de droits workflow ; identifiant technique dépliable et nature lisible pour les événements de sécurité sans processus (sessions, profils…).
- 13 : modale avec croix, corps formulaire et pied d’actions distinct.
- 14 : liste déroulante de statuts avec recherche, cases à cocher, résumé des libellés sélectionnés, choix sélectionnés regroupés et effacement. Liste affichée en popover natif pour ne pas être coupée par le conteneur.
- 15 : état vide de recherche avec marges, arrondis, icône et indication de relance.

Styles/scripts référencés via `Assets` pour utiliser les URL de ressources gérées par le framework. Vérification limitée à la compilation ciblée du CLI, syntaxe JavaScript et inspection du navigateur sur 5080 ; aucune suite de tests relancée pour cette seconde passe, conformément à la demande.

### Simplification SDK des médias

Politiques exclusivement au module (`MediaPolicies`), avec visibilité obligatoire et références `UseMediaPolicy` dans les champs. L’ancienne méthode `.Files` est conservée pour les artefacts binaires historiques mais interdite à la compilation des nouveaux modules. Les politiques sont figées dans la configuration canonique des définitions ; aucune surcharge n’est exposée aux processus ou aux handlers.

`media.For(context)` lie une collecte à chaque invocation de service, de tâche humaine ou d’initialisation de state. Rattachements et suppressions sont transmis automatiquement aux commits source ; les mêmes changements transactionnels sont implémentés pour SQLite et Oracle. Les sessions sont fermées à la sortie du handler. La validation des fichiers de formulaire retrouve et vérifie leur portée, propriétaire et politique avant collecte. L’exemple de congé passe à release 1.2.0 / contrat 3, en conservant le schéma d’état 2.

Qualification ciblée : compilation Release sans avertissement ; huit tests média SQLite réussis (services, humains, rejet, remplacement, suppression, portée des uploads, politiques partagées et restauration) et cinq tests fluent historiques réussis et un test de chargement réel du module de congé réussi. Aucun parcours navigateur ni conteneur Oracle relancé pour cette simplification ; les implémentations Oracle sont compilées.

### Ultime itération du 26 octobre — état typé et formats médias

Les API d’auteur précédentes sont remplacées sans couche de rétrocompatibilité : DTO d’état obligatoire, version explicite et initialiseur ; lectures détachées `ReadState<T>()` / `ReadSubmission<T>()`, remplacement explicite `NodeStateUpdate.Replace<T>()`. Le transport interne reste canonique. Les propriétés d’état doivent être sérialisables avec lecture et écriture publiques (y compris `init`).

Les actions d’approbation utilisent `RequireCommentAt(s => s.Comments)` ou `CommentAt`. Le handler intégré ajoute le commentaire avec l’identité et la date serveur ; décision, validation et ajout partagent le commit. Les tests vérifient notamment le rejet d’un état invalide et l’absence de doublon de replay. Aucun handler d’approbation personnalisé n’est ajouté.

Les politiques restent exclusivement au module ; `.Files` et les listes de médias retournées manuellement sont retirées. Treize formats intégrés sont disponibles, avec contrôles bornés de structure pour les familles Office/ODF, RTF et TIFF. Les validateurs supplémentaires sont déclarés au module et résolus depuis l’artefact exact (module, release et hash). L’exemple et le template sont adaptés ; LeaveRequest passe en release 1.3.0, contrat 4, schéma d’état 3. Ces indications remplacent les limitations et numéros des paragraphes historiques ci-dessus.

Validation locale effectuée : compilation Release de la solution sans avertissement ni erreur ; compilation séparée du projet de tests Oracle sans avertissement ni erreur ; **143 tests courts réussis**, aucun ignoré (Core 65, Runtime 44, Extensions 8, Presentation 12, SQLite 12, Web 2). Les tests couvrent les expressions de ciblage des commentaires, les lectures typées strictes, le commit et le replay, les familles de formats et packages malformés, et l’isolation des validateurs par artefact. Une assertion de test attendait le rejet trop tard, à la publication : elle a été corrigée pour vérifier le rejet immédiat lors de la déclaration. `git diff --check` passe.

Le préparateur d’environnement isolé a été vérifié sur un répertoire temporaire : références projet résolues et aucune donnée `.ew` copiée. Le serveur utilisateur 5080 n’a pas été réinitialisé ni redémarré pour cette itération.

**Non exécutés pour cette itération** : documents bureautiques réels via navigateur, parcours Chromium/SMTP complets, exécution Oracle, restauration d’exploitation et matrice CI/LDAP. La [recette détaillée M1 à M8](../validation/l9b-final-recette.md) fournit les commandes, prérequis, résultats attendus et le tableau de confirmation du porteur. Les preuves antérieures ne valent pas validation automatique de cette nouvelle itération ; le lot reste en qualification.

### Correctifs après retour M5/M6 du 26 octobre

Le journal `/private/tmp/ew-l9b-browser-20261026.log` fourni par le porteur indique 4 scénarios, dont 2 réussis et 2 en échec : les refus RH et manager étaient enregistrés, mais le tableau de détail affichait le JSON canonique avec accents échappés. Le tableau resérialise désormais les objets/collections pour afficher les caractères Unicode lisiblement, tout en conservant l’encodage HTML de Razor. Un nouveau test de rendu vérifie l’accent et l’absence d’injection HTML. Les 3 tests Web passent ; M5 complet n’a pas été relancé par l’agent et reste à confirmer après reconstruction dans un répertoire de qualification neuf (packages de développement à version identique).

Oracle : le test d’approbation utilisait un état initial incomplet (`comments` absent), refusé par la validation avant l’ouverture de la tâche. Le jeu de données est corrigé. La commande de recette ne répète plus `--configuration Release`, déjà injecté par le script. Exécution effective `./tests/oracle/oracle-test.sh test` sur le conteneur existant : **15 tests réussis, 0 échec, 0 ignoré**, durée totale environ 16 secondes. Compilation Release de la solution et `git diff --check` réussis. Cette exécution remplace le statut « compilé uniquement » d’Oracle ci-dessus ; restauration et matrice CI ne sont pas déduites de ce résultat.

Second retour M5 : le journal remplacé indique un `FileExistsError` sur le dossier de module de la première exécution ; aucun scénario navigateur n’a démarré. Le script crée désormais un sous-répertoire unique `browser-runs/run-XXXXXXXX` pour chaque passage, avec cache NuGet isolé et feed explicite, sans supprimer les précédents. Vérifications locales : syntaxe shell, deux préparations successives, configuration NuGet et préservation d’un fichier du passage précédent. M5 complet reste à confirmer par le porteur. Le porteur a également confirmé la réussite d’Oracle.

Troisième retour M5 : les accents restaient échappés car le CLI déjà installé embarquait la DLL Web historique. Chaque exécution reconstruit maintenant le package CLI et installe l’outil dans son répertoire isolé. La première relance de contrôle a identifié un second défaut intermittent : un conflit de révision lors de requêtes parallèles sur la même session était traité comme une perte d’authentification, entraînant un échec antiforgery. `SecuritySessionManager` relit et revalide la session avant de réessayer son heartbeat (trois reprises au maximum), sans accepter une session révoquée/expirée ni masquer un conflit persistant. Trois tests dédiés passent.

Relance effective suivante : `scripts/qualify-l9-browser.sh /private/tmp/ew-l9b-packages-20261026` terminée avec code 0 ; **4 scénarios Chromium/SMTP réussis, 0 échec, 0 ignoré** en environ 16 secondes, puis module chargé dans le Kernel de production, catalogue et CSS disponibles, endpoints de développement absents. Journal `/private/tmp/ew-l9b-browser-agent-fix-r2.log`, données isolées `browser-runs/run-5b2aYa3C`. Cette preuve remplace le statut navigateur en attente ci-dessus. Le script de qualification initial des packages n’a pas été entièrement réexécuté dans ce passage.

Le porteur confirme ensuite tous les blocs M1 à M7. M8 reste en attente de commit/push et CI. De nouveaux ajustements produit sont remontés pendant M1–M3 (identifiant public, vues, listes, onglets, commentaires et audit métier) ; leur validation n’est pas incluse dans cette confirmation.

### Ajustements acceptés après recette — implémentation et vérification ciblée

- Référence publique persistée sur dix caractères en base 36 (`TTTTTSSRRR` : minute depuis 2026, compteur modulo 1296, aléatoire cryptographique). Les GUID restent les clés internes. Index unique et reprise bornée des collisions sur SQLite et Oracle ; attribution de références stables aux instances historiques par migration.
- Composant de ligne commun à Today, Mes demandes, À traiter et Recherche, avec référence, processus, date, nom/ID de l’initiateur et statut ; destination variant entre détail et tâche. Recherche par référence courte ou GUID.
- Cinq onglets du détail : Détails (un seul encart métadonnées/statut/état), Bibliothèque, Progression, Historique, Commentaires. `[CommentState]` extrait les collections après projection des permissions ; références médias et commentaires dédiés sont exclus récursivement du tableau. Le nom d’acteur du commentaire peut être capturé côté serveur au commit.
- Vue de formulaire raccordée au démarrage et aux tâches via `Values` et `State` autorisé. Le sample possède maintenant un formulaire de confirmation distinct avec sa vue informative : un formulaire de démarrage n’est pas implicitement celui de toutes les tâches.
- Bibliothèque découplée du state : la présence d’une référence dans l’état visible n’est plus requise. Autorisation d’instance et politique média restent obligatoires ; fichiers cachés/restrictifs exclus. Le sample collecte les uploads sans liste de références métier redondante.
- Audit admin unifié sur les journaux sécurité et workflow transactionnels, pagination par date/source/identifiant, noms d’acteurs et contexte module/processus/action/étape/référence, filtres communs au CSV. Les opérations automatiques portent « Système ». Les résultats historiques non stockés restent « Non renseigné » plutôt que d’être inventés.

Compilation Release sans avertissement ni erreur. **151 tests courts réussis**, aucun ignoré : Core 67, Runtime 44, Extensions 8, Presentation 15, SQLite 14, Web 3. Couverture supplémentaire : migration d’instances existantes sans changement de GUID, contrainte unique réelle, persistance/recherche de référence, capture d’acteur dans l’audit, pagination intersources à timestamp identique et export filtré, absence de fuite des commentaires et politiques de bibliothèque indépendantes du state.

**15 tests Oracle réussis**, aucun ignoré, via `./tests/oracle/oracle-test.sh test` ; les nouvelles migrations sont appliquées dans le schéma jetable. Le test d’approbation vérifie également les nouvelles recherches d’audit et de référence. Journal `/private/tmp/l9b-adjust-oracle.log`.

**6 parcours Chromium réussis**, aucun ignoré, dans `/private/tmp/ew-l9b-adjust-review-2g3pzriw/review` sur 5098, sans réinitialiser l’environnement applicatif du porteur. Journal `/private/tmp/l9b-adjust-browser.log`. Contrôles explicites de la référence dix caractères/recherche, du composant de confirmation, des cinq onglets, de l’absence de commentaires/médias dans le tableau, des uploads et fichiers générés en bibliothèque, du refus de téléchargement non autorisé et de l’audit métier avec CSV filtré. Ce passage ne configure pas le SMTP ; il ne remplace pas la distribution complète/Mailpit, les formats réels, la restauration ni M8.

Le sample est en release 1.4.0 / processus 5 / schéma d’état 4. La recette [R1–R5](../validation/l9b-final-recette.md#r1r5--compléments-après-les-ajustements-acceptés) porte les confirmations manuelles restantes pour ces ajustements. Les anciens M1–M7 restent confirmés pour l’itération précédente ; M8 attend commit/push et CI. Aucune publication ni CI n’a été déclenchée par ces vérifications. Syntaxes JavaScript/shell et `git diff --check` passent.

Contrôle final de conservation des schémas historiques : la définition d’action indique si le nom d’auteur fait partie du commentaire. Les anciennes définitions n’ajoutent pas ce nouveau champ à leur état ; un test du handler vérifie explicitement son absence. Les noms d’acteurs sont aussi capturés côté serveur lors des affectations, prises en charge et libérations de tâches. Après ce correctif, Core 67, Runtime 44, Extensions 8, Oracle 15 et les 6 parcours Chromium passent à nouveau ; les suites non touchées restent vertes selon les résultats ci-dessus. Compilation complète Release sans avertissement/erreur et `git diff --check` confirmés.


### Itération CLI natif et simplifications DX — 26 octobre

Implémentés : `ew dev` depuis les sources avec environnements nommés, `ew.dev.json` v2 et priorité CLI/environnement/commun/défauts, reset archivé limité à l’environnement possédé, mode `--artifact` sans compilation/surveillance, sélection des seules générations/artefacts réellement requis et cache vérifié des packages externes. Les espaces `.ew/dev` sont conservés sans import automatique. Les générations ont un marqueur explicite et une identité réservée (`…-ewdev.gN`, contrat `1000000000 + N`) ; le Kernel/verify de production les refusent. Le catalogue de démarrage suit l’artefact courant, sans détourner une instance historique vers lui.

Le résultat humain `Success()` n’expose plus d’outcome : l’action autorisée fournit celui de la transition immuable. Fixtures, exemples, template et handler intégré sont adaptés. `IWorkflowSerializer` fournit la façade canonique DI ; les conventions sont fixes et les budgets sont recontrôlés à l’initialisation, aux commits et aux migrations. Les budgets de configuration restent sous les plafonds existants du moteur (1 Mio / profondeur 64). Un test vérifie qu’un remplacement statique trop volumineux ne contourne pas le budget au commit et ne remplace pas partiellement l’état.

Compilation Release de la solution sans avertissement ni erreur. **160 tests locaux réussis**, aucun ignoré : Core 68, Runtime 45, Extensions 9, CLI 6, Presentation 15, SQLite 14, Web 3. Les 6 tests CLI incluent le cycle natif opt-in explicitement activé : deux générations sans changement de release, reprise et traitement d’une ancienne instance, isolation, reset d’un seul environnement avec archive, lancement d’une release packagée dans un environnement contenant une génération antérieure, reprise après déplacement des packages externes, artefact inutilisé illisible ignoré et refus précis d’un artefact requis absent. Durée CLI environ 55 secondes. Journaux `/private/tmp/l9b-next-*.log`.

**15 tests Oracle réussis**, aucun ignoré, schéma jetable existant ; **6 parcours Chromium réussis**, aucun ignoré, dans un environnement source isolé sur 5098. Le passage navigateur ne configure pas SMTP et ne vaut pas qualification Mailpit/distribution complète. Journal navigateur `/private/tmp/l9b-next-browser.log`, environnement `/private/tmp/ew-l9b-adjust-review-2v3f8ei1/review` ; Oracle `/private/tmp/l9b-next-oracle.log`.

Le sample déclare maintenant release 1.5.0 / contrat 6 / schéma d’état 4. Les liens de test suivent le catalogue pour accepter les révisions locales, sans les coder en dur. Le script de qualification est adapté aux nouveaux chemins `.ew/environments/default` et conserve le packaging de production distinct du dev. La matrice CI active le test natif, mais aucun commit/push ni lancement de pipeline n’a été effectué ici. La recette N1–N6 porte les confirmations restantes (surveillance longue, distribution/SMTP, restauration complète et matrice). Les validations précédentes ne sont pas extrapolées à ces contrôles. `git diff --check` et syntaxe shell passent.

## Corrections des six annotations — 27 octobre 2026

- Étape commune dans les listes via les positions d’activation groupées (SQLite et Oracle).
- Résultat métier terminal explicite (`Succeeded`, `Declined`, `Cancelled`), indépendant de `Completed`. Sample 1.6.0 / contrat 7 ; historique sans métadonnée affiché avec badge neutre.
- Référence `No.` avec copie par icône et retour accessible ; formulaires recherche/TAT avec pied d’actions ; commentaires vides centrés.
- Compilation solution Release : aucune erreur ni avertissement. Présentation : 19 tests ; SQLite : 14 ; Oracle : 15 ; Chromium : 6 parcours, tous réussis.
- Journaux locaux : `/private/tmp/l9b-annotations-{build,presentation,sqlite,oracle,browser-2}.log`. Navigateur exécuté sur une copie temporaire isolée au port 5098 ; base utilisateur conservée.

Relancer le serveur `ew dev` existant pour charger le framework recompilé ; une actualisation seule ne remplace pas les assemblies du serveur en cours. Le résultat métier des anciennes définitions sans métadonnée n’est pas réinterprété rétroactivement.

## Validation de formulaires et correctifs CI — 27 octobre 2026

- Audit : grille de champs et pied d’actions séparés.
- Erreurs structurées `WorkflowValidationError` dans les handlers, initialisateurs et validateurs d’état ; conservation des erreurs lors du passage runtime / portail / redirection ; résumé et messages de champs accessibles, texte HTML encodé.
- Feedback serveur temporaire lié à l’identité et à la page, jeton opaque, usage unique, dix minutes, capacité bornée. Saisies, identités sélectionnées, clé d’idempotence et références média conservées ; antiforgery exclu. Les messages des pannes techniques ne sont pas exposés ; une référence est corrélée aux logs.
- Le validateur de dates du sample fournit désormais son message explicite (release 1.7.0 / contrat 8). Les validateurs d’anciennes générations locales restent chargeables.
- CI : suppression des chemins/version figés dans `qualify-dev.py`, migration des assertions au cycle g1/g2 et aux environnements nommés ; attente bornée de la tâche après restauration dans le test CLI natif.
- Vérifications exécutées : compilation Release sans erreur/avertissement ; Core 68, Runtime 47, Présentation 20, Extensions 9, Web 5 = 149 tests rapides réussis. Syntaxe Python et `git diff --check` valides.
- Journaux : `/private/tmp/l9b-validation-{build,core,runtime,presentation,extensions,web}.log`.
- Non exécutés dans cette itération : cycle natif CLI complet, qualification des packages/projets générés, Chromium et Oracle. Recette : `docs/validation/l9b-validation-ci-recette.md`. Les correctifs CI restent à confirmer par leur exécution et la matrice GitHub sur les trois OS.

## Fond quadrillé — 27 octobre 2026

La page autonome de choix du profil charge désormais le script partagé, déjà présent sur la connexion. La grille de base reste visible au repos (contraste augmenté), et le survol ajoute une accentuation locale plus légère sur un rayon réduit. Les coordonnées sont calculées par rapport au calque fixe, pour conserver l’alignement avec le pointeur après défilement et en présence du bandeau de développement. Vérifications : syntaxe JavaScript (`node --check`) et compilation CLI Release.

## Confirmation finale du porteur — 27 octobre 2026

Le porteur confirme : « j’ai exécuté le test lourds locaux et la pipeline est également passé sur github ». Cette validation couvre la recette longue déléguée et le passage GitHub après les correctifs CI. Elle complète les preuves locales consignées, notamment les 149 tests rapides de la dernière itération et la compilation du correctif de fond quadrillé.

L9b-0 à L9b-5 sont clôturés. Les recettes restent conservées comme procédures reproductibles : [recette finale](../validation/l9b-final-recette.md) et [validation/CI](../validation/l9b-validation-ci-recette.md). La publication publique relève du prochain lot L10 ; aucune publication NuGet.org n’est revendiquée ici.
