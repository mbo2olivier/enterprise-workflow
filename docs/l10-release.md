# L10 — préparation et exécution d’une release

Statut au 28 octobre 2026 : **L10 clos sur confirmation du porteur**, implémentation réussie, release créée et packages publiés sur NuGet. Voir la [clôture](evidence/l10.md#clôture--28-octobre-2026). Les mentions de préparation ci-dessous décrivent la procédure et les constats historiques du 27 octobre.

Pour créer les comptes, configurer les droits et identifier les décisions attendues
du porteur, suivre le [guide de première publication](l10-nuget-setup.md).

## Licence et catalogue

À la demande du porteur de choisir une licence permissive permettant les forks
internes en banque et la réutilisation dans ses autres projets, Apache-2.0 est
retenue. Le texte officiel est dans `LICENSE`. Elle permet l’utilisation privée,
commerciale et les dérivés propriétaires ; elle inclut une concession de brevets
des contributeurs dans son périmètre. Redistribuer impose notamment de joindre la
licence, conserver les mentions applicables et signaler les fichiers modifiés.
Elle ne garantit pas l’absence de risques et ne change pas les licences tierces.

Le [catalogue candidat](../release/catalog.json) énumère les 18 bibliothèques,
l’outil CLI et les templates (20 packages au total).
Le Kernel est distribué en archive ; tests, fixtures et samples ne sont pas des
packages publics. `1.0.0-rc.1` et les IDs actuels sont retenus avec les templates. Le propriétaire
individuel et auteur public est `mukadi.sheba` (confirmé par le porteur). La
disponibilité des IDs et la configuration effective du compte restent à vérifier.

## Préparer les artefacts

### Changer de version et vérifier avant commit/tag

Pour passer, par exemple, de `1.0.0-rc.1` à `1.0.0`, modifier ensemble :

1. `release/catalog.json` : champ `version`.
2. `Directory.Build.props` : propriété `Version`.
3. `templates/module/WorkflowSample.csproj` et
   `templates/embedded/WorkflowSample.csproj` : versions des références
   `EnterpriseWorkflow.*`.
4. `release/NOTES.md` : titre `# Enterprise Workflow 1.0.0` et notes actualisées.

Depuis la racine, **avant le commit/push et sans créer de tag** :

```bash
python3 scripts/check-release.py
```

Ce contrôle local ne contacte aucun service et ne modifie aucun fichier. Il
vérifie le format de version et l’alignement catalogue/props/templates/titre des
notes. Il affiche la version et le propriétaire en cas de succès ; tout écart
provoque une erreur et un code de sortie non nul. Il ne remplace pas les builds,
tests, contrôles de packages ou vérifications des droits NuGet.

Après succès des contrôles et qualification, committer puis pousser les fichiers,
et créer le tag correspondant (`v1.0.0` pour la version `1.0.0`) sur ce commit.
La CI réexécute le contrôle avec `--tag` et vérifie aussi que le commit appartient
à `main`. Cette option est destinée à la CI : elle contacte le remote `origin`,
alors que le remote de ce checkout local est nommé `github`.

Les scripts de recette lisent automatiquement la version du catalogue. Les
versions métier des modules d’exemple (par exemple LeaveRequest) sont distinctes
et ne doivent pas être remplacées pour cette seule montée du framework.

Depuis un checkout propre et le SDK épinglé :

```bash
dotnet restore EnterpriseWorkflow.slnx --locked-mode
python3 scripts/prepare-release.py artifacts/release --public
python3 scripts/archive-kernel.py osx-arm64 artifacts/kernel
```

Chaque sortie doit être nouvelle. Les packages incluent README et licence ; la
préparation contrôle identités, versions, dépendances internes et fichiers locaux
interdits, puis produit les SHA-256 et l’identité du build dans `manifest.json`.
Ce contrôle de chemins ne remplace pas une revue des secrets et des dépendances.
Le script ne modifie aucun flux NuGet. Le workflow manuel/réutilisable `release-candidate`
prépare les packages et les deux modes d’archives sur Linux x64, Windows
x64 et macOS ARM64. Les archives autonomes sont disponibles via
`--self-contained` et leur smoke est prévu sur chaque runner. Aucun trimming/AOT.
Les archives sont marquées `qualified: false` jusqu’à acquisition des preuves.

## Gate avant publication

Q06 est clos le 27 octobre 2026 par décision explicite du porteur : aucun seuil
de charge/performance, disponibilité ou RPO/RTO ni test correspondant requis.
Aucune garantie chiffrée n’est annoncée ; les utilisateurs dimensionnent leur
installation. Fermer les autres volets G4 : qualifier restauration/migrations,
sécurité, Oracle 19 et chaque archive
sur son OS réel avec un module externe et attente/reprise. La compilation et un
checksum ne constituent pas cette qualification. Inventorier les licences
transitives, notamment les conditions des composants Oracle et des assets.

Vérifier le catalogue, le propriétaire NuGet et les noms disponibles. Construire
depuis un commit propre, conserver le manifeste et les logs de CI, inspecter le
contenu des packages et les configurations. Ne jamais placer une clé de
publication dans le dépôt ou les preuves.

## Publication et reprise

Employer une identité NuGet protégée (secret du système de release ou identité
de confiance configurée sur NuGet). Publier exactement les packages validés,
aux SHA-256 conservés, avec `dotnet nuget push` vers
`https://api.nuget.org/v3/index.json`. Publier les bibliothèques avant CLI/templates.
Pour une reprise partielle, comparer l’identité et le contenu déjà publiés à
l’inventaire ; ne pas masquer tous les conflits par `--skip-duplicate`.
Une version publique est immuable : tout changement de contenu exige une nouvelle
version et l’alignement des dépendances et templates.

## Validation depuis NuGet.org

Pour la version candidate 1.0.0-rc.1, une fois les packages publiés :

```bash
./scripts/qualify-l9.sh /tmp/ew-l10-public-consumption public
```

Le mode `public` crée un répertoire neuf, copie `global.json`, définit
`NUGET_PACKAGES` vers un cache vide et écrit un `NuGet.Config` avec `<clear />`
et uniquement NuGet.org. Il installe CLI/templates 1.0.0-rc.1 avec une installation de
templates isolée. Le script lit la version dans le catalogue ; le contrôle de release vérifie son
alignement avec les templates et Directory.Build.props. Le parcours se déroule
sans `pack-local.sh`, sans flux local et sans références projet au dépôt : module,
validation, inspect, pack, verify, embedded et qualification `ew dev`. Charger le
module dans l’archive Kernel correspondant au RID, attendre/reprendre un workflow,
puis vérifier sauvegarde/restauration selon [operations.md](operations.md).
Conserver commandes, résultats, versions, OS/CPU, providers, liens NuGet et hashes.

L10 reste ouvert tant que publication complète et consommation publique ne sont
pas prouvées. Le wiki public relève de L10b.

## Pipeline de publication livrée

`publish-release.yml` accepte les tags `v*` et les relances manuelles **sur un tag**.
Le tag doit être `v` + version du catalogue et son commit appartenir à `main`.
La CI complète et la préparation/qualification des six archives précèdent le job
protégé `nuget-release`. Celui-ci utilise Trusted Publishing avec `mukadi.sheba`.
Configurer la politique NuGet pour `publish-release.yml` et cet environnement.
Aucun secret API permanent n’est nécessaire ; le pseudo est explicite dans le YAML.

La publication vérifie inventaire, SHA-256, commit propre et versions ; une reprise
ne saute une version existante qu’après comparaison des contenus ZIP, signature
NuGet exclue. Relancer les **jobs échoués** pour conserver les artefacts originaux.
Une reconstruction différente ne remplace jamais un package existant.
Après indexation et recette publique sur trois OS, la GitHub Release reçoit les
six archives, le manifeste NuGet et les checksums. Les notes de release doivent
être mises à jour avec la version. Les préversions sont marquées comme telles.

La pipeline n’a pas été exécutée à distance tant que ces fichiers ne sont pas
committés/poussés ; la politique NuGet est confirmée configurée par le porteur le 27 octobre 2026. Aucun succès de
publication ou de matrice distante ne se déduit des tests locaux.
