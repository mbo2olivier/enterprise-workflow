# L9 — CLI, templates et runtime de développement

Date : 20 octobre 2026. **Spécification retenue et implémentée localement ; qualification multiplateforme L9 restante.** Les noms publics des packages et templates restent provisoires. L’[ADR 0025](adr/0025-runtime-developpement-l9.md) consigne les décisions ; la distribution publique reste soumise à l’ADR 0010 et à L10.

## Objectif et périmètre

Un auteur doit pouvoir générer un module, explorer son rendu et exécuter ses parcours métier dans un navigateur avec `ew dev`, puis produire un artefact déployable. Aucun projet Kernel ni application embarquée à écrire pour cette boucle locale. L’hôte fourni par l’outil réutilise les bibliothèques runtime, application, sécurité, loader et UI ; il ne crée pas un second moteur.

L9 comprend templates, CLI, packaging reproductible, runtime de développement et exemple embedded distinct. L10 conserve qualification de distribution, publication effective des packages publics (bibliothèques/adaptateurs retenus, CLI et templates) sur NuGet.org, archives RID et runbooks ; L11 conserve le Studio. Les commandes ci-dessous sont implémentées ; voir le [guide CLI](l9-cli.md) et la [preuve locale](evidence/l9.md).

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

Les commandes CLI terminent en échec avec diagnostic et code non nul si leur opération échoue ; le code 1 signale les diagnostics CLI gérés. Un projet est détecté dans le répertoire courant ; si plusieurs projets sont candidats, demander `--project` sans choisir arbitrairement. `ew pack` partage validation et préparation d’artefact avec `ew dev`. Il n’inclut ni base locale, ni configuration de développement, ni identités, secrets ou sélecteur de test. Une éventuelle commande `ew install` est hors du premier incrément.

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

Le seed est idempotent par ID stable : un redémarrage ne duplique pas les utilisateurs et ne rétablit pas silencieusement un droit révoqué dans l’administration. Une modification du seed après initialisation doit être diagnostiquée et demander une réconciliation explicite ou une réinitialisation volontaire ; `ew dev --reset` archive l’ancien environnement puis réinitialise ; aucun rapprochement automatique des changements du seed. Les permissions inconnues sont refusées avant application partielle du seed. Les droits techniques d’administration locale sont distincts des permissions métier.

## Identités et interface de développement

Un sélecteur d’identité permet de passer entre demandeur, RH et manager. Il est propre à l’hôte de développement, lié à une session et soumis aux protections des mutations web ; il conserve les contrôles serveur d’habilitation, d’affectation, de séparation d’acteurs et d’idempotence. Aucune identité arbitraire ni élévation de droits métier ne découle du sélecteur. Aucun mot de passe partagé par défaut n’est nécessaire.

L’écoute est limitée à la boucle locale dans le premier incrément ; une URL distante est refusée. Les routes de sélection et le bootstrap de démonstration sont absents du Kernel de production et des artefacts métier. SMTP et connecteurs externes ne sont pas configurés par défaut. Les secrets éventuels passent par les mécanismes de configuration locaux appropriés et ne sont ni consignés ni packagés.

Réutiliser catalogue, démarrage, inbox, détail de tâche, suivi et administration L8, formulaires standard L8b et composants Razor du module. Un bandeau affiche « Développement », identité active, module et génération. Les tâches, timers et services sont exécutés par le véritable runtime ; aucune simulation de succès métier.

## Surveillance, versions et persistance

La surveillance est active par défaut ; `--no-watch` la désactive. Une modification pertinente déclenche compilation et validation, puis arrêt contrôlé et redémarrage de l’hôte sur une génération immuable. Exclure `.ew/`, `bin/`, `obj/` et les sorties de packaging de la surveillance. Sérialiser les reconstructions pour éviter les courses. Aucun hot reload ni remplacement de DLL chargée n’est ajouté.

