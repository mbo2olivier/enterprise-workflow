# L9b — contrats auteur et exploitation

Implémentation locale en cours de qualification. Les décisions produit restent celles des ADR 0026/0027 et de la [spécification](l9b-spec.md). Ce guide décrit les API effectivement ajoutées ; les preuves et limites sont suivies séparément dans [evidence/l9b.md](evidence/l9b.md).

## État métier et formulaires

Tout processus doit déclarer `WithState<MyState>().InitializedBy<MyInitializer>().WithStateSchemaVersion(2)`. Conserver le builder typé retourné pour définir les étapes et leurs actions. Le DTO racine peut être vide ; ses propriétés, lorsqu’il en possède, doivent être publiques avec `get` et `set` ou `init`. `IWorkflowStateInitializer` transforme les entrées validées du formulaire initial en `WorkflowStateInitializationResult`. Ce résultat porte le JSON d’état et, facultativement, des références de médias générés. Le contexte contient l’acteur, la soumission et sa clé d’idempotence. L’initialiseur doit être déterministe pour cette entrée et sans effet externe ; les fichiers générés utilisent une clé d’opération stable. Il n’existe aucune copie automatique d’un champ de formulaire dans le state typé.

Le DTO utilise `JsonPropertyName` pour fixer les noms persistés, `Label` pour les libellés, `HiddenField` pour exclure un champ du détail et `VisibleFor` pour exiger au moins une permission du groupe. Répéter `VisibleFor` ajoute une conjonction ; `VisibleForAll` exige toutes les permissions indiquées. Les identifiants de permissions sont locaux au module dans la déclaration initiale et qualifiés par le builder. Tous les champs déclarés sont visibles par défaut aux lecteurs de l’instance, y compris les nouveaux champs : inspecter le contrat avant publication.

Types actuellement compilés : objets non cycliques, listes/tableaux, chaînes, booléens, nombres, entiers, `DateOnly`, `DateTimeOffset`, `Guid` et nullables. Les noms persistés sont ceux du DTO et de `JsonPropertyName`, sans convention camelCase implicite. Les types/conversions JSON non pris en charge sont refusés. Profondeur maximale 12, jusqu’à 128 propriétés par objet. Les champs non déclarés sont rejetés à la validation d’état.

`Required`, `StringLength`, `MinLength`, `MaxLength` et `Range` sont compilés en contraintes ; les bornes de longueur concernent actuellement les chaînes. `RequiredFor("approval")` ajoute une obligation contextuelle. Associer les groupes aux stages/actions par `.ValidateState("approval")`. Les contextes sont dérivés du serveur. Pour des règles croisées, annoter la classe `[StateValidator(typeof(MyValidator), "approval")]` et implémenter le contrat pur `IWorkflowStateValidator` ; sans groupe, le validateur s’applique à chaque validation d’état.

Le runtime valide le state initial, puis les résultats des handlers humains/services avant les commits concernés. Les handlers utilisent les résultats et mises à jour d’état existants ; un résultat sans mise à jour ne fusionne pas les entrées. Les formulaires typés ne sont pas préremplis automatiquement par égalité de noms avec le state. `.ShowFields(...)` référence les champs du state pour le résumé d’une tâche. Les anciens parcours d’auteur sans DTO ne sont plus proposés.

Les contrats initiaux sont sérialisés dans la configuration canonique de la nouvelle définition. Le catalogue refuse un processus d’auteur sans contrat DTO ou sans initialiseur. Les snapshots locaux issus des anciens prototypes ne sont pas un contrat de compatibilité à conserver avant la première publication ; utiliser une recette isolée neuve. Les validateurs et initialiseurs sont résolus depuis l’artefact exact du catalogue ; un validateur déclaré mais indisponible ne doit pas être ignoré.

## Lecture et mutation typées

