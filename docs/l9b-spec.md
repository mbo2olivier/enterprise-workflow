# L9b — consultation, recherche, médias et temps de traitement

Date : 23 octobre 2026 ; décision état métier du 25 octobre 2026. Statut : besoins confirmés ; DTO d’état annoté, version explicite, migrations et mapping des formulaires acceptés (ADR 0026). Les recommandations A1–A7 sont acceptées par le porteur le 25 octobre 2026 (ADR 0027). Les signatures illustratives et détails techniques non arrêtés restent à stabiliser pendant l’implémentation. L’implémentation locale est livrée et sa qualification est en cours : voir le [guide des API effectives](l9b-sdk.md) et la [preuve et ses limites](evidence/l9b.md). Cette spécification ne remplace pas les preuves de qualification. L9b s’intercale entre L9 et L10 ; L10 ne fige ni ne publie les contrats avant sa qualification.

## Confrontation à l’existant

| Besoin | État observé dans le dépôt | Écart à traiter |
| --- | --- | --- |
| Détail depuis chaque écran | `Requests.razor` rend des cartes sans lien ; les demandes récentes de `Today.razor` pointent vers `/requests`. `TaskDetail.razor` expose `/tasks/{TaskId:guid}`. `IWorkflowPortalService` propose `ReadTaskAsync`, sans lecture de détail d’instance. | Créer une vue d’instance indépendante d’une tâche active, puis des liens partout où une instance est présentée. Le détail actuel depuis Today/inbox est un détail de tâche. |
| Recherche générale | `ListMyRequestsAsync` utilise `ReadWorkflowInstancesCommand(Initiator, Limit)` puis contrôle `WorkflowActions.ReadInstance` par définition/version. Limite bornée, sans recherche générale ni curseur. | Nouveau contrat de recherche autorisée ; ne pas élargir la liste actuelle en supprimant simplement le filtre initiateur. |
| Audit admin | `AdminAudit.razor` vérifie `security.audit.read` et appelle `ReadAuditAsync(250)`. `SecurityAuditEntry` contient date, acteur, action, cible, résultat. | Recherche paginée et export côté serveur ; distinguer audit de sécurité, historique métier et logs techniques. |
| Médias | Les contrats de formulaires, handlers, stockage et rendu existent ; aucun service transverse de pièces jointes n’a été identifié. Les ressources statiques de modules et payloads outbox ne sont pas des médias d’instance. | Contrats SDK/API, stockage durable, formulaires fichier, cycle de vie et bibliothèque sécurisée. |
| TAT | Les stores possèdent instances, tâches, complétions, timers, claims et commits durables. Les dates courantes et historiques d’acteurs ne constituent pas un suivi temporel normalisé complet des changements d’affectation. | Projections transactionnelles dédiées ; aucune reconstruction exacte d’un temps humain actif depuis ces seules données. |

Références de code : `src/EnterpriseWorkflow.Presentation/WorkflowPortalService.cs`, `src/EnterpriseWorkflow.Persistence.Abstractions/StoreModels.cs`, `src/EnterpriseWorkflow.Security.Abstractions/SecurityContracts.cs`, stores SQLite/Oracle et composants Web précités. Le modèle séquentiel, les versions immuables, le fencing, l’idempotence et la séparation lecture/action restent applicables (ADR 0007/0008/0019/0020/0024).

## 1. Détail d’instance et périmètre de lecture

Proposition : route canonique `/instances/{instanceId}` et service de lecture applicatif partagé par UI/API. Vue : identité/référence métier, processus et version, statut, dates, progression, champs de l’état métier filtrés côté serveur selon les annotations du DTO et les permissions du lecteur (§1.1), historique autorisé et onglet **Bibliothèque**. Elle reste accessible après complétion, annulation ou échec sans dépendre d’une tâche active. Les actions de traitement restent soumises aux contrôles de tâche existants.

Today, À traiter, Mes demandes, résultats de recherche et autres listes contenant une instance proposent le même accès au détail lorsque permis. Un droit de lecture de tâche ne doit pas donner implicitement accès à toutes les données d’instance ; le détail de tâche reste utilisable dans son propre périmètre. Le statut d’initiateur ou d’administrateur ne constitue pas à lui seul un droit métier.

Le service charge le contexte durable puis contrôle `instance.read` et la politique de visibilité d’instance retenue en A1. Ni identifiant fourni par le client, ni présence d’un lien, ni résultat antérieur de recherche ne remplacent cette vérification. Ressource absente ou interdite : réponse non révélatrice identique. Fournisseur indisponible : refus et erreur opérationnelle sans données.

### 1.1. État métier typé — décision acceptée le 25 octobre 2026

Le processus déclare obligatoirement son **state** au moyen d’un DTO C# (par exemple `LeaveRequestState`). Le DTO décrit les données métier que chaque instance maintient ; les métadonnées imposées par le framework (instance, workflow/version, initiateur, statut, dates, révision) restent séparées et ne peuvent pas être remplacées par ces données. Il ne s’agit pas d’un contrat nommé « Instance View » ni d’une seconde copie de l’état à alimenter.

La façade SDK expose une déclaration de type `WithState<LeaveRequestState>().WithStateSchemaVersion(1)` ; le nom exact des nouvelles API sera figé à l’implémentation. **La version du schéma reste explicite**, avec les migrations existantes de l’ADR 0022. Une empreinte déterministe peut aider aux diagnostics de changement, mais ne choisit ni la version, ni la compatibilité, ni la migration. Les artefacts et contrats historiques restent immuables ; aucune réécriture des hashes existants. Le DTO est une façade auteur au-dessus du stockage JSON durable, pas une dépendance CLR imposée au modèle canonique.

Les annotations du DTO centralisent structure, noms persistés stables, libellés (texte ou clé de traduction), visibilité et validation. Les noms d’attributs ci-dessous sont illustratifs, pas des API déjà livrées :

```csharp
public sealed class LeaveRequestState
{
    [Label("Date de début")]
    public DateOnly StartDate { get; init; }

    [Label("Montant approuvé")]
    [VisibleFor(LeavePermissions.ReadFinancialData)]
    [RequiredFor(StateValidationGroups.Approval)]
    public decimal? ApprovedAmount { get; init; }

    [HiddenField]
    public string? InternalReference { get; init; }
}
```

