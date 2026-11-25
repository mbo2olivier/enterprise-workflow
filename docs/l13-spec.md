# L13 — découverte des modules depuis un répertoire

Date : 24 novembre 2026. Statut : implémentation locale le 24 novembre 2026 ; qualification lourde en attente du porteur. Voir [preuve](evidence/l13.md) et [recette](validation/l13-recette-porteur.md). Décision : [ADR 0031](adr/0031-decouverte-modules-et-retrait-artefacts.md). L14 traite séparément le retrait des anciens artefacts ; Studio est L15.

## Configuration et priorité

```json
{
  "Modules": {
    "RootDirectory": "/opt/enterprise-workflow/modules",
    "Directories": ["/legacy/module-a/1.0.0"]
  }
}
```

Si `Modules:RootDirectory` est renseigné, seule la découverte sous cette racine détermine le catalogue. `Modules:Directories` est intégralement ignoré : aucune fusion, validation de ses chemins, activation ni recherche de secours dans cette liste. Un diagnostic indique le mode sélectionné et que la liste est ignorée.

En l’absence de répertoire de découverte configuré, `Modules:Directories` conserve le comportement actuel. Une racine explicitement vide, invalide, inaccessible ou absente provoque une erreur de configuration ; elle ne déclenche jamais le fallback. Un répertoire existant et vide donne un catalogue vide, ensuite soumis aux contrôles de références persistées. Le chemin relatif du répertoire de découverte est résolu contre le content root de l’hôte, indépendamment du répertoire courant du processus. Le fallback explicite conserve sa résolution historique, pour ne pas changer les configurations existantes.

## Découverte et éligibilité

Scanner au démarrage uniquement ; aucun watcher ni activation à chaud. Accepter la racine elle-même comme artefact si elle contient un `module.json`, sinon explorer ses sous-répertoires, dans un ordre déterministe. Arrêter la descente dès qu’un manifeste est trouvé : les dépendances et ressources de cet artefact ne deviennent pas d’autres modules. La structure habituelle `module-id/release/module.json` issue de `ew pack` est compatible, sans imposer les noms de dossiers comme identité.

Ignorer les répertoires sans manifeste. Ne pas suivre les liens symboliques/jonctions, ni sortir de la racine physique ; les contrôles existants du contenu d’un artefact restent applicables. Un accès impossible pendant le scan est diagnostiqué comme une découverte incomplète et bloque le démarrage. Les limites de profondeur, nombre de répertoires visités et nombre de candidats sont bornées et configurables ; valeurs effectives : profondeur 8 (racine = 0), 10 000 répertoires, 1 024 candidats, et 100 000 entrées de système de fichiers pour borner aussi les dossiers riches en fichiers. Configuration : `Modules:Discovery:MaximumDepth`, `MaximumDirectories`, `MaximumCandidates`, `MaximumEntries`. Un dépassement fait échouer le scan, sans résultat partiel.

Vérifier les manifestes et les hashes avant activation. Un candidat intègre mais incompatible avec le runtime complet est exclu, avec motif explicite, sans exécuter son assembly. Un manifeste invalide, fichier manquant/corrompu ou doublon ambigu de module/release fait échouer le démarrage ; ne pas masquer ces anomalies par une exclusion silencieuse. Plusieurs releases distinctes d’un module peuvent coexister ; aucune sélection automatique de la seule « dernière version ».

La découverte ne cherche pas les DLL arbitraires et ne constitue ni une isolation de sécurité ni une approbation du code trouvé. Le répertoire de déploiement contient exclusivement des artefacts approuvés ; les sauvegardes et staging sont conservés hors de la racine parcourue.

## Intégration et frontière avec L14

Fournir un mécanisme réutilisable dans Extensions et l’intégrer au SDK Hosting, au Kernel standard et aux hôtes personnalisés. Conserver l’API de chargement explicite pour les usages existants, notamment `ew dev`, ses générations et les vérifications CLI ciblées. La découverte ne change pas leurs identités ou leur cycle de vie.

Réutiliser la validation de compatibilité L12, les assemblies partagées, l’isolation, l’intégrité et la réconciliation durable. **À la sortie de L13 seule, la politique actuelle de rétention demeure** : les références des définitions publiées peuvent encore imposer le chargement d’anciennes versions. Un artefact requis absent ou exclu bloque le démarrage. L14 change cette politique ; ne pas annoncer cette capacité comme livrée par L13.

Aucune migration SQL nécessaire à la seule découverte. Mettre à jour configuration Kernel, exemples, scripts de qualification, guides de déploiement et documentation source ; le fallback garde les configurations actuelles utilisables.

## Critères de validation

- Racine configurée + liste contenant des chemins invalides : liste ignorée, seul le contenu de la racine est évalué.
- Racine invalide + liste valide : échec, aucun fallback silencieux.
- Racine absente de la configuration : comportement historique de la liste conservé.
- Racine vide, profondeur et volumes limites, ordre déterministe, jonctions/liens, accès refusé, arrêts de descente testés.
- Intégrité, incompatibilités, doublons, coexistence et références durables manquantes conservent leurs diagnostics.
- Qualification avec Kernel standard et hôte personnalisé sur Linux/macOS/Windows ; non-régression dev et packaging.

Tests légers automatisés pendant l’implémentation ; campagnes lourdes décrites dans une recette détaillée et exécutées par le porteur. Aucun résultat n’est acquis par cette spécification.

## API et diagnostics effectifs

`WorkflowModuleDiscovery.FindArtifacts(root, options)` découvre les chemins sans activer de code. `WorkflowModuleCatalog.Discover(root, configureModules, configureDiscovery)` valide tous les candidats, refuse les doublons avant filtrage, exclut les runtimes incompatibles puis active les éligibles. Les limites du manifeste et du loader restent celles de WorkflowModuleLoadOptions.

`WorkflowModuleSource.Load(configuration, contentRootPath, ...)` applique la priorité exclusive dans Hosting. `EnterpriseWorkflowApplicationBuilder.ConfigureDiscovery(...)` complète les options liées à la configuration. Les diagnostics peuvent être collectés par callback ; Hosting les journalise au démarrage via un logger typé.

| Code | Signification |
| --- | --- |
| EW7060_INVALID_DISCOVERY_ROOT | Racine vide, inexistante, non répertoire ou lien configuré comme racine |
| EW7061_DISCOVERY_SCAN_FAILED | Parcours incomplet, accès refusé ou répertoire devenu lien pendant le scan |
| EW7062_DISCOVERY_LIMIT_EXCEEDED | Limite de profondeur/volume atteinte, aucun résultat partiel |
| EW7063_DISCOVERY_LINK_SKIPPED | Lien/jonction rencontré et non suivi |
| EW7064_MODULE_DISCOVERY_SELECTED | Mode découverte ; liste individuelle ignorée |
| EW7065_EXPLICIT_MODULES_SELECTED | Absence de racine configurée ; mode explicite historique |
| EW7055_INCOMPATIBLE_RUNTIME | Candidat intègre exclu pour incompatibilité runtime |

Les codes existants de manifeste, hashes, doublons et readiness restent inchangés. Les artefacts doivent être immuables pendant leur découverte et chargement ; ce mécanisme ne crée pas de sandbox contre un acteur modifiant simultanément le système de fichiers.