`context.ReadState<TState>()` est disponible pour les services/décisions et complétions humaines ; `context.ReadSubmission<TInput>()` pour les complétions humaines et initialiseurs. `CanonicalJson.Deserialize<T>()` en est la primitive commune. Les noms JSON sont ceux des propriétés ou de `JsonPropertyName` : pas de convention camelCase implicite. Les propriétés JSON non mappées et les types incompatibles provoquent une `JsonException`, au lieu de perdre des données silencieusement.

```csharp
var input = context.ReadSubmission<ReviewInput>();
var state = context.ReadState<RequestState>();
state.Reference = input.Reference;
return HumanTaskCompletionResult.Success(
    stateUpdate: NodeStateUpdate.Replace(state));
```

Les lectures retournent des objets détachés ; modifier l’objet ne persiste rien. `NodeStateUpdate.Replace<T>(state)` sérialise un remplacement complet, pas un patch. Le runtime conserve la version de schéma courante et valide le state et les groupes de l’action avant son commit, avec la décision et les autres effets. Ne pas utiliser un DTO partiel pour remplacer l’état complet. Les primitives JSON restent le transport interne du moteur et de ses tests canoniques, pas une seconde façon d’écrire un processus sans DTO.

## Commentaires des approbations

```csharp
var flow = process.WithState<RequestState>().InitializedBy<RequestInitializer>();
flow.WithId("request").WithStateSchemaVersion(1)
    .RequiresPermission("request").StartWithForm("request-form").Then("review");
flow.Approval("review", "Validation", task => task
    .RequiresPermission("review").ClaimedByEligibleParticipant()
    .Actions(actions =>
    {
        actions.Add("approve", "Approuver", action =>
            action.CommentAt(state => state.Comments).Then("completed"));
        actions.Add("decline", "Refuser", action =>
            action.RequireCommentAt(state => state.Comments).Then("rejected"));
    }));
flow.End("completed").End("rejected");
```

Le DTO déclare par exemple `[JsonPropertyName("comments")] public List<WorkflowComment> Comments { get; init; } = [];` (`EnterpriseWorkflow.Abstractions`). Le handler intégré ajoute texte, étape, action, fournisseur/identifiant de l’auteur et date UTC serveur. Les métadonnées ne sont pas prises dans le formulaire. Un commentaire facultatif vide n’ajoute rien ; un commentaire requis vide est refusé. Le texte est borné à 4 000 caractères.

L’expression est compilée en chemin JSON, sans delegate persisté. Seuls les chemins de propriétés sérialisées vers une collection de `WorkflowComment` sont acceptés : pas d’index, de méthode, de capture ou de champ ignoré. Initialiser les objets parents d’un chemin imbriqué ; un parent absent/non objet est refusé. La validation contextuelle est appliquée après préparation du commentaire et avant commit. Un rejet ne conserve pas le commentaire ; un replay ne le duplique pas.

Les approbations utilisent le handler et le formulaire intégrés pour ce mapping. `HandledBy` reste destiné aux tâches de formulaire, pas aux nœuds d’approbation ; les services conservent leur handler métier. L’exemple LeaveRequest utilise un commentaire facultatif pour valider et obligatoire pour refuser, aux deux étapes RH/manager.

## Migrations explicites

La version du schéma reste obligatoire et ne dépend pas d’un hash calculé automatiquement. La clé de migration historique reste artefact/source/cible : les versions utilisées par plusieurs DTO d’un même module doivent être choisies en conséquence ; aucune migration par processus ni chaînage automatique n’est ajouté. Les migrations historiques `Add<TMigrator>(source, target)` restent disponibles pour les processus historiques. Pour le nouveau parcours, déclarer `Add<TMigrator, TTargetState>(source, target, validationGroups)` : le contrat cible et ses validateurs sont vérifiés avant commit. Une migration d’un state typé sans contrat cible est refusée. La sélection des contrats après migration repose sur les artefacts déjà référencés et les versions explicites ; aucune référence à un nouvel artefact n’est ajoutée silencieusement.

## Médias

