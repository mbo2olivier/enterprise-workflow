# Utiliser la CLI et le runtime de développement L9

Implémentation locale du 20 octobre 2026. Les packages `1.0.0` ci-dessous sont des artefacts de travail locaux ; aucune publication NuGet.org n’est effectuée en L9. La qualification et les limites sont consignées dans la [preuve L9](evidence/l9.md).

## Essayer immédiatement depuis les sources

Depuis la racine du dépôt, sans installer l’outil :

```bash
dotnet run --project src/EnterpriseWorkflow.Cli --configuration Release -- dev --project samples/LeaveRequest.Module
```

Ouvrir l’URL annoncée, choisir Alice, Bob ou Carol et suivre le sample. Le fichier `ew.dev.json` du sample configure leurs droits ; aucun SMTP n’est activé par défaut.

## Installer depuis le dépôt

SDK requis : celui de `global.json` (.NET 10). Produire les packages locaux :

```bash
./scripts/pack-local.sh /tmp/ew-feed
```

Installer la CLI depuis ce flux et les templates depuis leur fichier :

```bash
dotnet tool install EnterpriseWorkflow.Cli --tool-path /tmp/ew-tools --add-source /tmp/ew-feed --version 1.0.0
dotnet new install /tmp/ew-feed/EnterpriseWorkflow.Templates.1.0.0.nupkg
```

Ajouter `/tmp/ew-tools` au PATH ou employer le chemin absolu de `ew` (`ew.exe` sur Windows). Configurer le flux local pour la restauration des projets générés ; leurs références aux packages du framework ne sont pas encore disponibles publiquement. La [procédure automatisée](../scripts/qualify-l9.sh) utilise un cache, une installation de templates et un répertoire d’outils isolés. Les noms et versions publics seront arrêtés en L10.

## Créer et explorer un module

```bash
dotnet new ew-module --name Acme.Leave --module-id acme.leave
cd Acme.Leave
ew dev
```

Ouvrir l’URL annoncée. Le sélecteur permet de choisir Alice (demandeuse), Bob (approbateur) ou l’administration locale. Alice crée une demande ; Bob la prend et approuve ou refuse. Le compte d’administration ne reçoit pas automatiquement de droits métier. Un bandeau permet de revenir au sélecteur.

Le template est un module C# compilable avec schéma d’état explicite, déclaration des permissions, formulaire, approbation standard et service de clôture. Il inclut `ew.dev.json` et les exclusions Git de l’espace local. Le sample [LeaveRequest](../samples/LeaveRequest.Module/README.md) offre le parcours complet confirmation → RH → manager désigné.

```bash
ew dev --project ./Acme.Leave.csproj --url http://localhost:5090
ew dev --config ./ew.dev.json --no-watch
ew validate .
ew inspect .
ew inspect . --format json --output ./artifacts/resolved-module.json
ew pack . --configuration Release --output ./artifacts
ew verify ./artifacts/acme.leave/1.0.0
```

Sans argument, le projet est recherché dans le répertoire courant ; plusieurs projets exigent `--project`. `--configuration` accepte `Debug` (défaut) ou `Release`. Les diagnostics de build vont sur stderr pour conserver une sortie JSON exploitable avec `inspect --format json`. Une opération en échec renvoie un code non nul (1 pour les diagnostics CLI gérés) ; le chemin de l’artefact est annoncé seulement après validation. `verify` ne modifie pas l’artefact.

## Configurer le runtime

Voir le [schéma v1](schemas/ew.dev.schema.json). Les sections absentes reçoivent leurs valeurs par défaut. Priorité : options CLI > fichier > défauts. Le fichier implicite est `ew.dev.json` dans la racine du projet. Les chemins sont relatifs au répertoire du fichier ; sans fichier, à la racine du projet.

```json
{
  "schemaVersion": 1,
  "server": { "url": "http://localhost:5080" },
  "storage": { "provider": "sqlite", "path": ".ew/dev/workflow.db" },
  "moduleDirectories": [],
  "watch": true,
  "seed": {
    "users": [
      { "id": "alice", "displayName": "Alice", "permissions": ["acme.leave.request", "acme.leave.read"] },
      { "id": "bob", "displayName": "Bob", "permissions": ["acme.leave.review", "acme.leave.read"] }
    ]
  }
}
```

Les propriétés inconnues, versions de schéma non prises en charge, permissions inconnues et providers autres que `sqlite` sont refusés. L’URL doit être une adresse HTTP de boucle locale, avec port non nul et sans chemin supplémentaire. SMTP est désactivé en l’absence d’hôte. YAML et Oracle/LDAP pour le runtime de développement restent différés.

L’hôte applique explicitement les migrations SQLite du moteur et de la sécurité, réconcilie l’inventaire puis initialise le seed. Les identités ont un sujet local stable `ew-dev-<id>`. Le seed n’est appliqué qu’à l’initialisation : une révocation administrative n’est pas annulée à la relance. Un changement du seed est diagnostiqué ; restaurer l’ancien fichier ou réinitialiser explicitement. Une initialisation interrompue laisse un marqueur et impose une réinitialisation, sans rejouer silencieusement des attributions partielles.