Par défaut, tous les champs déclarés du state sont affichables dans le détail par une personne autorisée à lire l’instance. Ajouter un champ peut donc l’exposer : l’aperçu résolu et la documentation doivent rendre cette conséquence visible. `HiddenField` exclut le champ du détail pour tous les lecteurs et prime sur les permissions ; une restriction de visibilité exige les permissions métier déclarées par le module, jamais les noms de profils de l’installation. Des formes distinctes expriment « au moins une permission » et « toutes les permissions ». Les règles s’appliquent récursivement aux objets et collections : autoriser un objet ne contourne pas la restriction d’un sous-champ. L’implémentation doit définir et valider les types supportés, noms JSON, nullabilité et contraintes, et diagnostiquer les déclarations non supportées avant publication.

La lecture est d’abord autorisée au niveau de l’instance, puis filtrée par champ ; une permission de champ n’accorde pas l’accès à l’instance. Ces annotations concernent la visibilité du détail : elles ne privent pas les handlers d’accès à l’état métier et ne donnent pas de droits implicites sur les formulaires, fichiers ou tâches. Le serveur produit les seules valeurs/métadonnées autorisées avant envoi au navigateur, y compris pour un composant personnalisé. Les données internes ne doivent pas fuir dans les erreurs de validation ou les diagnostics retournés au lecteur.

### 1.2. Formulaires indépendants et alimentation du state

Chaque formulaire définit son propre contrat d’entrée, avec des noms et une structure indépendants du DTO d’état. **Les handlers définissent explicitement le mapping**, tant pour le formulaire initial que pour les tâches humaines et les services. Aucune égalité de noms ne déclenche de copie ou fusion implicite. Les annotations de lecture des formulaires existants gardent leur portée ; elles ne définissent pas automatiquement la visibilité de l’état, et réciproquement.

Le formulaire initial ne devient plus directement l’état de l’instance sur le nouveau parcours typé : un handler de démarrage produit l’état initial validé. Le contrat de démarrage doit donc être complété explicitement, sans simuler une tâche humaine persistée avant création de l’instance. Les handlers humains/services produisent les mises à jour via les résultats existants (`HumanTaskCompletionResult`, `ServiceNodeResult`, `NodeStateUpdate`) ou leurs façades typées, à spécifier ; la persistance reste pilotée par le runtime. Une soumission seule, ou le handler standard qui ne modifie pas l’état, n’enrichit pas le state automatiquement.

La vue de détail lit l’état committé courant et applique les annotations ; aucune projection métier persistée supplémentaire n’est à synchroniser par les handlers. Les projections TAT du §5 restent des tables de reporting distinctes.

### 1.3. Validation permanente et contextuelle

Les invariants de structure, types et contraintes permanentes sont vérifiés pour tout état initial ou nouvel état avant persistance. Les champs alimentés ultérieurement peuvent rester facultatifs/nullables jusqu’à leur contexte d’exigence. Des groupes de validation nommés sont déclarés dans les annotations, puis associés explicitement aux nœuds/actions de la définition du processus. Le contexte de sortie est calculé par le serveur ; le navigateur ne peut pas choisir ou désactiver les groupes.

Ordre : validation de l’entrée du formulaire → handler et mapping/calcul du nouvel état → invariants permanents → groupes de validation du contexte de sortie → commit atomique de l’état et de la transition. Un échec ne committe ni état, ni progression, ni effets outbox/rattachements médias associés. Une mise à jour depuis un service et une migration vers le contrat cible passent également la validation applicable avant commit. La validation d’une migration ne simule pas une action utilisateur ; les groupes requis doivent être déterminés explicitement par le contexte cible.

Des validateurs personnalisés, associés aux mêmes groupes, couvrent les règles croisées qui ne se réduisent pas à une annotation de propriété. Ils ne produisent aucun effet externe ; les effets durables passent par les mécanismes transactionnels/outbox existants. Une validation ne peut pas annuler un effet externe déjà exécuté par un handler.

La compilation des annotations produit un contrat inspectable, versionné avec le processus et lié à l’artefact exact. Un changement incompatible du state exige la version explicite et la migration correspondantes ; un changement de libellé ou de visibilité implique une nouvelle définition/artefact immuable, sans nécessiter à lui seul une migration de données si leur structure reste compatible. Les anciennes instances continuent d’utiliser leur contrat exact. La stratégie d’adoption des anciens processus JSON doit être explicite : aucun mapping automatique ni requalification rétroactive des formulaires historiques.

## 2. Recherche générale

Dans la barre d’en-tête, bouton loupe immédiatement à gauche de l’encart utilisateur, disponible sur les écrans utilisant ce layout. Nom accessible « Rechercher des demandes », clavier, focus initial, fermeture Échap et restitution du focus. Ouverture d’une modale ; validation vers une SERP dédiée, proposée sous `/search`, avec filtres modifiables, pagination, état vide et erreurs de saisie.

« Demande » désigne ici une instance démarrée dans l’installation, tous initiateurs et statuts confondus, dans le périmètre de lecture courant de la personne. La recherche n’est donc pas limitée à Mes demandes.

Filtres : statut d’instance (multi-sélection), création début/fin, processus, version facultative, identifiant d’instance, référence métier ; étape courante et mise à jour début/fin proposés en complément. Le filtre initiateur et son sélecteur sont réservés à l’administration habilitée (A1), refusés côté serveur pour les autres, même injectés dans l’URL. Les dates civiles sont interprétées dans le fuseau configuré de l’installation et converties en intervalle UTC `[début, lendemain de fin[` ; début après fin refusé. Identités par `(ProviderId, SubjectId)`, processus/version par IDs exacts.

Pas de recherche libre dans le JSON d’état, le contenu des fichiers ou les champs non projetés en L9b. Recherche textuelle bornée sur les seules métadonnées autorisées. Aucun filtre, suggestion, facette ou nombre de résultats ne doit révéler une ressource interdite.

