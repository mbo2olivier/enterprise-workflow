# L9b — recette de l’ultime itération à confirmer par le porteur

Date : 26 octobre 2026. Ce document délègue les vérifications longues ; leur présence ici ne signifie pas qu’elles ont réussi. Les captures sont facultatives : une confirmation par bloc avec résultat et éventuel message d’erreur suffit.

## Confirmation du porteur et nouvelle passe de corrections

Le porteur confirme la réussite de M1 à M7 ; M8 reste en attente du commit/push et de la CI. Cette confirmation concerne l’itération testée avant les nouvelles demandes : référence publique lisible, vues de formulaires, listes uniformisées, réorganisation des onglets, commentaires dédiés, retrait des références médias redondantes et audit métier unifié. Ces évolutions devront recevoir une vérification ciblée supplémentaire ; la validation précédente ne vaut pas preuve anticipée de leur implémentation.

## 1. Préparer un environnement natif .NET

Pré-requis : SDK .NET 10 du `global.json`, navigateur. Aucun Python ni copie de sources n’est requis pour utiliser `ew dev`. Docker/Oracle sont nécessaires uniquement pour les blocs correspondants. Depuis la racine du dépôt :

```bash
dotnet build EnterpriseWorkflow.slnx --configuration Release
export L9B_REVIEW_MODULE="$PWD/samples/LeaveRequest.Module"
export L9B_REVIEW_ENV=recette-l9b-native-20261026
export L9B_REVIEW_ROOT="$L9B_REVIEW_MODULE/.ew/environments/$L9B_REVIEW_ENV"
dotnet src/EnterpriseWorkflow.Cli/bin/Release/net10.0/EnterpriseWorkflow.Cli.dll dev "$L9B_REVIEW_MODULE" --environment "$L9B_REVIEW_ENV" --configuration Release --url http://localhost:5099 --no-watch
```

Choisir un autre nom d’environnement pour une recette vierge. Pour réutiliser des données, conserver le nom ; le CLI conserve les artefacts exacts nécessaires. Pour repartir explicitement de zéro, ajouter `--reset` : seul cet environnement est archivé. Les anciennes données `.ew/dev` ne sont ni effacées ni importées automatiquement. Les réglages avancés pointant vers une base externe ne sont pas réinitialisés.

Ouvrir <http://localhost:5099/__ew/dev>. Le sample déclare release **1.6.0**, contrat **6**, schéma d’état **4**. En mode sources, le bandeau montre une release locale `…-ewdev.gN` et le détail une génération locale ; l’identité technique est attribuée par le CLI. Le catalogue de démarrage doit montrer une seule carte « Demande de congé ». Les tests doivent suivre les liens du catalogue, sans coder en dur sa révision technique.

Identités : Alice — Demandeuse, Bob — RH, Carol — Manager, Administration locale. Le sélecteur permet de changer d’identité. Les permissions ne sont pas élargies automatiquement lors d’une modification du seed.

Les scripts Python restants servent uniquement aux qualifications/outillages historiques de recette. Ils ne constituent plus le parcours de lancement du produit.

## M1 — Formats réels : positif, téléchargement et type falsifié

1. Préparer de petits fichiers non confidentiels réellement enregistrés dans ces formats : PDF, DOCX, XLSX, PPTX, ODT, ODS, ODP, RTF, TXT UTF-8, CSV UTF-8, JPEG, PNG, TIFF classique. Éviter des fichiers de production pour cette recette.
2. Avec Alice, créer une demande ; sélectionner une date de début et une date de fin cohérentes, le type de congé et un motif d’au moins trois caractères.
3. Le champ « Justificatifs » accepte au maximum trois fichiers, de 10 Mio chacun au maximum. Répartir les treize formats sur plusieurs demandes. Attendre la fin de chaque upload puis envoyer et confirmer la demande.
4. Depuis « Mes demandes », ouvrir le détail, puis « Bibliothèque ». Retrouver les fichiers dont la référence a été mappée au state, les télécharger et vérifier qu’ils s’ouvrent dans leur application habituelle.
5. Répéter avec un texte renommé en `.docx`, un DOCX renommé en `.xlsx`, une archive ZIP quelconque renommée en `.odt`, un fichier tronqué, un fichier `.doc/.xls/.ppt`, un document à macros `.docm/.xlsm/.pptm` et un document chiffré. Attendu : rejet explicite, pas de pièce jointe active et pas de passage silencieux à l’étape suivante pour un fichier rejeté.
6. Vérifier un quatrième fichier et un fichier de plus de 10 Mio : refus explicite.

