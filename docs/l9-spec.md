# L9 — CLI, templates et runtime de développement

Date : 20 octobre 2026. **Spécification retenue à la demande du porteur ; non implémentée et non qualifiée.** Les noms publics des packages et templates restent provisoires. L’[ADR 0025](adr/0025-runtime-developpement-l9.md) consigne les décisions ; la distribution publique reste soumise à l’ADR 0010 et à L10.

## Objectif et périmètre

Un auteur doit pouvoir générer un module, explorer son rendu et exécuter ses parcours métier dans un navigateur avec `ew dev`, puis produire un artefact déployable. Aucun projet Kernel ni application embarquée à écrire pour cette boucle locale. L’hôte fourni par l’outil réutilise les bibliothèques runtime, application, sécurité, loader et UI ; il ne crée pas un second moteur.

L9 comprend templates, CLI, packaging reproductible, runtime de développement et exemple embedded distinct. L10 conserve qualification de distribution, publication effective des packages publics (bibliothèques/adaptateurs retenus, CLI et templates) sur NuGet.org, archives RID et runbooks ; L11 conserve le Studio. Les commandes ci-dessous décrivent le contrat cible, pas des commandes déjà disponibles.

## Parcours et commandes cibles

```bash
# Noms de templates provisoires ; aucune publication de packages présumée.
dotnet new ew-module --name Acme.LeaveRequest --module-id acme.leave-request
cd Acme.LeaveRequest
dotnet build --configuration Release
ew validate .
ew inspect .
ew inspect . --format json --output ./artifacts/resolved-module.json
ew dev
ew dev --config ./ew.dev.json
ew dev --project ./Acme.LeaveRequest.csproj --url http://localhost:5090
ew dev --no-watch
ew pack . --configuration Release --output ./artifacts
ew verify ./artifacts/acme.leave-request/1.0.0
```

| Commande | Responsabilité |
| --- | --- |
| `dotnet new ew-module` | Projet compilable reprenant les conventions L8b : métadonnées uniques, état explicite, processus, handlers, permissions et formulaires |
| `ew validate` | Compilation et validation des déclarations et bindings résolubles ; diagnostics processus/nœud/référence ; aucun remplacement des tests métier |
| `ew inspect` | Aperçu résolu lisible ou JSON : IDs, révisions, propriétaires exacts, permissions, affectations et transitions |
| `ew dev` | Compilation, artefact local immuable, SQLite, sécurité, worker et UI accessibles par URL |
| `ew pack` | Compilation, validation, assemblage des dépendances et ressources, manifeste et empreintes ; répertoire installable en premier format |
| `ew verify` | Contrôle hors projet source du manifeste, fichiers, empreintes et compatibilité vérifiable ; la composition et l’inventaire de la cible restent vérifiés au chargement |
| `dotnet new ew-embedded` | Exemple intégrant le même moteur dans une application .NET, avec composition et autorisation explicites |

Les commandes CLI terminent en échec avec diagnostic et code non nul si leur opération échoue ; les codes détaillés restent à définir. Un projet est détecté dans le répertoire courant ; si plusieurs projets sont candidats, demander `--project` sans choisir arbitrairement. `ew pack` partage validation et préparation d’artefact avec `ew dev`. Il n’inclut ni base locale, ni configuration de développement, ni identités, secrets ou sélecteur de test. Une éventuelle commande `ew install` est hors du premier incrément.

## Démarrage de `ew dev`

1. Résoudre projet, fichier de configuration et options ; vérifier leur validité avant mutation.
2. Compiler le module, valider les déclarations et préparer manifeste, dépendances et ressources via les mêmes règles que le packaging.
3. Préparer un espace `.ew/dev/` propre au projet, ignoré par Git et exclu du packaging.
4. Initialiser explicitement les bases SQLite locales nécessaires au runtime et à la sécurité, appliquer leurs migrations et réconcilier les artefacts. Cette orchestration appartient à l’hôte de développement ; le loader ne migre pas implicitement une base.
5. Initialiser les identités de test et leurs habilitations via les services existants, puis composer UI et worker.
6. Annoncer l’URL uniquement après readiness ; afficher module/release, génération, chemins de stockage et mode de surveillance.