L’arrêt et le redémarrage sans modification doivent conserver tâches, instances, droits et historique. Une nouvelle compilation ne remplace jamais un hash sous le même ID/release. Une modification d’artefact avec la même release produit un diagnostic demandant une nouvelle release ou une réinitialisation volontaire de l’espace local. L’outil n’invente pas de suffixe de version ni de révision de contrat. Tout processus redéclaré avec un nouvel artefact exige une nouvelle révision, puisque son identité canonique inclut cet artefact ; une ancienne révision se réutilise explicitement depuis son propriétaire exact.

Les générations correspondent aux artefacts exacts, pas à une autorisation de muter une release. Les générations antérieures encore référencées sont conservées et réconciliées conformément à l’ADR 0022. Les ressources UI historiques restent disponibles. Une génération rejetée n’est pas publiée ; l’outil affiche clairement si l’ancien hôte reste actif ou si le runtime est arrêté.

Aucune suppression automatique de base ou purge d’artefacts référencés. Une réinitialisation est une opération volontaire limitée à l’espace local possédé par l’outil ; `ew dev --reset` archive uniquement `.ew/dev`, refuse un stockage externe pour cette opération et ne supprime pas les données archivées. Le verrouillage des DLL Windows doit être pris en compte dans la stratégie de redémarrage.

## Qualification et critères de sortie

La [preuve L9](evidence/l9.md) distingue les scénarios acquis localement des qualifications encore à exécuter ; cette table conserve les exigences de sortie.

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

## Détails de livraison et choix techniques

- CLI sous forme d’outil .NET `ew`, packages et templates locaux `1.0.0` ; noms et versions publics à confirmer en L10.
- Schéma JSON versionné livré dans [schemas/ew.dev.schema.json](schemas/ew.dev.schema.json), avec SMTP explicite et `moduleDirectories` pour les propriétaires exacts externes.
- Seed conservé après initialisation ; modification refusée et réinitialisation explicite avec archivage via `--reset`.
- Générations immuables sous `.ew/dev/artifacts/<id>/<release>` ; validation du catalogue combiné avant activation. Erreur de compilation ou de version : ancien hôte conservé ; échec de démarrage après arrêt : runtime signalé arrêté.
- Packaging en répertoire ; archives de transport et publication NuGet.org restent en L10. Les dépendances natives/RID et satellites sont refusées par le format initial.

Le [guide CLI](l9-cli.md) expose les limites et commandes de qualification. Le support revendiqué reste limité aux preuves acquises ; aucune réussite CI L9 n’est présumée.

## Complément de séquencement — qualification intégrée L8b/L9

Le 20 octobre 2026, le porteur fournit la preuve Oracle 19 (13 tests réussis, 0 échec, 0 ignoré) et lève le gate d’entrée L9. Il reporte explicitement le parcours LeaveRequest complet et la qualification SMTP à la sortie L9 pour exercer le SDK et les nouvelles commandes ensemble. Ces preuves restent obligatoires avant clôture L9 ; le lot est désormais implémenté localement et sa qualification est suivie séparément.

Ajouter à la configuration du runtime de développement un bloc SMTP explicite couvrant les options `Notifications:Smtp` existantes (hôte, port, TLS, expéditeur, URL de base, pagination, timeout, adresses et références aux secrets si nécessaires). Le bloc `smtp` est livré dans le schéma JSON v1 ; aucun SMTP activé par défaut et aucun secret dans les packages. Cette capacité complète le schéma initial ; la sélection Oracle/LDAP pour `ew dev` reste différée.

Livrer une stack Docker Compose de capture SMTP et sa procédure : endpoint SMTP réel, consultation des messages, version d’image fixée, disponibilité, ports locaux et nettoyage. Elle reste facultative pour `ew dev` et sert à qualifier notifications, fan-out, retries et panne/reprise durant le parcours navigateur. Aucune livraison Internet ni capacité TLS/authentification de production n’est déduite de cette capture.

Critère supplémentaire de sortie : `ew pack` → `ew verify`, puis parcours LeaveRequest via `ew dev` avec SMTP capturé, scénarios de refus, reprise et anciennes versions ; chargement distinct de l’artefact packagé dans le Kernel. Voir la [procédure intégrée](l8b-validation.md) et le [plan](implementation-plan.md). Les templates et packages ne sont figés qu’après ces preuves.