Limites voulues du validateur intégré : au plus 2 048 entrées et 200 Mio décompressés par package ; 100 Mio par entrée ; XML limité à 32 Mio et profondeur 64 ; ratio de compression supérieur à 100 refusé pour une entrée de plus de 1 Mio. Les macros, objets embarqués Office et ActiveX sont refusés ; les packages chiffrés ne sont pas pris en charge. TIFF classique : au plus 64 répertoires d’images, BigTIFF non pris en charge. Les contrôles sont bornés de signature/structure, sans rendu ni certification complète de conformité bureautique ; ils ne remplacent pas un antivirus. Si un document bureautique courant valide est rejeté, indiquer son logiciel de création et le message d’erreur : ne pas considérer ce rejet comme un succès de recette.

Confirmation : `M1 OK` ou liste des formats/scénarios en échec. Captures facultatives du rejet et de la bibliothèque. Ne pas joindre de document confidentiel.

## M2 — Commentaire RH obligatoire et conservation

1. Créer et confirmer une demande avec Alice ; relever son ID.
2. Passer à Bob, ouvrir « À traiter », ouvrir la validation RH et prendre la tâche si nécessaire.
3. Cliquer « Refuser » avec un commentaire vide ou composé uniquement d’espaces. Attendu : refus de la complétion, tâche toujours traitable, aucun nouveau commentaire dans le state.
4. Saisir `Refus RH — recette L9b`, puis refuser.
5. Revenir à Alice et ouvrir le détail. L’onglet « Commentaires » contient une entrée avec ce texte, l’étape `validation-rh`, l’action `decline`, l’identité `local / ew-dev-bob` et une date UTC serveur. Le state n’est pas clôturé par le service de succès ; aucun fichier de clôture n’est généré sur cette branche.
6. Actualiser la confirmation ou répéter la même requête via les outils réseau du navigateur, avec le même corps et la même clé d’idempotence. Attendu : un seul commentaire, une seule décision. Un corps modifié sous la même clé doit être refusé.

Confirmation : `M2 OK`, ID de demande facultatif. Capture facultative de l’onglet « Commentaires ».

## M3 — Commentaires facultatifs, manager et mutation typée du service

1. Créer/confirmer une nouvelle demande avec Alice.
2. Avec Bob, saisir `Avis RH favorable`, sélectionner Carol dans la recherche des responsables éligibles puis valider. Le commentaire est facultatif mais doit être conservé lorsqu’il est saisi.
3. Avec Carol, ouvrir la validation manager. Vérifier que le refus sans commentaire est bloqué.
4. Saisir `Accord manager`, puis approuver.
5. Avec Alice, vérifier le détail : les deux commentaires sont présents dans leur ordre d’ajout, attribués respectivement à Bob et Carol, sans écrasement ; le champ « Clôturée » vaut Oui après l’exécution du service.
6. La bibliothèque contient `cloture.txt`. Le fichier est lisible et correspond à cette demande. Le handler n’a pas besoin de retourner une liste de médias.
7. Faire une autre demande, puis refuser au manager avec un texte distinct : ce commentaire doit être conservé, sans exécuter le service de clôture.
8. Faire un dernier parcours avec approbation RH puis manager sans texte facultatif : aucune entrée vide ne doit être ajoutée.

Confirmation : `M3 OK` ou scénario précis en échec.

## M4 — Permissions, navigation et administration

Sur les demandes créées pendant M1–M3 :

- Une carte « Mes demandes » ouvre le détail ; les liens depuis Today et une tâche fonctionnent également. Le lien d’une tâche est dans la dernière ligne du tableau de détails.
- Détails : un encart unique avec métadonnées, badge statut et état métier. Les cinq onglets Détails, Bibliothèque, Progression, Historique et Commentaires restent distincts ; les tableaux Progression/Historique/Commentaires ont des en-têtes horizontaux.
- La loupe ouvre une modale avec croix, corps de formulaire et pied d’actions ; le statut ouvre une liste recherchable à cases à cocher, sans découpe ni différence majeure de hauteur. Sélectionner plusieurs statuts et vérifier les résultats ; une recherche vide présente des marges et des arrondis.
- L’administration « Identités » présente cinq colonnes et un menu « … ». Les changements/retraits passent par leurs modales. Utiliser uniquement une identité de recette non administrative pour tester un changement, puis restaurer son profil. Le retrait du dernier administrateur doit être refusé.
- La modale de profil reste au-dessus de l’en-tête et se ferme avec la croix, Annuler ou Échap.
- Un compte sans droit métier ne lit pas une instance en collant son URL ni un fichier en collant son URL de téléchargement ; la recherche ne révèle pas les données correspondantes. Tester aussi après retrait d’une permission dans le profil de recette.
- Audit : filtres et bouton CSV visibles ; export autorisé uniquement avec sa permission. Les événements de sécurité sans processus restent identifiés comme tels ; les changements de droits workflow affichent leur processus/étape. Rapports TAT accessibles sous leurs permissions dédiées.