Contrat serveur typé : filtres validés, taille de page plafonnée, tri stable `(CreatedAtUtc DESC, InstanceId DESC)`, curseur opaque lié aux filtres et à l’acteur. Appliquer l’autorisation avant le contenu rendu, les compteurs et la pagination visible. Une limite sur les candidats suivie d’un filtrage local ne suffit pas : elle peut masquer des résultats permis. Avec un provider non traduisible en SQL, balayer les candidats de façon bornée et reprendre au curseur du dernier candidat examiné ; signaler une continuation, pas une fausse fin ni un total exhaustif. Ne pas exposer de total global non autorisé. SQL paramétré et indexes SQLite/Oracle qualifiés.

Revalider chaque lecture de détail et téléchargement. Aucun cache partagé de résultats entre acteurs ; une révocation entre recherche et ouverture doit interdire l’accès. Tester aussi les anciennes définitions et les permissions métier L8b.

## 2.1. Dette UX — sélection des destinataires par recherche distante

Besoin ajouté au lot par le porteur le 25 octobre 2026, avant implémentation : désigner une personne sans connaître ni saisir exactement son identifiant technique. Orientation retenue : sélecteur avec recherche distante progressive, sans préchargement exhaustif des utilisateurs. Ce sélecteur est distinct de la recherche générale des instances.

État observé : `StartProcess.razor` et `TaskDetail.razor` utilisent déjà un formulaire GET de recherche puis un `select` alimenté par les candidats renvoyés. Les services `SearchAssignmentCandidatesAsync`, `SearchTaskAssignmentCandidatesAsync` et `SearchRecoveryCandidatesAsync` existent. Le parcours actuel recharge cependant la page pour chercher et certaines options de tâche présentent des identifiants techniques. Le besoin n’est donc pas d’introduire une identité saisie librement mais de rendre la recherche et la sélection directes, lisibles et sans perte de saisie. La recherche partielle locale existe ; les capacités et sémantiques des annuaires distants doivent être vérifiées.

Livrer un composant réutilisable de type combobox accessible, avec saisie par nom/libellé et résultats distants contextualisés. Rechercher après au moins deux caractères, avec temporisation courte (cible initiale 300 ms), requêtes bornées et annulation/ignorance des réponses obsolètes. Ne pas télécharger tout l’annuaire ni présenter une liste partielle comme exhaustive. Si une limite est atteinte, inviter à affiner ; un provider incapable de recherche expose une indisponibilité explicite, sans repli vers un identifiant libre.

Afficher le nom et un complément autorisé utile à la désambiguïsation (par exemple email si disponible), sans imposer l’exposition de tous les attributs de profil. La valeur sélectionnée reste l’identité stable `(ProviderId, SubjectId)` ; le texte saisi n’est jamais une affectation valide. Modifier le texte après sélection invalide l’ancienne sélection. États explicites : saisie minimale, chargement, aucun candidat, erreur/recherche indisponible et candidat sélectionné. Navigation clavier, focus, labels et annonces de résultats accessibles ; aucune sélection automatique du premier résultat.

Réutiliser le même composant au démarrage, lors de la désignation pour l’étape suivante, et dans la récupération/réaffectation autorisée. Une recherche ne recharge pas le formulaire et conserve les champs, pièces jointes temporaires et sélections des autres destinataires. Les résultats sont isolés par acteur, processus/version, tâche/révision, action et cible ; un changement de branche/action invalide les sélections qui ne sont plus applicables. Une cible commune à plusieurs actions ne doit pas conduire à réutiliser à tort les candidats d’une autre action.

Le serveur déduit le contexte depuis la tâche et ne renvoie que les candidats éligibles : permissions de l’appelant à rechercher/désigner, habilitations de la cible, contraintes d’initiateur et séparation d’acteurs. Les endpoints de recherche ne deviennent pas un annuaire public. Revalider sélection et éligibilité à la soumission/activation selon les contrats existants ; révocation, candidat arbitraire, annuaire indisponible et tâche modifiée produisent un refus explicite sans substitution automatique. Le périmètre SDK/runtime des affectations reste celui de l’ADR 0024.

Qualification : recherche par fragment de nom sans identifiant exact, doublons de noms et fournisseurs distincts, résultats hors ordre, absence de candidat et panne annuaire, clavier, maintien des données du formulaire, changement d’action/cible, non-divulgation des utilisateurs inéligibles et retrait des droits entre recherche et soumission. Les scénarios navigateur couvrent démarrage, désignation suivante et récupération d’affectation, avec vérification locale et LDAP des recherches partielles sur les environnements revendiqués.

## 3. Recherche et extraction de l’audit

L9b cible l’audit de sécurité existant, pas un agrégateur universel des logs applicatifs. Filtres : intervalle de dates, acteur stable, action, cible et résultat ; corrélation/instance seulement si ces liens sont effectivement persistés, avec migrations additives explicites. Pagination stable par date et identifiant durable (à ajouter si absent), sans plafond silencieux des 250 dernières entrées.

Le même service applicatif porte recherche et export. Décision A3 : CSV UTF-8, colonnes documentées, échappement et neutralisation des formules tableur. Export de tous les résultats correspondant aux filtres, pas seulement de la page. Droit d’export distinct, audit de l’extraction (acteur, filtres non sensibles, date, nombre, résultat), aucune inclusion de secrets ou payloads métier bruts. Ne pas promettre une preuve d’intégrité cryptographique ou un journal inviolable.

Décision A3 : export synchrone en flux, période/taille/durée configurables ; dépassement explicite invitant à réduire la sélection, jamais de troncature silencieuse. Fixer une borne temporelle et une clé haute au départ pour exclure les nouvelles entrées ; documenter la cohérence en cas de purge concurrente. Recontrôler les droits à l’exécution. Un export asynchrone, hors périmètre retenu en A3, exige job durable, expiration, propriétaire et nouvelle autorisation au téléchargement ; il ne peut être un fichier public.

## 4. API médias commune SDK/framework

Contrats publics implémentés : `IWorkflowMediaService` pour créer depuis un flux, lire les métadonnées, ouvrir un flux, remplacer avec révision attendue, supprimer et valider une spécification ; `IWorkflowMediaStorageProvider` pour l’adaptateur binaire. Le Core canonique ne dépend ni de HTTP, ni d’un chemin local, ni du fournisseur. Un handler utilise un contexte technique borné à son installation/instance/exécution ; aucune API publique anonyme de stockage. Uploads et fichiers générés suivent le même service et les mêmes validations.

