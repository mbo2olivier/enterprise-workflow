# Contexte Codex — Framework de workflows .NET réutilisable

Date : 30 septembre 2026. Langue de travail : français. Statut : document de cadrage et de démarrage, pas une spécification entièrement approuvée.

## 1. Mission de l’agent qui reprend le projet

Poursuivre la modélisation avec le porteur du projet, puis construire progressivement un framework open source de workflows en C#/.NET. Commencer par une tranche verticale durable et testable avant les outils graphiques. Préserver la distinction entre les exigences utilisateur, les propositions architecturales et les arbitrages encore ouverts.

Le présent livrable ne contient aucune implémentation. Les noms `Workflow.*`, interfaces, commandes et exemples ci-dessous sont des propositions ; ne pas les présenter comme une API existante. Le nom commercial, les identifiants NuGet et la licence restent à choisir.

### Provenance et niveau de certitude

Source : conversation « Conception plateforme workflows .NET », identifiant `6abd0386-3620-83e9-8a3a-ef1ed04414a3`. Le message initial de l’utilisateur a été récupéré intégralement. La longue réponse architecturale de l’assistant est accessible jusqu’au début de sa section sur les thèmes, puis tronquée. Les propositions sur les thèmes, la sécurité détaillée, l’observabilité, les conventions et le backlog sont donc des compléments de ce document, pas des décisions prétendument acceptées dans la conversation.

- **Exigence** : explicitement formulée par l’utilisateur.
- **Orientation proposée** : issue de la première réponse, sans validation explicite ultérieure.
- **Recommandation de démarrage** : ajout pour rendre le travail implémentable.
- **À arbitrer** : information manquante ou choix à confirmer.

Les noms d’entreprise, produits bancaires et SGBD cités à titre d’exemple dans la réponse précédente ne constituent pas une description confirmée de l’employeur ou de son infrastructure.

## 2. Vision produit et problème à résoudre

L’entreprise a plusieurs projets d’automatisation de processus. Une solution commerciale appelée « IPBPS New Gen » dans le message initial a été envisagée, mais son financement bloque. Le responsable de l’ingénierie souhaite une solution réutilisable, maintenable et open source, utilisable au bureau et dans d’autres contextes.

Le produit vise quatre ensembles cohérents :

1. **Engine / Runtime** : exécution durable de processus, reprise et attentes longues.
2. **SDK** : définition de workflows et développement d’extensions métier en C#.
3. **Studio** : concepteur graphique et générateur de code, réservé au développement.
4. **Kernel / Host** : application d’accueil des modules métier, authentification, autorisation, HTTP, UI et exploitation.

Principe directeur : **le noyau exécute les processus sans connaître le métier ; les modules décrivent le métier sans réimplémenter le moteur**.

Les utilisateurs sont les développeurs de workflows, les développeurs de nœuds réutilisables, les utilisateurs métier qui remplissent ou approuvent des tâches, les exploitants et le mainteneur du framework.

### Résultat attendu à terme

Un développeur installe les bibliothèques ou un modèle de projet, écrit son workflow ou le dessine dans le Studio, ajoute ses nœuds et formulaires, compile un module et livre ses DLL avec leurs dépendances. Après installation et redémarrage du Kernel, les processus autorisés apparaissent automatiquement à l’accueil. Les instances survivent aux redémarrages.

## 3. Contraintes et limites de l’environnement

### Exigences utilisateur

- Respecter la stack en place et les politiques du groupe ; leur contenu exact reste à obtenir.
- Utiliser C#/.NET, avec préférence pour une version LTS ou une compatibilité Standard si elle est justifiée.
- Ne pas imposer de transformation majeure de l’infrastructure : conteneurisation, nouveaux runtimes ou services d’exploitation ne doivent pas être des prérequis gratuits.
- Distribuer une ou plusieurs bibliothèques intégrables à un projet.
- Proposer une définition visuelle, une configuration ou une syntaxe fluent ; permettre les nœuds métier en code.
- Permettre l’usage sans Studio et l’export du modèle graphique vers une classe C# fluent.
- Offrir une persistance indépendante du SGBD via l’intégration à un `DbContext`.
- Fournir des pages et formulaires avec un design minimal personnalisable : logo, titre, couleurs, thèmes ou extension équivalente.
- Offrir une bonne expérience de scaffolding et de lancement du Studio, éventuellement par un outil `dotnet`.
- Charger des modules DLL dans un noyau ; accepter le redémarrage pour leur découverte.

### Hypothèses de travail à vérifier

La présence d’un serveur Windows, IIS, Linux, Docker, Active Directory, d’un SGBD donné ou d’un fournisseur d’identité particulier n’est pas confirmée. Ne pas reprendre l’exemple Docker de la première réponse comme une obligation.