Confirmation : `M4 OK` ou annotation/scénario précis à reprendre.

## M5 — Parcours Chromium et SMTP automatisés (long)

Ces scripts créent des packages, un environnement isolé, installent les templates et utilisent Docker/Mailpit pour le SMTP. Le répertoire de sortie doit être **nouveau**. Ils n’ont pas été relancés par l’agent pour cette itération.

Le répertoire doit être nouveau pour `qualify-l9.sh`. Le script navigateur est relançable sur un répertoire de packages déjà qualifié : chaque passage crée `browser-runs/run-XXXXXXXX` avec ses propres sources, base, artefacts, journaux et cache NuGet. Le chemin exact est affiché au lancement ; les passages antérieurs sont conservés.

Le script reconstruit également le package CLI depuis les sources actuelles puis l’installe dans `tools` de cette exécution, via un feed et un cache dédiés. C’est nécessaire car le CLI embarque le frontend Web : isoler seulement le cache du module ne renouvelle pas une ancienne installation du CLI. Le feed des dépendances du module reste celui préparé par `qualify-l9.sh` ; le reconstruire si ces dépendances ont changé.

```bash
./scripts/qualify-l9.sh /private/tmp/ew-l9b-packages-20261026 2>&1 | tee /private/tmp/ew-l9b-packages-20261026.log
./scripts/qualify-l9-browser.sh /private/tmp/ew-l9b-packages-20261026 2>&1 | tee /private/tmp/ew-l9b-browser-20261026.log
```

Si Chromium manque, suivre le diagnostic du script ou installer le navigateur du projet :

```bash
pwsh tests/EnterpriseWorkflow.Web.Playwright.Tests/bin/Release/net10.0/playwright.ps1 install chromium
```

Attendu : outils/packages/templates chargeables, embedded et Kernel opérationnels, scénarios LeaveRequest/SMTP réussis, y compris refus RH/manager avec texte conservé. Un échec avant le lancement des scénarios (Docker, navigateur, dépendance réseau) ne vaut pas validation fonctionnelle. Les scénarios ont été adaptés au processus de release 6 ; le manifeste packagé est en release 1.6.0 ; les sources utilisent une génération locale.

Confirmation : `M5 OK` avec résumés de tests et/ou journaux en cas d’échec. Les scripts utilisent les ports documentés dans leurs fichiers ; arrêter les services de recette qui occuperaient ces ports.

### Reprise après les échecs remontés le 26 octobre

Le premier passage navigateur a réussi deux scénarios sur quatre ; les branches de refus RH et manager échouaient sur l’affichage du commentaire accentué. Le commentaire était enregistré, mais la vue affichait les séquences JSON `\u00E9`. Le rendu est corrigé et couvert par un test Web local. Le passage M5 complet reste à confirmer avec les nouveaux packages.

Les packages locaux portent encore la version de développement `1.0.0` : ne pas réutiliser le cache ni les outils installés du premier passage. Relancer avec un **nouveau** répertoire (adapter le suffixe s’il existe déjà) :

```bash
./scripts/qualify-l9.sh /private/tmp/ew-l9b-packages-20261026-r2 2>&1 | tee /private/tmp/ew-l9b-packages-20261026-r2.log
./scripts/qualify-l9-browser.sh /private/tmp/ew-l9b-packages-20261026-r2 2>&1 | tee /private/tmp/ew-l9b-browser-20261026-r2.log
```

Le second retour signalait `FileExistsError` sur `LeaveRequest.Module` avant tout test. Le script est corrigé pour isoler automatiquement chaque passage. Si les packages et l’outil de ce répertoire ont déjà été reconstruits après le correctif d’affichage, une simple relance suffit :