```text
✓ Module compilé : acme.leave-request / 1.0.0
✓ Déclarations validées
✓ Artefact de développement préparé
✓ Stockage SQLite initialisé
✓ Module chargé
✓ Runtime démarré

Interface : http://localhost:5080
Espace local : .ew/dev/
Ctrl+C pour arrêter.
```

Une erreur de compilation, de manifeste, de configuration ou de compatibilité doit empêcher l’activation de la nouvelle génération et produire un diagnostic. Un port occupé est signalé avec invitation à utiliser `--url`. Ctrl+C arrête hôte et surveillance et libère les fichiers. Deux sessions ne doivent pas piloter simultanément le même espace local : la seconde échoue explicitement.

## Configuration JSON

`ew.dev.json` est facultatif, recherché à la racine du projet sélectionné. Priorité : options explicites > fichier > défauts. `--config` sélectionne un fichier obligatoire, avec erreur s’il est absent. Les chemins du fichier sont relatifs à son répertoire ; sans fichier, les défauts sont relatifs à la racine du projet. Un JSON invalide, une propriété inconnue ou un provider non pris en charge provoque une erreur explicite.

Schéma cible initial :

```json
{
  "server": { "url": "http://localhost:5080" },
  "storage": { "provider": "sqlite", "path": ".ew/dev/workflow.db" },
  "watch": true,
  "seed": {
    "users": [
      {
        "id": "alice",
        "displayName": "Alice — Demandeuse",
        "permissions": ["acme.leave-request.request"]
      },
      {
        "id": "bob",
        "displayName": "Bob — RH",
        "permissions": ["acme.leave-request.hr.review"]
      },
      {
        "id": "carol",
        "displayName": "Carol — Manager",
        "permissions": ["acme.leave-request.manager.approve"]
      }
    ]
  }
}
```

Le fichier runtime est `.ew/dev/workflow.db` par défaut ; les éventuels fichiers de sécurité et métadonnées restent dans le même espace et leurs chemins sont affichés. SQLite est le provider du premier incrément. YAML et sélection Oracle/LDAP sont des extensions différées, sans support implicite.

Sans configuration, fournir des identités de démonstration et un parcours explicite pour attribuer les permissions du module dans l’administration locale ; aucune attribution automatique de tous les droits métier. Le fichier d’exemple configure les trois acteurs du parcours LeaveRequest, sans accorder de lecture complète implicite. La permission de lecture doit être ajoutée explicitement pour tester le suivi intégral.

Le seed est idempotent par ID stable : un redémarrage ne duplique pas les utilisateurs et ne rétablit pas silencieusement un droit révoqué dans l’administration. Une modification du seed après initialisation doit être diagnostiquée et demander une réconciliation explicite ou une réinitialisation volontaire ; la syntaxe de ces opérations reste à définir. Les permissions inconnues sont refusées avant application partielle du seed. Les droits techniques d’administration locale sont distincts des permissions métier.

## Identités et interface de développement

Un sélecteur d’identité permet de passer entre demandeur, RH et manager. Il est propre à l’hôte de développement, lié à une session et soumis aux protections des mutations web ; il conserve les contrôles serveur d’habilitation, d’affectation, de séparation d’acteurs et d’idempotence. Aucune identité arbitraire ni élévation de droits métier ne découle du sélecteur. Aucun mot de passe partagé par défaut n’est nécessaire.

L’écoute est limitée à la boucle locale dans le premier incrément ; une URL distante est refusée. Les routes de sélection et le bootstrap de démonstration sont absents du Kernel de production et des artefacts métier. SMTP et connecteurs externes ne sont pas configurés par défaut. Les secrets éventuels passent par les mécanismes de configuration locaux appropriés et ne sont ni consignés ni packagés.

Réutiliser catalogue, démarrage, inbox, détail de tâche, suivi et administration L8, formulaires standard L8b et composants Razor du module. Un bandeau affiche « Développement », identité active, module et génération. Les tâches, timers et services sont exécutés par le véritable runtime ; aucune simulation de succès métier.

## Surveillance, versions et persistance

