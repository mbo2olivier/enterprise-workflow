# L13 — recette des tests lourds délégués au porteur

Date : 24 novembre 2026. Statut : à exécuter. Les tests légers acquis sont dans [la preuve L13](../evidence/l13.md). Les étapes ci-dessous ne sont pas présentées comme exécutées. Elles ne publient aucun package et n’autorisent pas une suppression d’artefacts de production. Utiliser uniquement les bases et répertoires jetables de cette recette.

## 1. Préparer et compiler

1. Depuis la racine du dépôt, relever `git rev-parse HEAD`, `git status --short`, `dotnet --info` et l’OS/CPU. Le SDK doit correspondre à `global.json`.
2. Créer un workspace externe et normaliser les alias macOS :

   ```sh
   export EW_L13_ROOT="$(mktemp -d -t ew-l13.XXXXXX)"
   export EW_L13_ROOT="$(python3 -c 'import os,sys; print(os.path.realpath(sys.argv[1]))' "$EW_L13_ROOT")"
   ```

3. Restaurer puis compiler les fichiers verrouillés :

   ```sh
   dotnet restore EnterpriseWorkflow.slnx --locked-mode
   dotnet build EnterpriseWorkflow.slnx --configuration Release --no-restore
   ```

4. Attendu : zéro avertissement/erreur. Ne pas effacer les lockfiles en cas de différence. Aucun package public de même version ne doit remplacer les sources de cette qualification.

## 2. Préparer deux hôtes et leurs artefacts

1. Produire un artefact standard et un artefact organisationnel à partir des projets source déjà compilés :

   ```sh
   dotnet run --project src/EnterpriseWorkflow.Cli -c Release --no-build -- pack samples/LeaveRequest.Module --configuration Release --output "$EW_L13_ROOT/modules"
   dotnet run --project samples/OrganizationWorkflow/Cli -c Release --no-build -- pack samples/OrganizationWorkflow/Module --configuration Release --output "$EW_L13_ROOT/custom-modules"
   ```

2. Attendu : un `module.json` sous chaque arborescence `module-id/release`. Le packaging reste explicite et ne scanne pas tout le disque. Conserver ces sorties immuables.
3. Publier les hôtes :

   ```sh
   dotnet publish src/EnterpriseWorkflow.Kernel -c Release --no-restore -o "$EW_L13_ROOT/kernel"
   dotnet publish samples/OrganizationWorkflow/Kernel -c Release --no-restore -o "$EW_L13_ROOT/custom-kernel"
   ```

4. Générer les configurations jetables avec le script dédié, sans heredoc ni indentation à recopier :

   ```sh
   python3 scripts/prepare-l13-config.py "$EW_L13_ROOT"
   ```

   Le script vérifie que les deux hôtes ont été publiés avant toute modification. Il conserve les configurations présentes sous `appsettings.before-l13.json` et refuse de remplacer une sauvegarde existante. Les configurations créées utilisent les bases jetables, la découverte relative et la liste individuelle volontairement invalide prévues par cette recette. En cas de sauvegarde déjà présente, ne pas la supprimer pour forcer une réexécution : vérifier d’abord si les configurations ont déjà été créées.

5. Migrer et initialiser les deux bases :

   ```sh
   dotnet "$EW_L13_ROOT/kernel/EnterpriseWorkflow.Kernel.dll" migrate
   dotnet "$EW_L13_ROOT/kernel/EnterpriseWorkflow.Kernel.dll" bootstrap l13-admin 'Local-L13-Test-2026!'
   dotnet "$EW_L13_ROOT/custom-kernel/Example.Workflow.Kernel.dll" migrate
   dotnet "$EW_L13_ROOT/custom-kernel/Example.Workflow.Kernel.dll" bootstrap l13-admin 'Local-L13-Test-2026!'
   ```

6. Ces identifiants sont exclusivement destinés aux bases jetables. Ne pas joindre de credentials/cookies aux preuves.

## 3. Démarrage et priorité de configuration

1. Démarrer le Kernel standard depuis la racine du dépôt, donc un répertoire courant différent de son content root :

   ```sh
   ASPNETCORE_URLS=http://127.0.0.1:5083 dotnet "$EW_L13_ROOT/kernel/EnterpriseWorkflow.Kernel.dll"
   ```