## Reconstructions et versions

La surveillance porte sur les sources C#, Razor, CSS, JavaScript et les fichiers projet/props/targets du projet. Elle exclut les sorties de compilation, `.ew`, les répertoires de packages d’artefacts et `node_modules`. Les dépendances de code externes au répertoire du projet et les changements du fichier JSON demandent une relance explicite.

Une erreur de compilation conserve l’ancien hôte actif. Une nouvelle release est validée avec les générations retenues avant arrêt/redémarrage contrôlé. Les DLL sont préparées et validées dans des sous-processus pour ne pas rester verrouillées dans le superviseur Windows. Les tâches, instances et droits persistent entre lancements.

**Modifier le code sous une release déjà utilisée est refusé.** Changer explicitement la release et la révision des processus redéclarés : leur définition inclut l’artefact courant et son hash. Conserver une ancienne révision exige sa réutilisation explicite depuis son propriétaire exact. Exemple : `1.0.0 / révision 1` → `1.0.1 / révision 2`. Les anciennes instances gardent leurs artefacts et handlers exacts. Aucun hot reload ni suffixe de version automatique.

Pour repartir volontairement de zéro :

```bash
ew dev --reset
```

La commande archive l’ancien `.ew/dev` dans `.ew/archives/<identifiant>` avant de créer le nouvel environnement ; elle ne supprime pas les anciennes données. Elle refuse de réinitialiser un stockage externe à cet espace ou un espace symbolique. Une session concurrente sur le même espace est refusée. Ctrl+C arrête le superviseur et l’hôte ; les outils d’automatisation peuvent envoyer une ligne `stop` sur stdin redirigé.

Si un hôte redémarré échoue malgré la prévalidation (port occupé, stockage indisponible, etc.), le runtime est signalé arrêté ; aucune reprise silencieuse ni suppression des données n’est effectuée.

## Référencer d’autres artefacts exacts

Les générations locales d’un même projet sont retenues automatiquement. Pour les références SDK à des contrats appartenant à un autre artefact, fournir explicitement ce propriétaire :

```bash
ew validate . --module-directory ../retained-module/1.0.0
ew inspect . --module-directory ../retained-module/1.0.0 --format json
ew pack . --module-directory ../retained-module/1.0.0
ew verify ./artifacts/acme.leave/2.0.0 --module-directory ../retained-module/1.0.0
```

L’option est répétable et disponible aussi pour `dev`. `moduleDirectories` permet la même configuration persistante dans `ew.dev.json`. Chaque répertoire doit contenir un artefact valide. Les propriétaires ne sont pas fusionnés dans le package courant : ils doivent rester disponibles dans l’environnement de déploiement. Toute collision ou référence incompatible est refusée.

## Tester SMTP et le parcours complet

La stack [SMTP Docker Compose](../tests/smtp/README.md) fournit une capture réelle avec interface locale. Ajouter explicitement la section `smtp` documentée à la configuration de développement. Elle utilise les options du transport existant ; aucun email n’est envoyé en l’absence d’activation.

La procédure intégrée démarre une copie isolée du sample, utilise la CLI installée, lance Chromium et vérifie le parcours, les refus d’accès, la désignation obligatoire, les projections, la panne SMTP, le retry durable et la livraison après reprise :

```bash
./scripts/qualify-l9.sh /tmp/ew-l9-qualification
# Après installation de Chromium selon la procédure Playwright du dépôt :
./scripts/qualify-l9-browser.sh /tmp/ew-l9-qualification
```

Choisir un nouveau répertoire de qualification. La première procédure couvre aussi la surveillance et la complétion d’une ancienne instance après redémarrage avec deux versions. La seconde nécessite Docker et utilise les ports SMTP 1025, capture 8025 et application 5087 ; elle arrête son runtime et sa stack à la sortie. Le serveur de capture ne prouve ni délivrabilité Internet, ni TLS/authentification de production.

## Exemple embedded et limites

`dotnet new ew-embedded --name Acme.Portal` génère une application qui charge le même package et la même définition, compose le moteur et SQLite, puis démarre une instance via les services applicatifs. Les permissions sont fournies explicitement ; l’exemple ne remplace pas l’autorisation par un succès simulé. Voir son README généré pour les arguments.

Le packaging initial produit des répertoires installables pour des modules fluents `WorkflowModule` avec un seul point d’entrée public dans l’assembly cible du projet. Les dépendances managées et les ressources `wwwroot` sont manifestées et hachées. Les dépendances natives/RID et satellites ne sont pas prises en charge par ce format : la commande les refuse plutôt que produire un artefact incomplet. Les archives, les métadonnées de publication et les versions publiques restent dans L10. La qualification CI et les scénarios non exécutés sont distingués dans la preuve L9.