Métadonnées relationnelles : MediaId opaque, installation, instance, nœud/exécution ou tâche source, champ éventuel, origine upload/génération, créateur stable ou acteur système, nom d’affichage nettoyé, type détecté, taille, hash, dates UTC, révision, statut et clé de stockage privée. Les binaires ne vont pas dans le JSON d’état ni dans l’audit. Une référence typée MediaId/révision est persistée dans les données de formulaire ; aucune URL ou chemin arbitraire fourni par le client n’est accepté comme preuve de propriété.

Spécification de champ fichier versionnée : obligatoire/facultatif, nombre, taille individuelle/totale, extensions et types autorisés. Validation serveur des octets, du type et de l’appartenance ; le Content-Type et l’extension du navigateur ne suffisent pas. Même validation à la soumission et au remplacement, erreurs localisées ; impossible de contourner une règle via l’API ou un composant personnalisé. Flux et quotas bornés, pas de chemins dérivés du nom utilisateur ; téléchargement en pièce jointe par défaut, sans rendu actif HTML/SVG dans le navigateur.

Cycle retenu pour A4 : dépôt temporaire privé, validation, rattachement atomique à la soumission ou au commit du handler, activation, suppression logique et purge différée selon rétention. Un dépôt avant création d’instance est lié à l’acteur et à la soumission idempotente, puis rattaché lors du démarrage ; les abandons expirent. Stockage binaire et transaction SQL ne sont pas prétendus atomiques : staging durable et finalisation/nettoyage idempotents après commit, reprise après panne, aucun média incomplet téléchargeable. Les retries d’un handler réutilisent une clé d’opération stable ; les remplacements concurrents utilisent une révision attendue. Une tentative de worker périmée ne peut rattacher un fichier.

Lecture, remplacement et suppression exigent accès à l’instance et politique média/source ; le droit de traiter une tâche ne permet pas de modifier toute la bibliothèque. Les médias issus de champs non visibles restent masqués. Les générations système déclarent explicitement leur audience, refus par défaut sans politique. Le détail de la bibliothèque et chaque endpoint recontrôlent les droits ; aucun lien permanent public. La bibliothèque regroupe uploads et productions du flow, avec nom, origine, date, taille, état et actions permises.

Décision A4 : identifiant logique stable et révisions binaires immuables ; remplacement crée une révision, références historiques préservées. Suppression logique retire l’accès courant ; la purge respecte rétention et références existantes, sans restauration implicite d’un fichier supprimé via une ancienne URL. Sauvegarde/restauration doit couvrir métadonnées ET binaires de façon cohérente. Quotas et rétention configurés/documentés avant production ; aucun effacement silencieux par défaut. Hook d’analyse antivirus avec état bloqué/en attente et politique explicite de l’installation (A5).

## 5. Projections TAT normalisées

Objectif : durées de bout en bout, durée par étape, âge des étapes ouvertes, attente avant prise et durée sous affectation par analyste. Le TAT retenu en A6 est une durée calendaire UTC, en millisecondes, entre événements committés ; affichage adapté au fuseau. Le temps sous affectation inclut inactivité, nuits et absences : **il ne mesure pas le temps de travail actif**. Calendriers ouvrés, pauses déclarées et SLA sont un périmètre supplémentaire à arbitrer.

Proposition de tables additives, noms indicatifs :

| Projection | Granularité et colonnes structurantes |
| --- | --- |
| `EW_TAT_INSTANCES` | Une ligne par instance : processus/version, initiateur stable, démarrage, fin nullable, résultat, version de projection, couverture historique |
| `EW_TAT_STEPS` | Une ligne par occurrence d’exécution : instance, nœud et version, ID d’exécution durable, type, entrée, fin nullable, résultat ; tentatives techniques distinctes si suivies |
| `EW_TAT_ASSIGNMENTS` | Une ligne par intervalle d’affectation/claim : occurrence/tâche, acteur stable nullable pour pool, mode, début, fin nullable, motif de fin (release, réaffectation, complétion, annulation) |
| `EW_TAT_EVENTS` | Événements minimaux dédiés au replay : clé unique de mutation, ordre/révision source, instant UTC, type et références normalisées, sans état métier brut |

Les clés d’acteurs restent `(ProviderId, SubjectId)` ; une suppression/renommage d’annuaire ne change pas l’historique. Clés étrangères et index par processus/version/nœud/acteur/date. Fournir dictionnaire, vues SQL documentées et exemples SQLite/Oracle : moyenne par analyste et tâche, moyenne par étape/processus/version, demandes ouvertes âgées. Accès SQL par comptes de reporting en lecture seule, administrés par l’exploitant : les permissions Web ne s’appliquent pas automatiquement à un accès direct à la base.

Capturer démarrage, entrée/sortie de nœud, activation humaine, désignation, claim/release/réaffectation, complétion, timer, erreur terminale, annulation et migration de version. Fermeture des intervalles ouverts en cas de terminaison ; retries techniques ne doublonnent pas une occurrence métier. Définir explicitement les frontières de nœuds techniques et les tentatives pour ne pas compter les backoffs comme travail humain. Migration conserve les anciennes occurrences/version et ouvre la suite dans la version cible.

Décision A7 : événement TAT et mise à jour des projections dans la même transaction SQL que chaque mutation durable source, sur les deux providers, y compris commandes humaines et maintenance ; pas uniquement dans le worker ni dans l’UI. Révision source, unicité des événements et fencing rendent replay/retry sans double comptage. L’échec de projection annule la mutation : coût et couplage à mesurer. Aucune modification du JSON canonique, des hashes historiques ou des colonnes moteur n’est requise, mais les chemins de commit doivent être enrichis et les tables ajoutées par migrations.

Reconstruction à partir des événements TAT dédiés, procédure versionnée, reprise et validation des agrégats. Pour les instances antérieures à l’activation, importer seulement les faits prouvables ; marquer les intervalles incomplets et les exclure des moyennes par défaut. Ne pas inventer d’anciennes dates de claim. Rapport : période, processus/version, étape, analyste stable, statut/résultat, population/cohorte explicite ; moyenne, nombre d’observations, min/max et âge courant des ouverts séparés. Les ouverts ne sont pas des durées de complétion nulles ou nulles comptées comme zéro. Rapport par analyste fondé sur les intervalles attribués, sans lui imputer l’attente du pool ; présenter séparément l’acteur ayant terminé la tâche. Cohorte proposée pour les moyennes : étapes clôturées dans l’intervalle, hors couverture partielle ; âge des ouverts à l’instant du rapport.