2. Attendu : diagnostic `EW7064_MODULE_DISCOVERY_SELECTED`, avec la racine publiée et l’indication que la liste individuelle est ignorée. Le chemin individuel volontairement invalide ne doit pas empêcher le démarrage. Le chemin relatif `../modules` est résolu depuis le content root du Kernel publié.
3. Ouvrir `/login`, se connecter, puis `/admin/workflows`. Vérifier la présence de LeaveRequest et ses permissions. Les routes `/__ew/dev` ne doivent pas être exposées. Arrêter avec Ctrl+C avant chaque modification suivante.
4. Retirer entièrement la clé `RootDirectory` de l’appsettings, puis remplacer `Directories` par la liste contenant le **répertoire exact** du manifeste LeaveRequest. Redémarrer : diagnostic `EW7065_EXPLICIT_MODULES_SELECTED` et même catalogue. Ne pas simplement mettre RootDirectory à une chaîne vide : cela constitue une configuration invalide.
5. Remettre `RootDirectory` avec un chemin inexistant et garder la liste valide. Attendu : échec `EW7060_INVALID_DISCOVERY_ROOT`, aucun fallback. Répéter avec une chaîne vide, des espaces et `null`.
6. Restaurer `RootDirectory: ../modules` et la liste invalide avant de poursuivre. Aucune fusion ne doit avoir lieu.

## 4. Hôte personnalisé et éligibilité

1. Démarrer le Kernel organisationnel :

   ```sh
   ASPNETCORE_URLS=http://127.0.0.1:5084 dotnet "$EW_L13_ROOT/custom-kernel/Example.Workflow.Kernel.dll"
   ```

2. Se connecter à `/login`, vérifier la marque et la présence du processus organisationnel dans l’administration. Le partage de contrats et les options runtime L12 restent fournis par la composition compilée.
3. Pour exécuter aussi une demande métier, lancer l’API de démonstration et configurer les profils nécessaires comme dans la recette L12 :

   ```sh
   dotnet run --project samples/OrganizationWorkflow/PolicyApi -c Release --no-build -- --urls http://127.0.0.1:5099
   ```

4. Arrêter le Kernel standard. Copier le répertoire `custom-modules` **comme sous-dossier supplémentaire** de sa racine `modules`, sans déplacer le LeaveRequest existant. Redémarrer : diagnostic `EW7055_INCOMPATIBLE_RUNTIME` pour l’artefact organisationnel, LeaveRequest toujours chargé. Aucun service du module incompatible ne doit être exécuté.
5. Arrêter le Kernel standard et retirer uniquement cette copie de test de la racine. Pour la coexistence de releases éligibles, utiliser deux artefacts réellement produits depuis des releases distinctes (nouvelle release et révision auteur), sans modifier simplement la version du manifeste. Les deux doivent être inventoriés ; aucune sélection silencieuse de la seule dernière version.

## 5. Anomalies, limites et liens

Effectuer ces cas sur des copies de travail de la racine, avec les hôtes arrêtés, puis restaurer les fichiers originaux avant le cas suivant. Ne pas transformer un échec attendu en lancement prêt avec un catalogue incomplet.

1. Copier deux fois le même artefact sous deux chemins : démarrage refusé `EW7012_DUPLICATE_MODULE_VERSION`. Ce contrôle précède le filtrage des incompatibles.
2. Modifier un octet d’une ressource manifestée dans une copie : refus `EW7006_MODULE_FILE_HASH_MISMATCH`, même si ce candidat annonce un autre runtime. Restaurer la copie avant toute réutilisation.
3. Ajouter un `module.json` invalide dans un sous-répertoire non artefact : refus `EW7003_INVALID_MODULE_MANIFEST`. Un dossier sans manifeste, en revanche, n’est pas un candidat.
4. Ajouter un faux manifeste dans `dependencies/nested` **sous un artefact déjà découvert** : il ne doit pas devenir un autre candidat, puisque le scan s’arrête à cet artefact. Ce faux fichier non manifesté est uniquement une donnée de test ; le retirer ensuite.
5. Créer un lien vers une arborescence extérieure : lien ignoré, diagnostic `EW7063_DISCOVERY_LINK_SKIPPED`, rien d’extérieur chargé. Sous Windows, tester aussi une jonction ; noter les permissions nécessaires à la création du lien. Une racine configurée qui est elle-même un lien est refusée.
6. Rendre un sous-répertoire de scan inaccessible au compte du Kernel : refus `EW7061_DISCOVERY_SCAN_FAILED`. Le compte ne doit pas être administrateur/root pour prouver ce cas. Restaurer les droits avant nettoyage ; sous Windows utiliser une ACL temporaire dans le workspace jetable.
7. Fixer `MaximumDepth` à 0 alors qu’un sous-répertoire doit être exploré ; puis `MaximumDirectories` à 1 ; puis `MaximumCandidates` à 1 avec deux candidats ; enfin `MaximumEntries` à 1 avec plusieurs entrées à parcourir. Chaque dépassement produit `EW7062_DISCOVERY_LIMIT_EXCEEDED`, sans catalogue partiel accepté. Restaurer les limites normales.
8. Une racine existante vide est valide pour la découverte. La tester sur une **nouvelle base vide migrée**, afin de ne pas confondre ce cas avec la rétention d’artefacts déjà publiés.