```bash
./scripts/qualify-l9-browser.sh /private/tmp/ew-l9b-packages-20261026 2>&1 | tee /private/tmp/ew-l9b-browser-20261026.log
```

La syntaxe shell et deux préparations successives ont été vérifiées localement, avec conservation du passage précédent et sources/cache NuGet distincts. Ce contrôle ne vaut pas exécution Chromium/SMTP complète.

Troisième retour et vérification effective : le cache neuf utilisait encore le CLI installé au premier passage, qui embarquait l’ancienne DLL Web. Le script reconstruit et réinstalle maintenant le CLI dans chaque exécution. Une relance a ensuite révélé un conflit intermittent sur le heartbeat de session entre requêtes parallèles (identité perdue puis échec CSRF). La validation relit et recontrôle désormais la session avant une reprise bornée ; elle ne contourne ni révocation, ni expiration, ni CSRF.

Dernière exécution par l’agent : **4 scénarios navigateur réussis, 0 échec, 0 ignoré**, puis contrôle du module dans le Kernel de production réussi. Journal : `/private/tmp/ew-l9b-browser-agent-fix-r2.log`, environnement : `/private/tmp/ew-l9b-packages-20261026/browser-runs/run-5b2aYa3C`. Trois tests ciblés couvrent le conflit récupérable, la révocation pendant le conflit et la borne des tentatives. Le script navigateur passe donc désormais ; ce résultat ne revendique pas une nouvelle exécution complète de `qualify-l9.sh`.

## M6 — Oracle Enterprise 19.19 (long, schéma de test dédié)

Les tests Oracle réinitialisent leur schéma. Utiliser le schéma jetable `EWTEST` de la stack de test, jamais un schéma d’exploitation. Le conteneur n’a pas besoin d’être recréé pour chaque test.

```bash
./tests/oracle/oracle-test.sh status
./tests/oracle/oracle-test.sh test
```

Si la stack n’est pas initialisée ou démarrée, suivre [tests/oracle/README.md](../../tests/oracle/README.md), en particulier le choix d’image officielle 19.19, les credentials et la différence entre `up`, `down`, `reset` et `destroy`. Ne pas utiliser `reset` simplement pour relancer un test.

Pour une instance de test Oracle 19.19 existante, renseigner localement `ENTERPRISE_WORKFLOW_ORACLE_CONNECTION_STRING`, puis :

```bash
dotnet test --project tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --configuration Release --no-build --no-restore
```

Attendu : suite Oracle sans échec ni scénario ignoré. Le nouveau test `TypedApprovalCommentsAreCommittedOnceWithTheDecision` doit passer : commentaire requis, auteur serveur et absence de doublon de replay. La suite couvre également médias, fencing, TAT et transactions source. L’agent a compilé ce test, sans démarrer ni utiliser Oracle pour cette itération.

Confirmation : `M6 OK — Oracle Enterprise 19.19`, nombres réussi/échoué/ignoré, durée et sortie finale. Aucun secret de connexion à transmettre.

Correctif du 26 octobre après retour de recette : le script fournit déjà `--configuration Release`, il faut donc appeler `test` sans répéter cette option. Le nouveau test d’approbation démarrait avec une collection `comments` absente de l’état ; son état initial est corrigé. La suite a été relancée par l’agent sur le conteneur de test existant : **15 réussis, 0 échoué, 0 ignoré**, environ 16 secondes. Aucune recréation du conteneur n’a été nécessaire.

## M7 — Restauration et reprise (long)

1. Dans l’environnement 5099, terminer M3 et relever une demande avec justificatifs et fichier généré.
2. Arrêter le serveur avec Ctrl+C. Sauvegarder ensemble la base, le volume `$L9B_REVIEW_ROOT/media` et les artefacts `$L9B_REVIEW_ROOT/artifacts` de cet environnement. Sauvegarder aussi la configuration ; ne pas déplacer les binaires de média seuls.
3. Redémarrer sans changer les sources du module : détail, commentaires, fichiers et statut doivent rester présents ; aucun doublon d’étape ou de commentaire.
4. Serveur de nouveau arrêté, restaurer la sauvegarde de ces trois ensembles au même point de cohérence dans cet environnement de recette ; redémarrer et répéter les lectures/téléchargements. Pour Oracle, utiliser une vraie procédure de sauvegarde/restauration de la base, pas une copie des fichiers du conteneur en cours d’écriture.
5. Révoquer ensuite le droit de lecture du compte de recette et vérifier le refus des URLs précédemment copiées, y compris après restauration.