Administration : droits de lecture/export TAT distincts de la gestion des accès et des droits métier ; données minimales de reporting, sans accès implicite aux formulaires ou fichiers. Export selon les mêmes garanties de bornage et protection que l’audit.

## Arbitrages A1–A7 — acceptés le 25 octobre 2026

Le porteur accepte les sept recommandations ci-dessous le 25 octobre 2026. La colonne « Décision acceptée » fait foi ; les alternatives sont conservées pour expliquer les options écartées. Voir l’[ADR 0027](adr/0027-consultation-medias-audit-tat-l9b.md). Cette acceptation ne constitue pas une preuve d’implémentation ou de qualification.

| ID | Question | Décision acceptée | Alternative écartée / conséquence |
| --- | --- | --- | --- |
| A1 | Qui peut consulter quelles instances, et utiliser le filtre initiateur ? | Conserver `instance.read` ; appliquer la visibilité du DTO acceptée au §1.1 pour les nouveaux contrats et préserver les contrats historiques ; pas de droit automatique d’initiateur/admin. La recherche retrouve toutes les instances lisibles, même d’autrui. Ajouter une capacité administrative explicite pour le filtre initiateur, sans élargir la lecture. | Restreindre la lecture à initiateur/participants exige une politique par instance nouvelle, à spécifier et attribuer ; les grants actuels ciblent surtout définition/version. |
| A2 | Quels journaux rechercher/exporter ? | Audit de sécurité existant en L9b, avec liens de corrélation ajoutés explicitement si nécessaires. | Inclure tous les événements métier/logs techniques demande un contrat et une politique de collecte/rétention supplémentaires. |
| A3 | Format et volumétrie des exports ? | CSV UTF-8 en flux, bornes configurables et erreur explicite au dépassement ; même socle pour audit/TAT. | Gros volumes asynchrones et/ou XLSX demandent jobs, stockage privé, expiration et suivi d’état. |
| A4 | Où stocker les fichiers et que signifie remplacer/supprimer ? | Adaptateur disque privé livré pour Kernel/embedded/`ew dev`, interface extensible ; révisions immuables, suppression logique/purge contrôlée. | BLOB SQL simplifie la sauvegarde atomique mais grossit les bases ; stockage objet exige un adaptateur livré et qualifié ; disque exige volume partagé si plusieurs hôtes. |
| A5 | Analyse antivirus et politique fichier ? | Hook et états de validation, profil de limites/types configurable ; activation de l’analyse selon l’installation, sans revendiquer un antivirus livré. | Exiger un scanner réel comme gate de L9b ajoute une dépendance de déploiement et sa qualification. Fixer aussi les limites et rétentions avant production. |
| A6 | Définition du TAT et historique antérieur ? | Temps calendaire, attente et affectation séparés ; historique prospectif avec import partiel marqué, exclu des moyennes par défaut. | Temps ouvré/actif, pauses et recalcul historique exhaustif nécessitent nouvelles données, calendriers et règles de calcul. |
| A7 | Cohérence des projections TAT ? | Écriture transactionnelle source + événements + projections, reconstructible ; mesurer le coût. | Projection asynchrone via journal transactionnel/outbox dédié : progression moins couplée aux agrégats, mais retard, checkpoints et supervision nécessaires. Une écriture best effort sans journal durable est exclue. |

## Découpage et critères de sortie

1. **L9b-0 — contrats et décisions** : matérialiser les décisions acceptées A1–A7 et les ADR 0026/0027, préciser les politiques de visibilité/rétention/limites, contrats publics et schémas additifs. Distinguer configuration de l’exploitant et droits de l’utilisateur ; appliquer la décision prépublication de l’ADR 0028 pour les anciens parcours d’auteur.
2. **L9b-1 — lecture et recherche** : DTO annoté et version explicite, mapping initial/humain/service, validation et visibilité du state, détail d’instance, liens transverses, bouton/modale/SERP, requêtes autorisées paginées SQLite/Oracle. Cette politique de lecture précède bibliothèque et recherche. Inclure le sélecteur distant réutilisable des destinataires (§2.1), indépendant du moteur de recherche des instances.
3. **L9b-2 — audit** : service de recherche, filtres/indexes, pagination et export auditables ; aucun export limité silencieusement à une page.
4. **L9b-3 — médias** : contrats SDK/provider, migrations et cycle durable, formulaires fichier et handlers, bibliothèque, politique et quotas ; étendre template, sample, Kernel, embedded et `ew dev`.
5. **L9b-4 — TAT** : événements/projections normalisés, tous chemins de mutation instrumentés, vues/dictionnaire SQL, rapports admin et exports, reconstruction et couverture historique.
6. **L9b-5 — qualification intégrée** : parcours avec upload et génération, reprise/retry/fencing, remplacement/suppression concurrents, recherche après révocation, audit/TAT export, installation et restauration depuis copie propre ; SQLite, Oracle Enterprise 19.19, matrice CI et navigateur Linux. Actualiser preuve et contrats de distribution avant L10.

Acceptations état métier : DTO/formulaires de noms différents, mapping initial explicite, absence de copie implicite, mise à jour humaine/service validée, champ différé requis seulement au bon contexte, invariant et règle croisée refusés sans commit/effet outbox, contexte forgé ignoré, champ caché et sous-champ protégés côté serveur, permissions any/all, état DTO obligatoire, mapping déclaratif des commentaires sans doublon, remplacement typé et migration cible validés.