La surveillance est active par défaut ; `--no-watch` la désactive. Une modification pertinente déclenche compilation et validation, puis arrêt contrôlé et redémarrage de l’hôte sur une génération immuable. Exclure `.ew/`, `bin/`, `obj/` et les sorties de packaging de la surveillance. Sérialiser les reconstructions pour éviter les courses. Aucun hot reload ni remplacement de DLL chargée n’est ajouté.

L’arrêt et le redémarrage sans modification doivent conserver tâches, instances, droits et historique. Une nouvelle compilation ne remplace jamais un hash sous le même ID/release. Une modification d’artefact avec la même release produit un diagnostic demandant une nouvelle release ou une réinitialisation volontaire de l’espace local. L’outil n’invente pas de suffixe de version ni de révision de contrat. Des changements de définition peuvent aussi demander une nouvelle révision selon les règles existantes.

Les générations correspondent aux artefacts exacts, pas à une autorisation de muter une release. Les générations antérieures encore référencées sont conservées et réconciliées conformément à l’ADR 0022. Les ressources UI historiques restent disponibles. Une génération rejetée n’est pas publiée ; l’outil affiche clairement si l’ancien hôte reste actif ou si le runtime est arrêté.

Aucune suppression automatique de base ou purge d’artefacts référencés. Une réinitialisation est une opération volontaire limitée à l’espace local possédé par l’outil ; sa commande et sa protection contre la suppression d’un chemin externe restent à spécifier avant implémentation. Le verrouillage des DLL Windows doit être pris en compte dans la stratégie de redémarrage.

## Qualification et critères de sortie

Tous les scénarios sont à exécuter ; ce document ne fournit aucune preuve d’implémentation.

| Scénario | Résultat attendu |
| --- | --- |
| Copie propre → template → `ew dev` | URL utilisable sans projet Kernel, base ni manifeste préparés manuellement |
| LeaveRequest complet | Formulaire → confirmation → RH → manager désigné → service → fin ; branches de refus et commentaires validés |
| Changement d’identité | Permissions, séparation, révocation et appels directs contrôlés ; administration sans droit métier implicite |
| Formulaires et ressources | Rendu standard et Razor personnalisé, validation serveur et ressources hachées utilisables |
| Arrêt/reprise | Même instance et tâche après relance ; double soumission sans double progression |
| Surveillance | Reconstruction et redémarrage contrôlés ; exclusions de sorties ; erreur de compilation visible |
| Même release, hash modifié | Diagnostic explicite, aucun écrasement ; nouvelle release ou réinitialisation volontaire |
| Coexistence A/B | Anciennes instances et ressources servies par leur artefact exact ; retrait référencé bloqué |
| Configuration et seed | Défauts, priorité des options, chemins, ambiguïtés, JSON invalide, droits inconnus, seed idempotent et révocation conservée |
| Isolation locale | URL distante refusée ; port occupé et seconde session diagnostiqués ; sélecteur absent en production |
| `pack` puis `verify` puis Kernel | Artefact utilisable sans sources ni espace de développement ; aucun secret/seed/base locale embarqué |
| Exemple embedded | Même définition et même moteur ; autorisation et invariants conservés |

Rattacher ces preuves à T14, T16, T12, T18 et T20 de la stratégie de tests. Qualifier CLI, SQLite, arrêt/reprise et surveillance sur Linux/macOS/Windows ; inclure un parcours navigateur et documenter sa plateforme. La compatibilité annoncée suit uniquement les preuves acquises.

## Détails restant à finaliser avant implémentation

- Noms publics des packages/templates et mode d’installation de la CLI.
- Schéma JSON versionné et correspondance détaillée avec les services de configuration existants, dont l’administration locale.
- Commandes de réconciliation du seed et de réinitialisation locale.
- Organisation physique et rétention des générations ; stratégie de restauration de l’ancien hôte après échec.
- Codes de sortie détaillés, format transportable des archives et éventuelle extension de providers.

Ces points ne remettent pas en attente le périmètre confirmé de `ew dev` ; ils doivent être explicités dans la préparation technique L9 sans prétendre déjà disposer d’un runtime livré.