Confirmation : `M7 OK`, stockage/provider utilisé et lecture de fichiers vérifiée. Captures facultatives avant/après.

## M8 — Matrice CI, LDAP et distribution (long)

Lancer la workflow `.github/workflows/ci.yml` sur la branche contenant ces changements, via le mécanisme habituel du dépôt. Vérifier les jobs Linux/macOS/Windows, le navigateur Linux, OpenLDAP/LDAPS et les packages/templates. La compilation locale ne qualifie pas ces environnements. Aucun push ni lancement de CI n’a été effectué par l’agent pour cette recette.

Confirmation : `M8 OK`, lien de la pipeline ou résumés des jobs, et noms des éventuels jobs ignorés/en échec.

## Réponse minimale attendue

Copier ce tableau et remplacer « à faire ». Une preuve n’est nécessaire que si elle aide à expliquer un résultat.

| Bloc | Résultat | Détail facultatif |
| --- | --- | --- |
| M1 Formats réels | à faire | Formats/logiciels testés |
| M2 Refus RH | à faire | ID/capture |
| M3 Manager et clôture | à faire | ID/capture |
| M4 Permissions et UI | à faire | Scénario en échec |
| M5 Chromium/SMTP/packages | à faire | Résumé/journal |
| M6 Oracle 19.19 | à faire | Réussis/échoués/ignorés |
| M7 Restauration | à faire | Provider/volume |
| M8 CI/LDAP/matrice | à faire | Lien/résumé |

Le lot reste en qualification tant que les blocs requis ne sont pas confirmés. Une capture seule ne remplace pas le résultat d’une suite automatisée ; une confirmation textuelle explicite suffit à consigner le résultat du porteur.


## R1–R5 — Compléments après les ajustements acceptés

Les résultats antérieurs M1–M7 concernent l’itération précédente. Pour ces contrôles ciblés, préparer un environnement neuf selon la section 1 avec un nouveau suffixe de répertoire, après reconstruction des sources/packages actuels. Vérifier le sample **1.6.0 / contrat 6 / schéma 4** (génération locale en mode sources). La reconstruction et le packaging complet peuvent prendre plusieurs minutes ; l’agent laisse les formats réels, la distribution complète, la restauration et la CI à confirmer par le porteur.

- **R1 Référence et lignes** : créer deux demandes ; leurs références publiques font exactement dix caractères A–Z/0–9, distinctes et stables après actualisation/redémarrage. Utiliser Copier, puis rechercher la référence. Vérifier la même structure de ligne dans Today, Mes demandes, À traiter et Recherche (référence, processus, date, initiateur nom + ID, statut). Cliquer au clavier et à la souris : détail pour une demande, tâche pour une action à traiter. Les anciens liens GUID restent valables.
- **R2 Vues et onglets** : vérifier la vue « Préparez votre demande » au démarrage, puis le motif dans la vue personnalisée de confirmation. Les cinq onglets sont présents ; Détails possède un seul encart regroupant métadonnées et état, avec statut en badge. Progression et Historique ne figurent plus sous le tableau Détails.
- **R3 Commentaires** : rejouer un refus RH et un accord manager avec texte. Les entrées apparaissent dans l’onglet Commentaires, sous le label de leur collection, avec auteur (nom et ID), date et étape. Aucun JSON de commentaires ni référence média n’est présent dans le tableau d’état. Le refus sans commentaire et le replay restent couverts par M2/M3.
- **R4 Bibliothèque et permissions** : joindre un fichier réel et terminer un parcours avec clôture. Le fichier initial et `cloture.txt` doivent être présents et téléchargeables sans liste de références dans le DTO. Un acteur sans droit d’instance doit échouer par URL directe ; une politique média cachée/restrictive doit toujours masquer son fichier, indépendamment du state. Conserver les autorisations du fichier dans les politiques médias, pas dans `[HiddenField]` d’une référence.
- **R5 Audit** : après ces parcours, filtrer l’audit par module `sample.leave-request`, processus `sample.leave-request.leave-request` et action `decline` ou `validate`. Vérifier référence, acteur, date, étape et résultat ; exporter avec ces mêmes filtres, ouvrir le CSV et comparer les lignes. Vérifier aussi l’ID utilisateur seul, les deux bornes de date, les actions système et une page suivante sans doublons. Le résultat historique non enregistré doit rester explicite et ne pas être inventé.