Acceptations obligatoires : accès direct par ID et requête forgée refusés ; absence de fuite via compteurs/suggestions, curseurs ou bibliothèque ; droits retirés entre modale, SERP et détail/téléchargement/export ; séparation admin/métier ; instance terminale lisible avec les bons droits ; résultats permis au-delà des premières pages de candidats ; dates/fuseaux/bornes stables. Médias : tailles/types falsifiés, chemins malveillants, fichier d’autrui, fichier temporaire abandonné, panne entre stockage et SQL, retry et worker périmé, restauration des binaires. TAT : durées déterministes sous horloge contrôlée, pool/claim/release/réaffectation, timer/retry/annulation/migration/replay sans doublons, reconstruction et données antérieures marquées. Exports : filtres identiques à l’écran, accents, formules CSV neutralisées, dépassement explicite, nouvelles entrées concurrentes et contrôle des droits.

Aucune mesure de performance n’est déclarée acquise : jeux de données, volumes de fichiers, cardinalités et seuils restent à fixer avec Q06 avant publication. Les migrations et la suite de conformité doivent couvrir les deux providers ; les preuves L9 ne qualifient pas automatiquement L9b.

## État d’implémentation locale — 25 octobre 2026

Les contrats effectivement livrés, plafonds et modalités d’exploitation sont décrits dans le [guide L9b](l9b-sdk.md). Les critères ci-dessus restent la cible de qualification. La [preuve locale](evidence/l9b.md) signale notamment la portée actuelle du hook de scan, les limites de pagination/rétention, les scénarios acquis et les contrôles externes restants ; ces points ne sont pas déclarés satisfaits par simple présence du code.

### Complément accepté — simplification de l’API média

Les politiques de fichiers sont déclarées uniquement dans `WorkflowModuleBuilder.MediaPolicies` ; ni surcharge ni déclaration au niveau processus, formulaire ou handler. Une politique nommée impose les formats, la taille et une visibilité explicite. Les formulaires utilisent `UseMediaPolicy` et ne définissent que l’obligation et le nombre de fichiers.

`media.For(context)` prépare la portée depuis le contexte serveur et expose stockage, lecture, remplacement, suppression et récupération des fichiers d’un champ. Les écritures sont collectées par exécution et liées automatiquement au commit réussi du handler, sans tableau de références obligatoire dans son résultat. Le rattachement à la bibliothèque ne remplace pas le mapping explicite de références vers le state. Les règles de quarantaine, d’idempotence, de contrôle de révision et d’autorisation restent applicables. Les suppressions sont différées jusqu’au commit, avec les mêmes garanties transactionnelles que les rattachements.

Les snapshots des politiques sont intégrés aux définitions publiées pour conserver les règles de la version d’origine. La décision prépublication de l’ADR 0028 dispense de conserver des primitives pour la seule compatibilité avec les prototypes ; les primitives internes nécessaires aux adaptateurs du framework restent distinctes de l’API d’auteur. Les politiques inline ne font pas partie du contrat cible.

## Compléments du 26 octobre 2026 — prochaine itération du L9b

### Décisions acceptées — implémentation locale livrée pour qualification

- Formats intégrés : PDF, DOCX, ODT, RTF ; XLSX, ODS, CSV ; PPTX, ODP ; TXT UTF-8 ; JPEG, PNG, TIFF. Les formats existants sont conservés. Chaque ajout doit disposer d’un validateur de contenu adapté, avec limites de lecture et de traitement ; une extension ou un MIME déclaré ne suffit pas. Les formats conteneurs nécessitent un contrôle de structure et des limites de décompression. L’acceptation d’un format n’implique pas l’exécution de son contenu ni une garantie antivirus. Les politiques restent exclusivement déclarées au module.
- Les anciens formats DOC/XLS/PPT, les variantes à macros et les formats spécialisés ne font pas partie de cette première liste. Prévoir un registre extensible et des validateurs fournis par les développeurs, liés à leur artefact exact, sans changement ultérieur du SDK pour chaque format ajouté. Le registre `MediaFormats` et le contrat `IWorkflowMediaFormatValidator` sont implémentés ; la résolution utilise l’artefact propriétaire exact.
- Lecture typée : `context.ReadState<TState>()` dans les contextes qui exposent le state ; `context.ReadSubmission<TInput>()` dans les contextes qui exposent une soumission. Une primitive `CanonicalJson.Deserialize<T>()` peut servir de socle. Respecter les conventions de sérialisation du contrat, retourner un objet détaché et produire une erreur explicite en cas d’incompatibilité. Aucune validation métier, migration ni sauvegarde implicite ne découle de la lecture. Formulaire et state restent indépendants.
- Mutation typée : surcharge `NodeStateUpdate.Replace<TState>(state)` utilisable dans les résultats humains et services. Elle exprime le remplacement complet du state, et non un patch ou une fusion des seules propriétés du DTO. La validation du contrat et des règles contextuelles reste effectuée avant le commit transactionnel, sous la version de schéma courante ; une évolution de structure requiert toujours version et migration explicites. Modifier l’objet détaché seul ne persiste rien.

Ces décisions sont implémentées localement dans l’ultime itération du L9b ; voir le guide des API effectives et la recette déléguée. Leur qualification Oracle, navigateur, documents réels et CI reste distincte de la compilation et des tests locaux.

### Mapping déclaratif des commentaires — décision acceptée

Le porteur accepte `action.RequireCommentAt(s => s.Comments)` pour que le handler d’approbation intégré contrôle et mappe le commentaire vers le state typé. Aucun handler métier supplémentaire n’est demandé pour cette opération ; les handlers métier restent associés aux formulaires et aux nœuds de service. `action.CommentAt(s => s.Comments)` conserve un commentaire facultatif lorsqu’un texte est soumis.

La cible est une collection de `WorkflowComment` : ajout d’une entrée structurée contenant le texte soumis, l’étape, l’action, l’acteur et la date serveur. Les commentaires précédents sont conservés. Les métadonnées de l’auteur, de l’étape, de l’action et de la date ne sont pas acceptées depuis le formulaire. Une soumission facultative sans texte n’ajoute aucune entrée.

Le fluent propage le type déclaré par `WithState<TState>()` jusqu’au builder d’action pour inférer le paramètre `s`. L’expression est vérifiée à la compilation du module puis transformée en chemin JSON durable, respectant `JsonPropertyName` ; aucune lambda n’est persistée. Une cible inexistante ou de type incompatible est refusée à la publication.