## 6. Rétention inchangée jusqu’à L14

1. Revenir à la racine LeaveRequest et à la base standard utilisées lors du premier démarrage. Vérifier que des définitions existent en base (`EwDefinitions`) ; au besoin créer une demande autorisée via les profils du module et conserver son identifiant.
2. Arrêter le Kernel. Déplacer l’artefact exact hors de la racine scannée (pas dans un sous-dossier « archive » sous cette racine). Redémarrer avec la même base.
3. Attendu : refus de réconciliation `EW3051_REQUIRED_MODULE_ARTIFACT_MISSING`, éventuellement enveloppé par le diagnostic de readiness. Le fallback individuel ne doit pas réintroduire le module manquant lorsque RootDirectory existe.
4. Remettre le même artefact et ses octets d’origine, puis redémarrer : reprise et historique disponibles. Un remplacement par une autre release ou un hash différent ne vaut pas restauration.
5. Même si les activations sont terminées, L13 conserve le besoin des définitions publiées. Ne pas considérer l’échec comme une régression de L14 : le retrait des anciennes versions sera livré séparément.

## 7. Non-régression et CI

1. Exécuter le socle Release :

   ```sh
   dotnet test --solution EnterpriseWorkflow.slnx --configuration Release --no-build --no-restore
   ```

2. Rejouer les qualifications de packaging et navigateur L9/L12 selon leurs recettes. `tests/cli/qualify-kernel.py` utilise désormais la découverte sur son artefact et configure une liste individuelle invalide pour contrôler la priorité. La CLI dev continue d’utiliser son catalogue explicite et ses identités de test.
3. Le script L12, phase `kernel`, configure une racine d’artefacts pour l’hôte personnalisé. Ne pas mélanger les workspaces ou les caches NuGet de qualification.
4. Rejouer le cycle natif et les tests navigateur activés selon L12 ; aucun hot reload de modules en production n’est attendu.
5. Vérifier la matrice Linux/macOS/Windows et, si Oracle est dans la qualification de release, son schéma dédié avec la recette Oracle existante. Aucune migration SQL L13 n’est nécessaire ; ne pas déduire une qualification Oracle d’un test SQLite.
6. Documentation : contrôle du snapshot public, tests/check/build wiki selon la recette existante. Les nouveautés source ne sont pas supposées déjà publiées sur NuGet.
7. Consigner commandes, OS/CPU, SHA, configuration, sorties et nombres de tests passés/échoués/ignorés dans la preuve L13. Les tests ignorés faute de privilèges ne sont pas des succès. Aucun lot n’est clôturé sans confirmation des campagnes déléguées.

### Reprise après « Localization cultures must be non-empty and unique »

Le chargement des options ajoutait le tableau JSON aux deux langues par défaut, créant artificiellement des doublons. Le correctif remplace les défauts par la liste configurée tout en refusant les vrais doublons et listes vides. Les configurations de la recette restent valides ; il faut actualiser les DLL des hôtes déjà publiés.

Arrêter les deux Kernels, puis lancer depuis la racine du dépôt :

```sh
python3 scripts/prepare-l13-config.py --refresh-hosts "$EW_L13_ROOT"
ASPNETCORE_URLS=http://127.0.0.1:5083 dotnet "$EW_L13_ROOT/kernel/EnterpriseWorkflow.Kernel.dll"
```

L’option refresh republie les deux hôtes depuis les sources et restaure leurs fichiers appsettings*.json existants, même si la publication échoue. Elle ne modifie ni les bases, ni les artefacts de modules, ni les backups initiaux de configuration. Ne pas relancer la préparation initiale ni supprimer SupportedCultures pour contourner l’erreur. Aucune nouvelle migration ou bootstrap n’est nécessaire pour ce correctif.