Les politiques se déclarent **exclusivement dans le module**. Aucun processus, formulaire ou handler ne redéfinit formats, taille ou visibilité. La visibilité est obligatoire. Les permissions sont qualifiées par le module ; plusieurs appels `VisibleFor` sont conjoints, les permissions d’un même appel sont alternatives.

```csharp
builder.MediaPolicies(policies =>
{
    policies.Add("documents").Label("Documents du processus")
        .AllowFormats(MediaFormats.Pdf, MediaFormats.Text)
        .MaximumSize(10.Megabytes())
        .VisibleToInstanceReaders();
});
```

Les politiques du module sont matérialisées automatiquement dans les définitions publiées, et les contraintes de fichier dans les métadonnées des formulaires de l’artefact exact. Ce sont des copies techniques immuables, pas des niveaux de déclaration supplémentaires. Une instance ancienne conserve sa politique d’origine.

Champ de formulaire : `.File("documents", "Justificatifs").UseMediaPolicy("documents").MaximumFiles(3).Required()`. Seuls le caractère obligatoire et le nombre de fichiers restent propres au champ. Une référence inconnue, une visibilité omise ou une tentative de surcharge est refusée à la publication. L’ancienne méthode `.Files(...)` a été retirée. Les formulaires fichier sont déclarés dans le module du processus, avec leurs politiques et validateurs ; `AddForm` permet de les partager entre plusieurs processus du même module. L’import de formulaires fichier depuis un autre artefact est refusé pour éviter un changement implicite de validateur.

Dans un service ou une tâche humaine :

```csharp
var files = media.For(context);
await files.StoreAsync("summary", "synthese.txt", stream, "documents", cancellationToken);
return ServiceNodeResult.Success(); // Aucune liste media à retourner.
```

`For(context)` dérive propriétaire, instance, activation et politiques depuis l’exécution serveur. Le service utilise l’identité technique `system/workflow` ; une tâche humaine utilise son acteur authentifié. La clé d’opération reste explicite et stable entre les reprises. Une exécution possède une seule collecte, même si `For(context)` est appelé plusieurs fois ; elle est fermée lorsque le handler termine. Aucun contexte ambiant global n’est utilisé.

Pour les uploads d’un formulaire, `await files.FromFieldAsync("documents", cancellationToken)` retrouve et revalide les références du champ (propriétaire, soumission, étape, formats et visibilité), et les collecte pour rattachement. La façade fonctionne aussi dans `IWorkflowStateInitializer`, où l’instance n’existe pas encore. Placer ces références dans le state reste un mapping métier explicite, indépendant du formulaire. Pour un upload, la bibliothèque conserve le filtre supplémentaire de présence de la référence dans l’état visible.

`GetAsync(reference)`, `OpenAsync(reference)`, `ReplaceAsync(reference, key, name, content, policy, cancellationToken)` et `DeleteAsync(reference)` utilisent l’instance du contexte. Remplacer crée une révision immuable ; supprimer prépare un retrait logique de toutes les révisions. Les créations, remplacements et suppressions ne deviennent effectifs qu’avec le commit réussi du handler, après validation du state et des droits. Rejet, exception, ancien bail ou conflit de révision ne doivent pas publier ni supprimer de fichiers. Les baux, révisions et portées sont revérifiés dans la transaction source SQLite/Oracle. Les décisions restent sans écritures de médias.

Les binaires sont écrits en stockage privé avant le commit ; un échec peut laisser un dépôt temporaire invisible, repris par la maintenance. La collecte automatique ne garantit pas l’effacement physique immédiat. Les API bas niveau `StageAsync`, `GetForInstanceAsync`, `OpenForInstanceAsync`, `DeleteForInstanceAsync` servent aux adaptateurs de confiance du framework et ne doivent pas être exposées telles quelles à un navigateur. Les paramètres manuels `Success(media: ...)` et `WorkflowStateInitializationResult.Media` ont été retirés : la collecte du contexte est le parcours unique pour les handlers.