Le handler intégré prépare la modification du state, puis applique les validations structurelles et contextuelles avant le commit. Commentaire, décision et autres effets sont committés dans la même transaction, sous contrôle des droits, de révision et d’idempotence. Un rejet de validation, une exception ou un conflit ne modifie pas le state ; un replay ne duplique pas le commentaire. Les règles de visibilité du DTO s’appliquent à la collection et à ses champs dans les vues de lecture.

### État typé obligatoire et absence de rétrocompatibilité prépublication — décision acceptée

Décision du porteur du 26 octobre 2026 : aucune version du framework n’ayant encore été publiée, ne pas développer de couche de compatibilité avec les anciennes façons de déclarer ou manipuler le state. Cette décision remplace les exigences antérieures de ce lot visant à charger les contrats de développement sans DTO ou à maintenir des parcours d’auteur alternatifs.

- Tout processus doit déclarer un DTO par `WithState<TState>()`, avec version de schéma explicite et initialisation/mapping métier explicites. Un processus sans donnée métier déclare également un DTO, éventuellement vide.
- Migrer les tests internes, fixtures, exemples, templates et hôtes embarqués qui utilisent encore les anciens parcours vers l’état typé obligatoire. Retirer les branches de compatibilité et les tests exclusivement destinés à les maintenir ; conserver les garanties métier qu’ils vérifiaient en les adaptant au nouveau contrat.
- Le commentaire des approbations est mappé par le mécanisme déclaratif ci-dessus, pas par un handler ajouté uniquement pour copier ce champ. Adapter en particulier l’exemple LeaveRequest et son state pour conserver les commentaires RH/manager.
- La persistance canonique JSON reste le contrat interne durable : l’obligation de DTO concerne la définition et la DX du processus, sans imposer des types CLR à la base de données ou au moteur canonique.
- La version explicite du schéma, les migrations et le rattachement de chaque instance à sa définition/son artefact exact restent des garanties du produit. Après publication, faire coexister plusieurs versions d’instances reste un besoin ; cette garantie ne constitue pas une obligation de supporter les anciens prototypes d’API avant la première publication.

Ces évolutions sont implémentées localement et passent en qualification. Les tests canoniques du moteur restent indépendants des types CLR ; les parcours d’auteur sans DTO ont été retirés et les exemples/templates/tests d’auteur adaptés. Référence : [ADR 0028](adr/0028-etat-type-obligatoire-et-commentaires-approbation.md).

## Recette finale du porteur

Les contrôles longs, les commandes reproductibles et les résultats attendus sont rassemblés dans [validation/l9b-final-recette.md](validation/l9b-final-recette.md). Les confirmations M1–M8 et les éventuelles captures seront consignées comme preuves du porteur ; aucune exécution déléguée n’est déclarée réussie par anticipation.

## Ajustements acceptés après recette M1–M7

- Référence publique de dix caractères A–Z/0–9, distincte du GUID technique : cinq caractères pour les minutes depuis le 1er janvier 2026 UTC (plage de 36^5 minutes), deux pour un compteur local modulo 1296 et trois aléatoires cryptographiques. Cette représentation n'est pas un calendrier lisible ni un secret. L'unicité repose sur un index unique en base ; les collisions déclenchent une génération bornée, jamais une réaffectation. Référence immuable, recherchable et copiable ; les anciens liens GUID restent valables. Une exhaustion de la plage temporelle est refusée explicitement.
- Un composant de ligne partagé présente référence, processus, date de création, initiateur (nom et ID) et statut dans toutes les listes ; seule la destination/action varie.
- Détail : Détails (métadonnées + badge statut + état dans un encart), Bibliothèque, Progression, Historique et Commentaires dans des onglets distincts.
- `[CommentState]` désigne une collection de `WorkflowComment`, avec le label comme titre de son encart ; elle est retirée du tableau générique. L'extraction respecte toutes les restrictions de visibilité avant affichage. Auteur, date, texte et étape sont présentés dans un tableau horizontal.
- Les références médias restent facultatives dans l'état métier mais ne sont jamais exposées dans son tableau générique, y compris sous forme imbriquée. La bibliothèque repose sur les relations médias/instance ; la collecte ne nécessite pas de dupliquer les références dans l'état.
- Les composants de formulaire personnalisés sont effectivement raccordés au démarrage et aux tâches ; ils reçoivent explicitement les valeurs de formulaire et la projection d'état autorisée, sans accès au state brut. Le composant remplace la partie champs générée, sans remplacer les contrôles de soumission et d'autorisation du framework.
- L'audit admin rassemble sécurité et exécution métier, avec acteur (ID et nom), date, module, processus, action, résultat et étape. Recherche et CSV partagent les mêmes filtres : acteur texte, dates, modules/processus/actions en listes. Les opérations système restent sélectionnables ; l'absence d'acteur humain est indiquée « Système ». Les événements métier proviennent du journal transactionnel source, sans émission secondaire pouvant se désynchroniser. Les noms historiques indisponibles ne sont pas inventés.

M1–M7 ont été confirmés par le porteur avant ces changements ; une recette ciblée sera nécessaire pour ces ajustements. M8 attend la CI après commit/push.

## Prochaine itération L9b — CLI de développement natif .NET

**Décision acceptée, implémentée localement et en qualification.** Python n'est pas un prérequis du framework ; les scripts de recette ne constituent pas l'interface de développement du produit. Les commandes/options suivantes sont désormais exposées par le CLI ; la qualification de distribution et de surveillance longue reste distincte des tests locaux.

```bash
ew dev .
ew dev . --environment recette-rh
ew dev . --environment recette-manager
ew dev --artifact ./artifacts/sample.leave-request/1.4.0 --environment qualification-1-4 --config ./ew.dev.json
ew dev . --environment recette-rh --reset
```