Recommandation : cibler **.NET 10 LTS** pour le nouveau Host et le Runtime si l’infrastructure et les providers EF l’autorisent. La politique officielle consultée indique une prise en charge jusqu’en novembre 2028 ; .NET 8 arrive en fin de support en novembre 2026. Vérifier à nouveau la politique et les versions de providers au démarrage réel. [Politique .NET](https://dotnet.microsoft.com/en-us/platform/support/policy).

`.NET Standard` est un contrat de compatibilité de bibliothèques, pas un runtime pour le Host. Ne pas promettre la compatibilité .NET Framework ou multiplier les cibles sans consommateur identifié. Un déploiement autonome peut éviter l’installation séparée du runtime, mais implique toujours sa distribution et ses mises à jour ; il doit être approuvé par les règles d’exploitation.

## 4. Principes de conception

- Respecter SOLID, particulièrement l’ouverture à l’extension et la stabilité des contrats.
- Garder des points d’extension explicites ; ne pas rendre chaque détail interne configurable.
- Séparer définition, validation, exécution, persistance, intégration HTTP et présentation.
- Le moteur consomme une représentation intermédiaire, jamais le DSL ou l’UI directement.
- Aucune dépendance EF Core, ASP.NET Core ou Blazor dans le domaine pur.
- Préférer composition, injection de dépendances et petits contrats aux hiérarchies profondes.
- Publier uniquement les API nécessaires ; conserver les détails d’ordonnancement internes.
- Concevoir la durabilité avant le dessin des workflows.
- Distinguer erreur métier, erreur transitoire, erreur permanente et conflit de concurrence.
- Ne pas promettre « exactly once » pour les effets externes.
- Ne pas ajouter un broker, Redis, Kubernetes, un serveur Java ou Node.js obligatoire pour exécuter le produit.
- Documenter les décisions significatives par des ADR courts avec contexte, options et conséquence.

## 5. Architecture et modes d’intégration

```text
C# fluent ─────────────┐
Studio → modèle ──────┼→ définition canonique → validation → registre versionné
Configuration future ┘                                  │
                                                         ▼
                                                  Runtime durable
                                                   │          │
                                           contrats de nœuds   persistance
                                                   │          │
                                              modules DLL   adaptateur EF
                                                              │
Host / Kernel → HTTP, identité, UI, worker, plugins          DbContext
                                                              │
                                                      SGBD de l’entreprise
```

Deux modes utilisent le même Runtime :

- **Embedded** : une application existante référence les packages et compose elle-même son hébergement et son identité.
- **Kernel** : le Host fourni compose les briques communes et charge les modules déployés.

Le mode Kernel est prioritaire pour la démonstration. Éviter de dupliquer la logique entre les deux modes.

### Structure cible des projets

Ne pas créer immédiatement tous les projets vides. Commencer par ceux nécessaires à la première tranche.

```text
Workflow.sln
src/
  Workflow.Abstractions/
  Workflow.Core/
  Workflow.Sdk/
  Workflow.Persistence.Abstractions/
  Workflow.Persistence.EntityFramework/
  Workflow.Runtime/
  Workflow.AspNetCore/
  Workflow.PluginSystem/
  Workflow.UI/
  Workflow.Studio/
  Workflow.Observability/
  Workflow.Host/
tools/
  Workflow.Cli/
templates/
  Workflow.Module.Template/
  Workflow.Node.Template/
samples/
  LeaveRequest.Module/
  EmbeddedHost/
tests/
  Workflow.Core.Tests/
  Workflow.Runtime.Tests/
  Workflow.Persistence.ContractTests/
  Workflow.Persistence.EntityFramework.Tests/
  Workflow.PluginSystem.Tests/
  Workflow.EndToEnd.Tests/
docs/
  adr/
  architecture/
  authoring/
  operations/
```

| Projet | Responsabilité et dépendances autorisées |
| --- | --- |
| Abstractions | Identifiants, contrats de nœuds et résultats, contrats de construction minimaux ; sans framework web ou ORM. |
| Core | Définition canonique, invariants, diagnostics, validation ; dépend d’Abstractions. |
| Sdk | Builders fluent, ergonomie développeur, descriptions de nœuds ; dépend de Core et Abstractions. |
| Persistence.Abstractions | Opérations atomiques, snapshots et contrats de stockage ; dépend de types purs, jamais du Runtime concret. |
| Runtime | Ordonnancement et transitions ; dépend de Core et des contrats de persistance. |
| Persistence.EntityFramework | Implémentation du stockage et mappings EF ; dépend des contrats de stockage. |
| AspNetCore | Enregistrement DI, API, politiques et hébergement du worker ; aucune règle métier spécifique. |
| PluginSystem | Manifestes, résolution, compatibilité et composition de modules ; n’exige pas l’UI. |
| UI | Composants, pages métier, formulaires et thèmes ; passe par les services applicatifs. |
| Studio | Édition visuelle, diagnostics et export ; absent de la composition de production. |
| Observability | Instrumentation et intégrations optionnelles ; éviter une dépendance circulaire avec le Runtime. |
| Host | Racine de composition et exécutable, sélection des adaptateurs et paramètres. |
| Cli | Scaffolding, validation, Studio et packaging ; ne réimplémente ni validation ni exécution. |

Les contrats nécessitant `IServiceCollection`, ASP.NET ou Blazor appartiennent aux intégrations correspondantes. Ne pas alourdir Abstractions pour y faire entrer une interface de module universelle.

## 6. Modèle de définition et SDK

### Représentation intermédiaire

Recommandation : utiliser des noms distincts `WorkflowDefinitionModel` pour le graphe et `WorkflowDefinitionBuilder` ou `IWorkflowDefinition` pour son auteur. Éviter la collision entre la classe de base DSL et le record de définition de la première proposition.

Une définition contient au minimum :

- `DefinitionId`, version immuable, version de schéma et empreinte du contenu ;
- nom, description, catégorie, métadonnées d’accueil et politique de démarrage ;
- nœuds avec identifiants stables, type enregistré, version de contrat et configuration ;
- transitions explicites avec origine, destination et issue ou condition ;
- schémas de données, références de formulaires et règles d’affectation nécessaires.

Utiliser des valeurs JSON documentées ou des DTO typés aux frontières persistées. Éviter un `Dictionary<string, object>` arbitraire comme format durable public. Ne pas persister delegates, instances de services, noms de types CLR arbitraires ou graphes d’objets exécutables.

Les décisions référencent un évaluateur identifié et versionné, ou une expression dans un langage restreint à définir. Le MVP peut utiliser des évaluateurs C# enregistrés produisant une issue nommée. Ne pas essayer de sérialiser une lambda C# libre ni de la reconstruire depuis le Studio.

### Validation avant publication

Vérifier unicité des identifiants, présence des points d’entrée et sortie, références et types enregistrés, nœuds atteignables, sorties attendues, branche par défaut, schémas et formulaires, compatibilité des versions. Interdire les constructions non supportées avec un diagnostic localisable ; ne pas les ignorer.

MVP : graphe séquentiel avec branchements exclusifs, sans cycles ni parallélisme. Les workflows plus riches viendront avec des sémantiques explicites de boucle, jetons et jointure.

### Exemple d’intention — API à concevoir, non compilable en l’état

```csharp
workflow.Define("leave-request", version: 1)
    .Start("start")
    .HumanTask("submit", form: "leave-request-form", assignment: "initiator")
    .HumanTask("approval", form: "manager-approval-form", assignment: "manager")
    .Decision("decision", evaluator: "approval-outcome")
    .On("approved").GoTo("notify-approved")
    .On("rejected").GoTo("notify-rejected");
```

La syntaxe finale devra exprimer sans ambiguïté les branches et leurs fins. Les génériques peuvent améliorer le typage au développement, mais les références durables doivent rester stables.

### Nœuds personnalisés

Un nœud reçoit un contexte d’exécution borné, des entrées validées et un `CancellationToken`. Il obtient ses services métier par DI et retourne un résultat explicite : réussite et issue, attente durable ou échec qualifié. Il ne manipule pas directement les tables du moteur et n’effectue pas une attente de plusieurs heures en mémoire.

Séparer description du nœud, éditeur de configuration et exécuteur. Les métadonnées alimentent le catalogue du Studio ; un nœud sans éditeur dédié reste déclarable en code.

## 7. Modèle runtime et garanties

### Entités et identifiants

| Élément | Données essentielles |
| --- | --- |
| Définition publiée | Identité, version, empreinte, schéma, module et artefact requis. |
| Instance | ID, définition/version, statut, initiateur, clé métier, corrélation, état sérialisé, dates UTC, révision de concurrence. |
| Exécution de nœud | ID propre, instance, nœud, tentative, statut, entrées/sorties bornées, erreur, dates. |
| Travail planifié | ID, échéance, état, propriétaire du bail, expiration, jeton de verrouillage logique. |
| Tâche humaine | ID, instance/nœud, affectation, statut, formulaire/version, décision, acteur et révision. |
| Événement reçu | ID de déduplication, type, corrélation, payload, réception et consommation. |
| Timer | ID, échéance UTC, état, rattachement au travail à reprendre. |
| Audit | Action, acteur, ressource, corrélation et date ; écrit avec la mutation pertinente. |
| Outbox | Message durable à livrer, tentatives, état et clé d’idempotence. |

`WorkflowExecution` distinct d’Instance est optionnel : ne l’ajouter que si une sémantique de run ou de reprise le justifie. Éviter un simple `CurrentNodeId` comme seul état si le parallélisme est envisagé plus tard.

### États proposés

Instance : `Created → Running → Waiting → Running → Completed`, avec sorties `Failed` et `Cancelled`. `Suspended` reste une extension future. Une demande d’annulation n’est pas automatiquement une annulation achevée.

Nœud : `Pending`, `Running`, `Waiting`, `Succeeded`, `Failed`, `Cancelled`. Tâche humaine : `Open`, éventuellement `Claimed`, `Completed`, `Cancelled`.

Documenter la table des transitions permises et leurs préconditions avant l’implémentation du Runtime.

### Boucle d’exécution durable

1. La commande de démarrage valide les droits et la définition, puis persiste l’instance et son premier travail dans une même transaction.
2. Un worker réclame atomiquement un travail arrivé à échéance avec un bail et un jeton de possession.
3. Il charge l’état et la version exacte, ouvre un scope DI court et exécute le nœud hors d’une longue transaction SQL.
4. Une transaction atomique vérifie possession/révision, sauvegarde le résultat et programme la suite, l’attente et l’audit/outbox nécessaires.
5. Une attente humaine ou temporelle libère le worker. Une commande ultérieure persiste le réveil ; elle ne réveille pas seulement un objet en mémoire.
6. Après incident, les travaux dont le bail a expiré peuvent être repris. Un worker dont le bail est périmé ne peut plus valider sa transition.

Un `BackgroundService` avec polling SQL suffit au départ. Un canal mémoire peut accélérer le réveil, mais la base reste la source de vérité. Ne conserver ni `DbContext` ni transaction pendant un appel externe ou une attente humaine.

### Incidents, concurrence et effets externes

- Garantir des transitions persistées atomiques et une reprise **au moins une fois** des travaux éligibles.
- Un crash après un effet externe mais avant son enregistrement peut entraîner une nouvelle exécution. Utiliser une clé d’idempotence stable par opération logique, réutilisée lors des retries.
- L’outbox assure l’enregistrement atomique de l’intention de livraison, pas l’unicité de traitement chez le destinataire.
- Limiter les retries transitoires avec délai progressif, jitter et plafond ; rendre les erreurs permanentes visibles sans boucle infinie.
- La décision et la clôture d’une tâche humaine ainsi que la programmation de sa suite doivent être atomiques. Une seconde soumission renvoie le résultat connu ou un conflit explicite, sans seconde transition.
- Les commandes de démarrage et événements acceptent une clé de déduplication avec portée et durée documentées.
- Prévoir le rejet des événements sans abonnement pour le MVP, avec retour explicite permettant à l’émetteur de réessayer ; le buffering précoce nécessite un arbitrage distinct.
- Pour un timer, l’échéance est un minimum de déclenchement, pas une garantie temps réel.
- Une annulation empêche la programmation de nouvelles étapes ; elle ne peut annuler rétroactivement un effet externe déjà réalisé.
- Pas de compensation automatique ou de transaction distribuée dans le MVP.

### Versionnement et instances en cours

Une instance reste liée à la version et à l’artefact de définition utilisés au démarrage. Interdire de republier un contenu différent sous le même identifiant/version. Une empreinte du graphe seule ne détecte pas une modification du code d’un nœud : tracer aussi la version ou l’empreinte du module.

Conserver les anciennes définitions **et le code nécessaire à leur exécution** tant que des instances les utilisent. Le chargement côte à côte de versions de modules doit être testé ; sinon bloquer un remplacement incompatible et imposer le drainage des instances. Ne pas continuer silencieusement une ancienne instance avec le nouveau code.

## 8. Persistance et intégration EF Core

L’exigence `DbContext` est satisfaite par un adaptateur, pas par une dépendance du Core vers EF. Fournir deux compositions : contexte dédié au Kernel et mappings intégrables au contexte de l’application embarquée.

Exemple indicatif :

```csharp
services.AddWorkflowEngine()
    .AddEntityFrameworkPersistence<ApplicationDbContext>();

// Dans OnModelCreating :
modelBuilder.AddWorkflowEngine();
```

Ces noms d’extension sont à implémenter. Le package ne choisit pas implicitement le provider ni la chaîne de connexion.

### Contrat de stockage

Un simple `Find/Save/GetPending` n’est pas suffisant pour exprimer les garanties. Concevoir des opérations telles que création idempotente, claim atomique avec bail, commit conditionnel de transition, clôture de tâche et réveil atomiques, enregistrement d’événement dédupliqué. Les types et noms exacts restent à définir.

Définir les erreurs de conflit et les invariants observables dans une suite de tests de conformité réutilisable par chaque adaptateur. Un stockage mémoire est utile aux tests unitaires, jamais une preuve de durabilité.

### Portabilité et migrations

- Choisir un premier provider de production après confirmation du SGBD réel.
- SQLite peut servir à un exemple local, mais ne valide pas les garanties de concurrence d’un autre provider.
- Chaque provider revendiqué doit réussir les mêmes tests d’atomicité, concurrence, reprise et migration.
- Ne pas imposer le type SQL Server `rowversion` comme contrat universel ; exposer une révision abstraite et une mise à jour conditionnelle portable.
- Vérifier types JSON/texte, précision temporelle, tailles d’index, collation, transactions et primitives de claim.
- Documenter la propriété des migrations : application pour son contexte intégré, Host pour son contexte dédié. Livrer une procédure explicite ; pas de migration destructive automatique au boot.
- Ne pas supposer qu’un changement d’état moteur et une écriture métier dans un autre contexte sont atomiques.
- Prévoir limites de payload et conservation des historiques. Les pièces jointes futures seront des références via une abstraction dédiée.

Les providers EF doivent être compatibles avec la version majeure utilisée ; l’indépendance du modèle ne garantit pas une portabilité opérationnelle automatique. [Documentation des providers EF Core](https://learn.microsoft.com/en-us/ef/core/providers/).

## 9. Modules DLL, chargement et déploiement

### Contrat fonctionnel

Un module déclare identité, version, workflows, nœuds, formulaires, métadonnées, permissions requises et services métier. Il n’a pas accès au stockage interne du moteur par son API publique. Il peut utiliser ses propres repositories métier.

Définir un contrat de déclaration pur dans Abstractions et un adaptateur d’enregistrement DI dans l’intégration appropriée. Les endpoints métier optionnels utilisent un contrat ASP.NET distinct.

### Artefact proposé

```text
plugins/
  LeaveRequest/
    1.0.0/
      module.json
      LeaveRequest.Module.dll
      LeaveRequest.Module.deps.json
      [dépendances privées et ressources publiées]
```

Manifeste proposé : `moduleId`, `moduleVersion`, `entryAssembly`, plage de compatibilité du contrat Kernel, liste ou découverte des workflows, références de ressources. Une DLL isolée n’est pas toujours suffisante ; l’outil de packaging doit livrer l’ensemble nécessaire.

### Séquence de démarrage

1. Lire configuration et manifestes autorisés ; résoudre les chemins sous le répertoire de plugins.
2. Vérifier doublons, compatibilité et dépendances avant publication des workflows.
3. Charger via `AssemblyLoadContext` et `AssemblyDependencyResolver` ; partager les assemblies de contrats avec le Host pour préserver l’identité des types.
4. Collecter les déclarations et enregistrer les services avant `builder.Build()`.
5. Construire l’application, exposer les endpoints autorisés et valider les définitions.
6. Publier les versions valides et alimenter le catalogue filtré de l’accueil.
7. Démarrer les workers après validation des prérequis.

Recommandation MVP : échec de démarrage avec diagnostic précis pour un module configuré incompatible, plutôt qu’un catalogue partiellement fonctionnel. Une quarantaine optionnelle est un futur choix explicite.

Les plugins s’exécutent avec les privilèges du processus : un contexte de chargement n’est pas un bac à sable de sécurité. Ne charger que du code approuvé. Aucun téléversement de DLL par un utilisateur métier, aucune compilation C# distante et aucun hot reload en production.

Valider très tôt la découverte des composants Razor et la livraison de leurs assets depuis un plugin externe ; ne pas présumer qu’une DLL suffit à exposer automatiquement ses ressources web.

## 10. Expérience développeur et CLI

Le développeur doit pouvoir tout réaliser sans Studio. Préférer un outil local épinglé par manifeste pour la reproductibilité ; l’installation globale reste possible.

Commandes envisagées, non existantes :

```text
workflow init
workflow new module LeaveRequest
workflow new flow LeaveRequest
workflow new node NotifyEmployee
workflow validate --project <projet>
workflow studio --project <projet>
workflow export <modele> --output <fichier.cs>
workflow build
workflow pack --output <dossier>
```

Choisir ultérieurement entre ce nom et une commande de forme `dotnet workflow`, selon disponibilité du nom de package et ergonomie.

- `init/new` produit une solution minimale, un exemple et les références compatibles.
- `validate` réutilise le validateur du Core ; diagnostics avec code, sévérité, nœud et correction possible, sortie machine optionnelle et code retour non nul en cas d’erreur.
- `studio` lance un serveur .NET local sur loopback avec port configurable.
- `export` produit un code stable, correctement échappé et compilable ; ne remplace pas un fichier existant sans option explicite.
- `build` peut rester un simple usage de `dotnet build` tant qu’une commande dédiée n’apporte rien.
- `pack` produit manifeste, assembly, dépendances et ressources avec validation de compatibilité.

Un projet chargé pour valider ses définitions peut exécuter du code de build ou du code C# : la CLI travaille sur des projets locaux de confiance.

## 11. Studio : périmètre et source de vérité

Exigence : interface graphique proche de l’ergonomie n8n, réservée aux développeurs, capable d’exporter une classe C# fluent.

Orientation : ASP.NET Core et composants Blazor/Razor pour servir des assets compilés sans runtime Node.js imposé au client ou en production. La bibliothèque de graphe reste à évaluer : interactions, accessibilité, licence et maintenance. Un outillage JavaScript à la construction du framework est un choix distinct à confirmer.

Périmètre initial : palette de nœuds connus, canvas, connexions, panneau de propriétés, validation, sauvegarde d’un brouillon sérialisé, aperçu et export C#.

**Autorité des données** : le brouillon du Studio est modifiable visuellement ; après export et personnalisation manuelle, le C# est la source maintenue par le développeur. L’export constitue un transfert explicite. Pas de synchronisation automatique entre ces deux branches.

L’exécution du builder C# peut produire une définition inspectable, mais ne permet pas de reconstituer ni de réécrire arbitrairement son code source. Le round-trip C# libre → graphique → C# n’appartient pas au MVP.

Studio activé explicitement en développement, exclu des routes et de la composition de production ; absence vérifiée par test. Une éventuelle prévisualisation d’exécution utilise des services simulés et n’appelle pas les intégrations métier par défaut.

## 12. UI métier, formulaires et thèmes

L’UI de production est distincte du Studio. Elle fournit progressivement : accueil des processus autorisés, démarrage, boîte de tâches, formulaire, détail d’instance et historique ; un dashboard d’exploitation vient ensuite.

### Formulaires

MVP : champs texte, texte long, date, nombre, booléen et choix, labels, requis, messages et mise en page simple. Validation serveur obligatoire, avec assistance côté client. Le formulaire et son modèle sont versionnés pour les instances en cours.

Une sortie vers un composant Razor personnalisé permet les cas complexes. Les composants UI résident dans des contrats/packages d’intégration UI, sans faire dépendre Core de Blazor. Les validations métier et permissions restent dans les services applicatifs.

Pièces jointes, éditeur visuel de formulaires et règles conditionnelles avancées sont différés.

### Thèmes

Proposition : tokens CSS pour couleurs, typographie, espaces, rayon et densité ; options de marque pour logo, titre et favicon ; points de remplacement de composants limités. Éviter un fork des pages pour changer une couleur.

Fournir navigation clavier, focus visible, labels associés, états d’erreur compréhensibles, contraste vérifié et affichage responsive. Externaliser les textes et prévoir le français ; conserver les dates en UTC et appliquer le fuseau à la présentation. La personnalisation ne doit pas modifier les règles d’autorisation.

## 13. Authentification et autorisation

Exigence : le Kernel gère ces préoccupations communes. Le fournisseur exact n’est pas connu ; ne pas imposer un serveur d’identité ou une base de comptes supplémentaire.

Recommandation : intégrer l’authentification ASP.NET Core et les politiques du Host, avec configuration vers le fournisseur approuvé. Un mode local simulé est réservé au développement et refuse de s’activer en production.

Permissions minimales : voir un processus, démarrer une instance, lire une instance, consulter une tâche, réclamer/réaliser une tâche, annuler une instance et consulter l’audit d’exploitation.

- Contrôler les droits côté serveur sur chaque commande et lecture sensible.
- Filtrer l’accueil et l’inbox selon les droits ; masquer un bouton ne suffit pas.
- Vérifier affectation, groupe ou identité courante au moment de la soumission, pas seulement à l’ouverture du formulaire.
- Distinguer initiateur, acteur de la tâche et identité technique d’exécution.
- Définir un service d’affectation extensible pour les règles « responsable de l’initiateur » sans dépendance directe à un annuaire dans Core.
- Utiliser protection anti-CSRF pour les sessions navigateur, validation des entrées et limites de taille.
- Injecter les secrets depuis la configuration approuvée ; ne pas les enregistrer dans les définitions ou l’historique.
- Les endpoints d’un module doivent appliquer les politiques du Host ; prévoir un mécanisme d’enregistrement qui rend la protection explicite.

Multi-tenancy et séparation maker/checker ne sont pas des exigences confirmées. Si nécessaires, les définir avant de figer les clés et les contrôles d’accès.

## 14. Observabilité et exploitation

Utiliser les abstractions standard de logs, traces et métriques .NET ; exporter par une intégration OpenTelemetry optionnelle. Aucun backend de monitoring supplémentaire ne doit devenir obligatoire.

- Logs structurés : module/version, définition/version, instance, exécution, nœud, tentative, corrélation et code d’erreur.
- Traces : démarrage, exécution, persistance et livraison externe ; relier les reprises sans garder un span ouvert plusieurs jours.
- Métriques : profondeur et âge de file, durées, succès/échecs, retries, conflits, baux expirés, tâches en attente et latence des timers.
- Éviter les identifiants d’instances comme labels de métriques à forte cardinalité.
- Health checks : distinguer processus vivant et capacité à accéder au stockage et au registre requis.
- Audit fonctionnel : indépendant des logs techniques, persisté avec les mutations critiques, sans prétendre à une inviolabilité non implémentée.
- Masquer secrets et données sensibles par défaut ; les payloads complets ne vont pas dans les logs.

Documenter installation, configuration, migrations, sauvegarde/restauration, mise à jour des modules, conservation des anciennes versions, diagnostic et reprise contrôlée. Une sauvegarde exploitable inclut la base et les artefacts de modules nécessaires. Le dashboard n’autorise pas à modifier arbitrairement l’état persistant.

## 15. Périmètre MVP et phases

| Phase | Livrable | Critère de sortie |
| --- | --- | --- |
| 0 — Cadrage | Contraintes vérifiées et ADR sur cible, stockage, durabilité et versions. | Hypothèses explicitement tracées ; aucune dépendance d’infrastructure ajoutée sans besoin. |
| 1 — Tranche durable | DSL minimal, validation, Start/Service/End, stockage EF et worker. | Une instance reprend après arrêt brutal ; transition concurrente protégée. |
| 2 — Attentes métier | HumanTask, décision exclusive, timer, autorisation et formulaire simple. | Approbation après redémarrage ; double soumission sans double progression. |
| 3 — Kernel modulaire | Module packagé, chargement, accueil et thème simple. | Ajout d’un module puis redémarrage sans changement du code Host. |
| 4 — DX | Modèles, validation CLI, packaging, exemple documenté. | Création et déploiement reproductibles depuis un checkout propre. |
| 5 — Studio initial | Graphe, propriétés, diagnostics et export C#. | Le code exporté compile et produit une définition équivalente validée. |

Le **MVP métier** correspond aux phases 0 à 4. Le **premier périmètre complet de la vision** inclut la phase 5 ; le Studio est différé pour sécuriser le moteur, pas supprimé du produit.

### Scénario de référence : demande de congé

Un utilisateur démarre une demande, saisit dates et motif, un responsable approuve ou refuse, puis une notification simulée est produite. Redémarrer le Host pendant l’attente et reprendre sur la même version. Déployer une nouvelle version, démarrer une nouvelle instance et vérifier que l’ancienne conserve son comportement. Les règles d’affectation de l’exemple sont simulées, sans supposer un annuaire d’entreprise.

## 16. Non-objectifs initiaux

- Remplacer immédiatement toute une suite BPM commerciale ou revendiquer une conformité BPMN 2.0.
- Designer destiné aux métiers non techniques, éditeur de formulaires complet ou marketplace de connecteurs.
- Hot reload/unload de DLL, sandbox de code non fiable, compilation de C# en production.
- Conversion bidirectionnelle de C# arbitraire et modèle graphique.
- Promesse de support universel des SGBD ou de .NET Framework.
- Orchestrateur distribué complet, autoscaling et infrastructure de messages obligatoire.
- Exactly-once des effets externes, rollback métier universel ou transactions distribuées.
- Parallélisme/jointures, sous-workflows, boucles, compensation et migration automatique d’instances dès la première tranche.
- Moteur de scripts libre, expression C# saisie par un utilisateur ou stockage d’objets CLR arbitraires.
- Multi-tenancy, signature électronique, pièces jointes avancées et conformité réglementaire revendiquée sans exigences.

Ces exclusions n’interdisent pas les extensions futures ; elles bornent les engagements initiaux.

## 17. Questions ouvertes et décisions attendues

| Question | Impact | Proposition pour progresser |
| --- | --- | --- |
| OS, mode d’hébergement, .NET autorisé et contraintes groupe ? | Cible et déploiement. | Confirmer avant d’arrêter le TFM de production ; .NET 10 pour un prototype si disponible. |
| SGBD, version et provider EF existants ? | Transactions, migrations, compatibilité. | Un seul provider de production testé en premier. |
| Fournisseur d’identité, claims et source des responsables ? | Login et affectation. | Adaptateur Host ; identités simulées seulement en local. |
| Volumes, durées, latence et nombre de workers ? | Polling, index et claims. | Une instance de Host initiale, garanties atomiques testées sans prétendre à une qualification multi-hôte. |
| Coexistence de versions de DLL ou drainage ? | Reprise des anciennes instances. | Tester la coexistence tôt ; bloquer les remplacements incompatibles tant qu’elle n’est pas prouvée. |
| Contexte partagé ou dédié ? | Propriété des migrations. | Contexte dédié pour le Kernel, intégration partagée documentée pour embedded. |
| Formulaires déclaratifs ou Razor d’abord ? | Coût UI et flexibilité. | Petit schéma déclaratif avec échappatoire Razor. |
| JSON/YAML auteur dans la V1 ? | Surface de validation. | JSON pour brouillons ; YAML et import public différés. |
| Événements précoces, ordre et corrélation ? | Risque de réveils perdus. | Reporter le nœud WaitForEvent jusqu’à contrat explicite. |
| Multi-tenancy, séparation des fonctions, rétention ? | Modèle de données et permissions. | Clarifier avant exposition de données réelles. |
| Nom, licence et gouvernance open source ? | Publication et contributions. | Utiliser des noms provisoires ; choisir la licence avant publication. |
| UI, bibliothèque de graphe et dépendances frontend ? | Distribution et maintenance. | Prototype technique ciblé, pas une décision implicite. |

Poser d’abord les questions sur stack, SGBD et identité. Continuer entre-temps sur les invariants, le modèle et les tests purs ; ne pas bloquer toute modélisation sur des choix cosmétiques.

## 18. Conventions de code et stratégie de tests

Recommandations à adapter aux conventions d’un dépôt existant :

- Code, identifiants et commentaires techniques en anglais ; documentation de cadrage et échanges en français.
- Nullable activé, analyseurs .NET, formatage par `.editorconfig`, versions centralisées et SDK épinglé dans `global.json` après validation de sa disponibilité.
- Version C# stable cohérente avec la cible, pas `preview` par défaut.
- API asynchrones suffixées `Async`, propagation de l’annulation, pas d’`async void` ni de fire-and-forget pour les travaux durables.
- `TimeProvider` injecté et `DateTimeOffset` en UTC pour les décisions temporelles testables.
- Types immuables pour définitions et résultats ; entités EF confinées à leur adaptateur si possible.
- Identifiants stables, erreurs structurées, pas d’exceptions avalées ou de données sensibles dans leurs messages publics.
- DI avec scopes courts par exécution ; aucun service scoped capturé par un singleton.
- JSON versionné et options de sérialisation explicites ; pas de désérialisation polymorphe de types arbitraires.
- Versions sémantiques des packages, des modules, des contrats de payload et des définitions traitées séparément.
- Documentation XML des contrats publics avec garanties, limites et idempotence attendue.
- Ne pas imposer CQRS, MediatR, event sourcing, repository générique ou génération de code sans besoin concret.

Tests prioritaires : invariants de graphe, transitions, crash/reprise, concurrence, idempotence, permissions, compatibilité des versions, chargement des dépendances et compilation des exports. Utiliser une vraie base pour les garanties transactionnelles ; le provider EF InMemory ne suffit pas.

Simuler explicitement les crashs avant/après claim, effet externe, commit et livraison. Vérifier qu’un bail expiré interdit le commit d’un ancien worker. Tester les refus d’accès inter-utilisateurs et la concurrence sur une tâche humaine.

CI minimale : restore, build, tests unitaires et intégration du provider supporté, vérification des références interdites, packaging et compilation de l’exemple. Ajouter la compatibilité d’API publique lorsque les premiers packages sont stabilisés.

## 19. Premier backlog d’implémentation recommandé

Les priorités ci-dessous sont ordonnées ; chaque item doit laisser le dépôt compilable et une preuve de fonctionnement.

| ID | Travail | Dépendances | Critère d’acceptation |
| --- | --- | --- | --- |
| B01 | Inspecter dépôt et contraintes ; rédiger ADR cible, stockage et durabilité. | Aucune | Décisions et inconnues séparées ; aucune hypothèse d’entreprise inventée. |
| B02 | Créer solution minimale Core/Abstractions/Sdk et tests. | B01 | Build propre, références unidirectionnelles, exemple de définition en code. |
| B03 | Modèle canonique et validateur du graphe séquentiel. | B02 | Rejet de doublons, références absentes, branches ambiguës et cycles non supportés. |
| B04 | DSL minimal produisant ce modèle. | B03 | Deux définitions équivalentes produisent un modèle normalisé identique. |
| B05 | États et contrats atomiques du store, avec tests de conformité. | B03 | Préconditions, conflits, idempotence et frontières transactionnelles spécifiés. |
| B06 | Adaptateur EF du provider choisi et migrations initiales. | B05 | Création, claim, commit conditionnel et reprise testés sur base réelle. |
| B07 | Worker et nœud ServiceTask simulé. | B04, B06 | Start → Service → End ; reprise après arrêt ; aucun commit par worker périmé. |
| B08 | Politique de retry et idempotence d’une intégration de démonstration. | B07 | Crash après effet simulé sans doublon métier ; retries bornés et échec lisible. |
| B09 | HumanTask, décision et commande de complétion sécurisée. | B07 | Attente durable ; acteur non autorisé refusé ; double soumission contrôlée. |
| B10 | Timer durable et annulation cohérente. | B07 | Reprise après échéance passée ; pas de progression après annulation validée. |
| B11 | Host et UI minimale avec formulaire, inbox et thème. | B09 | Parcours congé utilisable avec identités de développement distinctes. |
| B12 | Contrat de module, loader et packaging manuel initial. | B04, B07 | Nouveau module visible après redémarrage ; dépendance privée et conflit diagnostiqués. |
| B13 | Versionnement de modules et définitions. | B12 | Nouvelle version sans modification silencieuse des instances précédentes ; retrait incompatible bloqué. |
| B14 | CLI de scaffolding, validation et pack. | B12, B13 | Projet généré compilable et package chargeable depuis une copie propre. |
| B15 | Logs, audit, métriques et runbook de reprise. | Dès B06, finaliser après B13 | Corrélation complète sans secrets ; restauration documentée et testée. |
| B16 | Prototype Studio puis export C# minimal. | B04, B14 | Graphe sauvegardé, validé et exporté ; code compilé avec équivalence du modèle. |

### Première tranche à entreprendre

Exécuter B01 à B07 avant d’engager la construction complète de l’UI. Le résultat concret doit être un exemple en ligne de commande ou un Host minimal qui démarre une instance persistée, exécute un nœud simulé et reprend après interruption. La tranche suivante ajoute l’approbation humaine pour éprouver le besoin métier.

Ne pas réduire l’acceptation à « la solution compile ». Fournir commandes de reproduction, tests exécutés, limites connues et décisions restantes.

## 20. Instructions de reprise pour l’agent Codex

1. Lire ce fichier et les instructions du dépôt ; inspecter le code avant de proposer une restructuration.
2. Résumer brièvement les exigences fermes et les trois inconnues bloquant les choix d’environnement.
3. Vérifier SDK et provider disponibles sans installation ou mise à jour d’infrastructure implicite.
4. Documenter les hypothèses retenues et démarrer le backlog par les éléments indépendants des réponses.
5. Implémenter par tranches testables, avec validation proportionnée aux garanties revendiquées.
6. Faire remonter les incompatibilités réelles avec les politiques du groupe ; éviter les fonctionnalités annexes.
7. Garder ce document et les ADR cohérents avec les décisions prises ; ne jamais requalifier une suggestion ancienne en exigence utilisateur.
8. Ne pas modifier les références synchronisées sous `sources/` dans le projet ChatGPT d’origine. Utiliser un répertoire de travail approprié pour le futur dépôt.

## 21. Références

- Conversation source : `chatgpt-conversation://6abd0386-3620-83e9-8a3a-ef1ed04414a3`, récupérée partiellement pour la réponse de l’assistant, intégralement pour le besoin utilisateur.
- [Politique de support officielle .NET](https://dotnet.microsoft.com/en-us/platform/support/policy), consultée le 30 septembre 2026 pour la proposition de cible LTS.
- [Providers EF Core](https://learn.microsoft.com/en-us/ef/core/providers/), consultée le 30 septembre 2026 pour les contraintes de compatibilité des providers.

Le reste du document constitue une synthèse de la vision et des recommandations de conception, à valider par les ADR et les preuves de fonctionnement du projet.