Formats intégrés : PDF, JPEG/PNG/TIFF classique, TXT et CSV UTF-8, RTF, DOCX/XLSX/PPTX et ODT/ODS/ODP. Les formats DOC/XLS/PPT, BigTIFF, archives génériques, documents chiffrés et variantes à macros ne sont pas intégrés. Les conteneurs Office/ODF sont reconnus par leurs parties, types et structures XML, sans extraction sur disque ni résolution externe ; DTD, chemins traversants, doublons d’entrées, macros, ActiveX et objets Office embarqués sont refusés. Limites : 2 048 entrées, 200 Mio décompressés au total, 100 Mio par entrée ; XML 32 Mio/profondeur 64 ; ratio supérieur à 100 refusé au-delà de 1 Mio par entrée ; TIFF au plus 64 répertoires. Ce sont des contrôles bornés de signature/structure, sans moteur bureautique ni certification complète des standards. Un document réel valide rejeté doit être signalé pendant la recette. Références de famille : [ECMA-376](https://ecma-international.org/publications-and-standards/standards/ecma-376/) et [ODF 1.3 Packages](https://docs.oasis-open.org/office/OpenDocument/v1.3/OpenDocument-v1.3-part2-packages.html).

Formats supplémentaires :

```csharp
builder.MediaFormats(formats => formats.Add<NoteValidator>("note")
    .ContentType("application/x-note").Extensions(".note"));
builder.MediaPolicies(policies => policies.Add("documents")
    .AllowFormats("pdf", "note").MaximumSize(10.Megabytes())
    .VisibleToInstanceReaders());
```

`NoteValidator` implémente `IWorkflowMediaFormatValidator.ValidateAsync(Stream, CancellationToken)` et retourne `bool` : faux, exception ou validateur absent bloque le dépôt. Le contenu fourni est en lecture seule. Respecter les limites et l’annulation dans un validateur personnalisé. Le framework résout ce type dans une portée DI via l’ID, la release et le hash exacts de l’artefact propriétaire ; aucune recherche de la « dernière version » ni repli permissif. Les identifiants/extensions intégrés ne peuvent pas être redéfinis. Les politiques utilisent soit l’enum `MediaFormats`, soit les identifiants texte (par exemple `"pdf", "note"` pour mélanger intégré et personnalisé).

Plafonds techniques : 100 Mio par fichier, huit fichiers par champ et 32 références rattachées par commit ; chaque formulaire peut imposer moins. Les installations doivent aussi dimensionner le volume et leur politique de rétention. Les implémentations `IWorkflowMediaScanner` sont appelées avant staging ; rejet ou exception bloque le dépôt. Aucun scanner antivirus n’est livré par défaut. Un `IWorkflowMediaInspector` facultatif peut retourner `Pending` : le média reste en quarantaine durable, non téléchargeable et non rattachable. L’intégration de confiance termine l’analyse par `CompleteInspectionAsync(reference, expectedSha256, approved, ...)`, liée au hash immuable ; aucun callback HTTP public générique n’est fourni. L’UI attend l’analyse avec un polling borné ; la façade signale l’analyse en attente : le runtime applique sa politique de retry aux services et refuse la complétion/démarrage avec un code explicite tant que le média reste en quarantaine. Une approbation rend le dépôt rattachable, un rejet le bloque.

La bibliothèque filtre les métadonnées et le téléchargement côté serveur. Un upload n’est montré que si sa référence apparaît dans l’état visible et si sa visibilité source le permet ; un document généré utilise sa politique explicite. La suppression est logique et retire aussi l’accès aux révisions antérieures ; le remplacement ne réécrit pas les octets historiques.

`services.AddEnterpriseWorkflowMedia(privateRoot)` active le même service dans un hôte embarqué ou sans UI ; l’intégration Web l’appelle automatiquement. Le module n’a donc pas à dépendre de Presentation pour générer des fichiers. `IWorkflowMediaStorageProvider` permet d’autres stockages. L’adaptateur disque crée des objets privés sans utiliser le nom utilisateur comme chemin. Kernel : configurer `Media:Root` sur un volume durable extérieur aux fichiers statiques ; valeur par défaut `media` sous le répertoire applicatif. `ew dev` utilise `.ew/dev/media`. En multi-hôte, tous les hôtes doivent partager le même volume ou un provider adapté.

`PurgeExpiredStagingAsync(minimumAge, batchSize)` est une opération locale explicite : minimum une heure, lots jusqu’à 1 000. Les dépôts expirés sont d’abord rendus non rattachables en base, puis supprimés du stockage avec reprise idempotente. Aucun effacement automatique des médias actifs ou historiques n’est activé. Les objets écrits avant une panne précédant toute métadonnée nécessitent une réconciliation opérationnelle du volume ; ne pas supprimer des fichiers sur la seule base de leur âge pendant que des uploads sont actifs.

## Recherche et exports

Le détail `/instances/{id}` reste disponible pour les instances terminales autorisées. Il présente le state filtré, la progression et les 100 dernières opérations moteur, annoncées comme historique récent. La loupe ouvre une modale puis `/search` ; les statuts sont multisélectionnables. Les dates civiles utilisent le fuseau de l’installation. Les curseurs de recherche sont chiffrés, liés à l’acteur et aux filtres ; un redémarrage invalide les curseurs de cette implémentation locale. Relancer la recherche dans ce cas. Aucun total global des instances interdites n’est exposé.

Les capacités administratives ajoutées sont `workflow.instance.search.initiator`, `security.audit.export`, `workflow.tat.read` et `workflow.tat.export`. Les nouveaux bootstrap administratifs les reçoivent ; les profils déjà existants doivent être modifiés explicitement par l’administration. Ces capacités ne donnent pas `instance.read` ni de droits de traitement métier.

L’audit conserve son droit `security.audit.read`. Les filtres serveur s’appliquent au tableau et à l’export. Le CSV UTF-8 est préparé sous limites avant envoi pour éviter une extraction tronquée ; formules tableur neutralisées. `configureAudit` dans `AddEnterpriseWorkflowPresentation` permet de régler le nombre de lignes (maximum 10 000), les octets (défaut 16 Mio) et le timeout (défaut 30 secondes). Un refus de limites est explicite. L’export est audité avant remise du fichier.

## Rapports TAT et SQL

Les tables `EW_TAT_INSTANCES`, `EW_TAT_STEPS`, `EW_TAT_ASSIGNMENTS` et `EW_TAT_EVENTS` sont additives et écrites dans les transactions source. Leurs colonnes sont identiques et citées avec leur casse sur SQLite et Oracle. `Coverage = 1` identifie les observations complètes ; les premières observations d’anciennes instances restent partielles. Les temps sont des millisecondes UTC calendaires.

`/admin/tat` et `ReadTatAsync` regroupent par processus/version/étape, et facultativement par analyste. Moyenne/min/max portent sur les intervalles clôturés dans la période ; nombre et âge des ouverts sont séparés. Les intervalles sous affectation ne sont pas du travail actif. Le rapport refuse plus de 10 000 observations plutôt que d’en agréger une partie silencieusement. `IWorkflowMaintenanceStore.RebuildTatAsync(instanceId, ...)` reconstruit une instance depuis les événements TAT retenus, dans une transaction ; plafond explicite de 50 000 événements par reconstruction.

Exemple commun SQLite/Oracle, moyenne calendaire des étapes clôturées :

```sql
SELECT i."ProcessId", i."ProcessVersion", s."NodeId", COUNT(*) AS observations,
       AVG(s."EndedAtMs" - s."StartedAtMs") AS moyenne_ms
FROM "EW_TAT_STEPS" s
JOIN "EW_TAT_INSTANCES" i ON i."Id" = s."InstanceId"
WHERE i."Coverage" = 1 AND s."Coverage" = 1 AND s."EndedAtMs" IS NOT NULL
GROUP BY i."ProcessId", i."ProcessVersion", s."NodeId";
```

Pour les analystes, le rapport admin somme les intervalles d’un même acteur par tâche humaine terminée avant de calculer sa moyenne ; la période porte sur la date de complétion de la tâche. En SQL, joindre `EW_TAT_ASSIGNMENTS` sur `StepId`, grouper par `ActorProviderId`/`ActorSubjectId` et exclure les pools sans acteur ; sommer les durées d’un analyste par tâche avant de calculer la moyenne si plusieurs intervalles lui appartiennent. Les permissions Web ne protègent pas une connexion SQL directe : fournir aux exploitants un compte de reporting en lecture seule.

## Sauvegarde et restauration

Arrêter les écritures et uploads du Host, sauvegarder la base avec les outils du SGBD, copier le volume privé des médias ainsi que les artefacts immuables référencés, puis relancer. Une restauration doit remettre cet ensemble au même point de cohérence et configurer le même provider. Une copie du seul fichier SQLite pendant des écritures WAL ne constitue pas une sauvegarde qualifiée ; utiliser son API de backup ou une procédure opérateur adaptée. Conserver les objets supprimés logiquement selon la rétention de l’installation avant toute purge physique explicite. Les sauvegardes Oracle et le stockage partagé doivent être qualifiés avec l’infrastructure réellement déployée.

## Présentation après recette : référence, commentaires et bibliothèque

Les nouvelles instances reçoivent une référence publique de dix caractères en base 36. Le format est `TTTTTSSRRR` : minutes UTC depuis 2026 sur cinq caractères (36^5 minutes), compteur local modulo 1296 sur deux et aléatoire cryptographique sur trois. Ce n’est ni un secret ni une garantie d’ordre global. Les stores SQLite/Oracle appliquent un index unique et huit tentatives bornées en cas de collision ; une erreur est retournée si aucune référence ne peut être attribuée. Le GUID reste la clé technique et les liens historiques restent fonctionnels. Les instances déjà présentes reçoivent une référence décimale de dix caractères lors de la migration, sans prétendre reconstituer leur numérotation temporelle. La recherche « Identifiant de demande » accepte référence publique et GUID.

Les collections de commentaires dédiées se déclarent ainsi :

```csharp
[CommentState, Label("Avis RH")]
public List<WorkflowComment> HrComments { get; init; } = [];
```

L’attribut accepte aussi les collections usuelles typées (`IEnumerable`, `ICollection`, `IReadOnlyCollection`, `IReadOnlyList`, tableaux) de `WorkflowComment` ; un autre élément est refusé. Les permissions et les champs cachés sont projetés avant extraction. Chaque collection accessible apparaît dans son encart de l’onglet Commentaires, avec auteur, date, texte et étape. Les commentaires issus du handler intégré peuvent porter le nom d’acteur résolu côté serveur au moment de la décision ; il ne provient jamais du formulaire. La définition d’action fige la présence de ce champ : les anciennes instances n’enrichissent pas leurs commentaires avec une propriété absente de leur ancien contrat d’état.

Ces collections et les propriétés typées `WorkflowMediaReference` (seules ou en collection) sont exclues récursivement du tableau d’état. Une référence média peut rester dans le DTO si elle a un rôle métier précis. La bibliothèque repose exclusivement sur le rattachement persistant à l’instance et la politique du fichier ; elle ne requiert pas de référence dans la projection d’état. Pour masquer un fichier, utiliser sa politique média, indépendamment de `[HiddenField]` sur une éventuelle référence métier.

`WithComponent<T>` raccorde une vue informative au formulaire de démarrage et à la tâche utilisant ce formulaire. La vue peut déclarer `[Parameter] public CanonicalJson? Values` pour les valeurs serveur du formulaire et `[Parameter] public CanonicalJson? State` pour l’état projeté selon les droits. Les deux contrats sont indépendants ; une saisie non soumise n’est pas une donnée serveur et n’alimente pas cette vue SSR. Les champs générés et les contrôles de soumission restent gérés par le framework. LeaveRequest démontre cette vue avec le motif de la demande.

L’audit admin fusionne les événements de sécurité et le journal métier transactionnel, avec filtre et CSV identiques. La pagination utilise date, source et identifiant pour conserver les événements simultanés des deux sources. L’export borne chaque source avant fusion et refuse une extraction dépassant ses limites. Les deux bases ne partagent pas de transaction de lecture globale. Pour les événements historiques sans contexte stocké, les étapes/noms absents ne sont pas reconstitués artificiellement ; le nom courant peut servir de libellé de consultation et un résultat historique non enregistré apparaît comme tel.

Le sample est désormais en release 1.4.0, processus 5, schéma d’état 4. Le framework ne réécrit pas les contrats immuables des anciennes définitions pour y ajouter les nouveaux attributs ; leurs instances restent liées à leurs artefacts d’origine.


## Itération DX : exécution locale et sérialisation

Le développement natif utilise `ew dev . [--environment nom]`. La configuration `ew.dev.json` v2 et les environnements `.ew/environments/<nom>` remplacent le besoin d’une copie Python du sample. Les espaces historiques `.ew/dev` ne sont ni supprimés ni importés automatiquement.

`ew dev --artifact <répertoire> --environment qualification --config <fichier>` démarre une release exacte sans build/watch ; le fichier de configuration est externe au package. `--configuration` et `--no-watch` sont des flags de sources, refusés avec `--artifact`. Le CLI copie dans le registre possédé uniquement les fichiers déclarés et vérifiés du package. Il conserve aussi les dépendances explicites pour pouvoir reprendre une instance après déplacement du dossier d’origine.

Les sources sont compilées en générations locales identifiées par leur empreinte de DLL/dépendances/ressources. Un redémarrage sans changement réutilise la même génération. Les révisions `1000000000 + N` et releases `…-ewdev.gN` sont réservées à ces générations ; elles ne sont pas des versions de production. Le manifeste les marque explicitement et un hôte de production les refuse. La base conserve les liens exacts ; le CLI charge la génération courante, les artefacts requis par toutes les instances conservées, et les dépendances configurées. Il ne substitue jamais une version absente. L’interface présente ces révisions comme « génération locale gN ».

`HumanTaskCompletionResult.Success()` accepte la soumission sans choisir la transition. Pour modifier l’état : `HumanTaskCompletionResult.Success(stateUpdate: NodeStateUpdate.Replace(state))`. Le runtime utilise l’outcome déjà déclaré pour l’action autorisée ; les outcomes des nœuds de décision restent distincts.

Le service DI `EnterpriseWorkflow.Core.Serialization.IWorkflowSerializer` fournit `Serialize<T>(value)` et `Deserialize<T>(canonicalJson)`. L’initialiseur peut retourner `new WorkflowStateInitializationResult(serializer.Serialize(state))`. Les conventions persistent les mêmes noms et types que les lectures typées et `Replace<T>` ; elles ne sont pas modifiables via un réglage global.

Budgets : `WorkflowRuntimeOptions.Serialization` en hébergement embarqué ; `Workflow:Serialization` dans les settings du Kernel ; `serialization` dans `ew.dev.json`. Propriétés : `MaximumStateBytes` et `MaximumDepth` (en JSON de développement : `maximumStateBytes`, `maximumDepth`). Défauts : 1 Mio et profondeur 64 ; ces bornes du moteur ne peuvent pas être dépassées par ce réglage. Une baisse des budgets peut refuser de nouvelles écritures d’un ancien état plus volumineux ; elle ne change pas son schéma ni sa lecture. Les budgets sont figés au démarrage et vérifiés à l’initialisation, aux commits humains/services et aux migrations ; la factory statique `Replace<T>` ne contourne pas le contrôle final.

Le sample courant est en release 1.5.0 / contrat 6 / schéma d’état 4. Les générations locales ne changent pas ces métadonnées déclarées dans le code source.