- Sources : compiler le projet courant et surveiller ses changements dans l'environnement sélectionné (`default` par défaut). Les nouvelles instances utilisent la génération courante ; les instances conservées restent attachées à la leur. Le développeur ne change pas manuellement sa release à chaque frappe/reconstruction.
- Environnements nommés : base SQLite, médias, clés, seed, registre d'artefacts, configuration résolue et verrou isolés sous `.ew/environments/<nom>`. Un reset archive uniquement cet environnement sous `.ew/archives`; il ne supprime pas isolément ses binaires.
- Artefact : sélectionner explicitement une release packagée et vérifiée sans compilation ni surveillance ; les options de build/watch explicites sont refusées. La configuration reste externe à l'artefact immuable.
- Chargement : génération courante et artefacts réellement nécessaires aux instances de l'environnement, avec fermeture des dépendances exactes ; ne pas charger toutes les releases simplement parce qu'elles se trouvent sur disque. Un artefact nécessaire absent/incompatible bloque avec un diagnostic identifiant environnement, artefact et instances concernées ; aucun repli sur une autre version.
- Les générations de développement doivent posséder une identité durable explicite. Ne pas écraser une définition/receipt existante ni accepter deux hashes sous une même clé durable actuelle module/release ou processus/version. Leur représentation dans le catalogue et la persistance fait partie du travail technique de cette itération ; les garanties des artefacts de production restent inchangées.
- `ew.dev.json` version 2 : configuration commune + adaptations par environnement ; priorité options CLI > environnement choisi > commun > défauts. `defaultEnvironment` choisit l'environnement implicite. Aucun changement de droits/seed existants ni reset automatique. Une modification incompatible du seed nécessite un choix explicite de nouvel environnement ou de reset.
- Les chemins usuels de base/médias sont implicites et possédés par l'environnement ; un chemin externe avancé ne peut pas être effacé/archivé implicitement. Sélection d'artefact et environnement sont des choix d'exécution, pas une mutation du manifeste du module.

Exemple cible :

```json
{
  "schemaVersion": 2,
  "defaultEnvironment": "default",
  "build": { "configuration": "Debug", "watch": true },
  "server": { "url": "http://localhost:5099" },
  "seed": {
    "users": [
      { "id": "alice", "displayName": "Alice — Demandeuse", "permissions": ["sample.leave-request.request", "sample.leave-request.read"] }
    ]
  },
  "environments": {
    "recette-rh": { "server": { "url": "http://localhost:5100" } },
    "qualification-1-4": { "build": { "watch": false }, "server": { "url": "http://localhost:5101" } }
  }
}
```

La configuration `ew.dev.json` v1 actuelle et `.ew/dev` ne sont pas renommés/effacés implicitement. La stratégie explicite d'archivage/reprise de ces environnements doit être décrite dans la recette avant le basculement.

### Compléments DX acceptés — implémentation locale

1. Retirer l'outcome du résultat de tâche humaine : `HumanTaskCompletionResult.Success()` ou `Success(stateUpdate: NodeStateUpdate.Replace(state))`. L'action soumise et autorisée reste portée par le contexte ; le runtime choisit l'outcome/transition dans la définition immuable (`declaredAction.Outcome`), valide l'état puis commit atomiquement décision, médias et effets. Le handler accepte/rejette la soumission et propose des mutations ; il ne choisit pas une autre transition. Adapter handler intégré, samples, templates et tests, sans ancien overload prépublication. Les outcomes des nœuds de décision restent un mécanisme distinct.
2. Introduire une façade de sérialisation canonique injectable, par exemple `IWorkflowSerializer.Serialize<T>(value)`, pour l'initialisation et les usages avancés. Les handlers n'ont pas à répéter les limites de taille/profondeur ni à faire `JsonSerializer.Serialize` suivi de `CanonicalJson.CreateObject`. Le paramètre actuel est `maximumBytes`, pas une taille minimale.
3. La façade, les lectures typées, `Replace<T>`, l'initialisation, les migrations et les contrôles avant commit doivent partager les mêmes conventions et limites effectives. Un simple wrapper DI laissant les factories statiques sur d'autres limites ne suffit pas. Le runtime recontrôle toujours les données avant persistance.
4. Les réglages d'exploitation peuvent centraliser les budgets et la profondeur. Les conventions modifiant le JSON persistant (noms, casse, enums, convertisseurs, omissions) sont liées au contrat d'état/module et à sa version ; un changement de settings global ne doit pas réinterpréter une instance historique ni remplacer une migration. La canonicalisation déterministe et la validation du contrat ne sont pas désactivables.

Ces compléments sont implémentés dans cette itération. Les anciennes preuves ne constituent pas une qualification anticipée de ces nouvelles API.


Précisions techniques livrées pour le CLI : les noms d’environnement sont insensibles à la casse ; les générations utilisent des manifests explicitement marqués et des révisions `1000000000 + N` réservées à l’exécution locale. Les révisions de contrat de production à partir de 1000000000 sont refusées par le packaging/chargement de modules. Les déclarations sources et les artefacts de production ne sont pas réécrits. Un artefact local ciblé doit être un package de release ; les anciennes générations locales requises par ses instances peuvent coexister dans cet hôte de développement, tandis que le Kernel de production les refuse toujours. Le catalogue de démarrage est limité à l’artefact courant choisi, même si une ancienne génération possède une révision technique numériquement supérieure.

Le service injectable est `IWorkflowSerializer` (Core.Serialization). Les budgets configurables sont bornés par les limites existantes du moteur : 1 Mio et profondeur 64 au maximum. Les conventions structurelles restent figées. Les commits et migrations recontrôlent ces budgets, y compris si le handler utilise directement `NodeStateUpdate.Replace<T>`.

### Recette UI du 27 octobre : étapes et résultat métier

Toutes les listes de demandes (Today, Mes demandes, recherche, À traiter) utilisent la même ligne et affichent l’étape courante, ou l’étape terminale atteinte. Les positions sont chargées en une requête groupée par page, sous les contrôles d’accès existants.

Le statut technique `Completed` ne signifie pas que la demande a été acceptée. Le concepteur déclare le résultat sur le nœud terminal : `flow.End("rejected", WorkflowCompletionOutcome.Declined, "Demande refusée")` ; les autres résultats sont `Succeeded` et `Cancelled`. Les vues affichent respectivement Terminée, Refusée ou Annulée. Les définitions historiques dépourvues de cette métadonnée restent Terminée avec un badge neutre ; aucun résultat métier n’est déduit du nom d’un nœud. La métadonnée absente est omise de la sérialisation pour préserver les définitions publiées.

Le titre du détail affiche `No.<référence>` et une icône de copie accessible. Les formulaires de recherche et de rapport TAT séparent les champs et les actions dans un pied de formulaire. Les messages de commentaires vides sont centrés.