Confirmer `R1 OK` à `R5 OK`, ou indiquer le scénario et le message en échec ; captures facultatives. La restauration complète et M8 ne sont pas réputés validés pour ces nouvelles migrations par une simple compilation.


## N1–N6 — Recette de sortie de l’itération CLI/DX (contrôles longs)

Ces contrôles portent sur la nouvelle itération et ne sont pas acquis par M1–M7 précédents. Les parcours courants utilisent uniquement .NET. Arrêter chaque hôte avant de réutiliser son port.

1. **N1 Sources et surveillance** : lancer `ew dev . --environment auteur` depuis le module (ou le DLL CLI compilé avec le chemin du module). Créer une demande, puis modifier une description/vue sans changer la release. Vérifier que la génération change, que la nouvelle instance utilise la génération courante et que l’ancienne se traite sur la sienne. Une erreur de compilation doit conserver l’ancienne génération active. Annuler l’erreur puis reprendre. Un redémarrage sans modification doit réutiliser la génération.
2. **N2 Isolation et reset** : lancer `--environment recette-rh`, puis `--environment recette-manager` ; les instances/base/médias doivent être distincts. Lancer `--environment recette-rh --reset` : une archive apparaît sous `.ew/archives`, seul cet environnement repart à zéro et manager conserve ses données. Une base externe explicite ne doit pas être archivée. Deux hôtes du même environnement ne doivent pas s’exécuter simultanément.
3. **N3 Release ciblée et cache** : produire `ew pack <module> --configuration Release --output <nouveau-répertoire>`. Lancer `ew dev --artifact <sortie>/sample.leave-request/1.6.0 --environment qualification --config <module>/ew.dev.json`. Aucun build/watch ne doit se produire. Créer une instance, arrêter, déplacer le dossier externe, puis relancer depuis les sources dans le même environnement : l’instance de release doit rester consultable grâce au registre local. Les flags CLI de build/watch avec `--artifact` doivent être refusés. Un manifeste de génération locale doit être refusé par `ew verify` et le Kernel de production.
4. **N4 Artefacts requis** : hôte arrêté, déplacer provisoirement un artefact référencé par une instance (uniquement dans la recette) ; le démarrage doit échouer en indiquant l’identité exacte et les instances concernées, sans choisir une autre version. Remettre le dossier. Un artefact inutilisé/incompatible simplement présent dans le cache ne doit pas bloquer le lancement. Vérifier aussi une dépendance externe importée et conservée.
5. **N5 Configuration et budgets** : vérifier CLI > environnement > commun > défauts pour URL, build et watch. Tester `serialization.maximumStateBytes` / `maximumDepth` sur un environnement dédié avec des valeurs positives sous les bornes du moteur. Un état dépassant le budget doit être refusé sans remplacement partiel, y compris après `Replace<T>`. Les JSON de naming/enums/converters persistants ne deviennent pas des réglages globaux. Rejouer le parcours de confirmation : `Success()` n’a plus d’outcome, l’action définie conserve sa destination ; rejets et replay restent atomiques.
6. **N6 Distribution et matrice** : reconstruire les packages dans un répertoire neuf avec `qualify-l9.sh`, rejouer navigateur/SMTP et Kernel avec `qualify-l9-browser.sh`, puis M8/CI et restauration cohérente de l’environnement complet (base, médias, artefacts, registre de générations, clés, configuration). Ne pas recopier uniquement les DLL ou la base.

Test automatisé natif opt-in, compilant le sample plusieurs fois, à lancer après le build de la solution :

```bash
EW_DEV_LIFECYCLE_TESTS=1 dotnet test --project tests/EnterpriseWorkflow.Cli.Tests/EnterpriseWorkflow.Cli.Tests.csproj --configuration Release --no-build --no-restore -- --filter-method '*SourceGenerationsPreserveInstances*'
```

Sous PowerShell, définir d’abord `$env:EW_DEV_LIFECYCLE_TESTS = '1'`, puis exécuter la commande `dotnet test` sans le préfixe d’environnement Unix. Ce test est optionnel dans les suites courantes ; il vérifie deux générations, reprise, isolation, reset archivé, artefact ciblé, cache après déplacement et artefact requis manquant. Il ne qualifie pas à lui seul la surveillance longue ni la matrice OS.

Réponse attendue : `N1 OK` à `N6 OK`, ou scénario en échec ; captures/logs facultatifs sauf résumé des suites automatisées et lien CI pour N6.
