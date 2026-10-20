# ADR 0024 — SDK fluent, permissions métier et affectation depuis une étape précédente

Date : 20 octobre 2026.

**Statut : principes acceptés par le porteur ; modalités signalées comme proposées ou ouvertes. Non implémenté.**

## Contexte et portée

Le SDK actuel expose trop directement les contrats techniques. Le module doit devenir lisible à l’écriture et en revue, y compris lorsque le code est produit par un agent. Cette décision prépare la prochaine itération L8b, avant les templates et le packaging L9. Elle complète les ADR 0009, 0014, 0017, 0019, 0020, 0022 et 0023 ; elle ne requalifie pas leurs preuves historiques.

## Décisions acceptées

### A1 — Release et révisions héritées

Le module déclare sa version de release textuelle et une `ContractRevision` entière positive. Les processus, handlers et formulaires héritent par défaut de cette révision. Un contrat peut explicitement référencer une révision antérieure. La version du schéma des données initiales doit toujours être déclarée explicitement ; elle ne se déduit ni de la release ni de la révision des contrats. Une nouvelle release ne migre pas automatiquement les états persistés.

L’identité d’artefact reste `(ModuleId, ReleaseVersion, Sha256)`. `ContractRevision` ne remplace pas la version de release de l’artefact. Les anciennes instances conservent leurs références exactes et la rétention des artefacts reste obligatoire.

**Contrainte :** réutiliser une ancienne révision ne permet jamais de modifier son contenu ou de relier silencieusement son handler à une nouvelle implémentation. Le registre actuel refuse deux registrations de la même clé de handler. La résolution de la réutilisation doit donc être fixée avant implémentation (O1).

### A2 — Identifiants locaux et compilation

Les identifiants globaux de processus, formulaires et handlers sont qualifiés par le module. Les nœuds restent locaux au processus, les champs au formulaire et les actions à leur tâche. Les identifiants sont explicites et stables ; ils ne sont pas déduits des noms de classes CLR. La qualification finale respecte la grammaire et la longueur maximale de `TechnicalId`.

Les processus peuvent être déclarés dans des classes séparées et regroupés par le module. Le SDK produit des contrats inspectables et validés, sans sérialiser de lambda ou de type CLR dans le graphe durable. Une étape de service utilise un handler ; une transition cible l’étape, jamais directement le handler. La fin reste explicite. Le périmètre acyclique et à branches exclusives de l’ADR 0007 reste inchangé.

### A3 — Formulaires et handlers indépendants

Les handlers sont enregistrés séparément des formulaires. Chaque formulaire métier déclare néanmoins son handler par défaut avec `.HandledBy<THandler>()`. La liaison est explicite, vérifiable et versionnée ; elle ne dépend pas de l’ordre des déclarations. Le remplacement du handler au niveau d’une tâche réutilisant ce formulaire est proposé (P1), pas encore arbitré.

Les métadonnées du module sont déclarées une seule fois côté auteur. Le packaging devra produire ou vérifier le manifeste correspondant ; il ne faut pas demander à l’auteur de synchroniser deux sources. Les composants personnalisés restent associés à leur artefact exact et ne remplacent pas la validation serveur.

### A4 — Démarrage et étape métier de confirmation

La soumission du formulaire initial démarre le flow et crée l’instance. Le stage `draft` de l’exemple est une étape métier ultérieure où le demandeur confirme sa demande. Aucun concept générique de brouillon, aucune persistance avant démarrage et aucun régime spécial lié au nom `draft` ne sont introduits.

Cette étape de confirmation relève d’une tâche de saisie/confirmation, pas nécessairement d’une approbation : l’interdiction d’auto-approbation ne doit pas empêcher le demandeur de confirmer sa propre demande. Sa politique d’affectation à l’initiateur reste à préciser (O4).

### A5 — Permissions métier déclarées par le module

`IWorkflowModule` reçoit un point de déclaration des permissions, avec identifiant et libellé. Le nom proposé est `ConfigurePermissions(WorkflowPermissionRegistryBuilder permissions)` ; les signatures exactes restent à stabiliser.

Le développeur manipule uniquement les permissions métier qu’il déclare, jamais des noms ou identifiants de profils, de groupes LDAP ou de personnes de l’installation. L’administration associe les permissions aux profils/identités. Les permissions locales sont qualifiées par le module et les références inconnues ou dupliquées sont diagnostiquées.

Le builder permet de déclarer les permissions nécessaires au stage/nœud et des exigences distinctes sur ses actions. La combinaison cumulative est proposée : une action exige les permissions du stage ET celles de l’action (P2). L’habilitation ne remplace ni affectation, ni claim, ni séparation des acteurs, ni autorisation de lecture des données.

**Évolution nécessaire :** L5b évalue actuellement des grants exacts `(WorkflowId, DefinitionVersion, NodeId, ActionId)` ; le catalogue de permissions métier et leur articulation avec ces grants n’existent pas encore. Aucune conversion implicite des permissions administratives globales en droits métier n’est autorisée. La portée inter-version et la stratégie de transition sont ouvertes (O2).

### A6 — Pool et désignation depuis l’activité précédente

`.ClaimedByEligibleParticipant()` déclare un pool : à l’activation de la tâche, les personnes éligibles sont notifiées si SMTP est configuré ; une seule peut prendre la tâche par claim atomique. Une notification ne réserve pas la tâche et n’accorde aucun droit.

La désignation explicite se fait sur l’activité précédente, parmi les personnes éligibles à la tâche cible ; elle n’est pas imposée systématiquement au démarrage. Lorsque le formulaire initial est l’activité source, la sélection peut accompagner sa soumission. Le builder doit relier explicitement source, action/branche et tâche cible, et permettre au formulaire standard ou personnalisé de présenter le sélecteur.

L’identité stable sélectionnée est validée côté serveur et persistée avec la progression ; les droits sont revérifiés à l’activation et à la complétion. Le runtime possède déjà un retour `DesignatedAssignments` sur les handlers de complétion ; cela ne constitue pas encore un parcours SDK/UI complet. Le client ne peut pas injecter une affectation arbitraire hors du contrat déclaré.

Le fait qu’une étape ne désigne personne pour la suivante n’est pas un mode d’affectation de cette étape. Aucun `AssignmentMode.None` n’est ajouté. Réaffectation, délégation et gestion automatique des absences ne sont pas impliquées par cette décision.

### A7 — Formulaires standard pour décisions humaines

Le framework fournit des formulaires standard pour les tâches humaines simples d’approbation/décision, avec possibilité de remplacement explicite. L’auteur n’a pas à déclarer un formulaire trivial pour chaque étape. Le SDK résout néanmoins des références durables versionnées de formulaire et de handler de complétion ; le contrat canonique actuel n’est pas rendu nullable par commodité syntaxique.

Une décision automatique avec évaluateur C# reste un nœud `Decision` sans formulaire. Les champs de commentaire, leur obligation, les données affichées et leur visibilité restent à préciser (O5). Le formulaire standard ne reçoit jamais l’état JSON brut en guise de résumé.

## Propositions techniques à valider

- **P1 :** `.HandledBy<THandler>()` au niveau tâche peut remplacer le défaut du formulaire. Aucun choix basé sur le dernier handler enregistré. Une référence CLR ambiguë entre plusieurs registrations doit être qualifiée explicitement.
- **P2 :** exigences de permissions cumulatives stage ET action ; méthodes distinctes pour une éventuelle combinaison « toutes » ou « au moins une ». L’éligibilité au pool signifie être autorisé à au moins une action traitable, sous réserve d’affectation et de séparation des acteurs ; les autres actions restent masquées/refusées. Vérifier l’absence de blocage lorsqu’un claimant ne peut pas réaliser toutes les actions.
- **P3 :** notification produite via une intention durable/outbox, après activation gagnante, distribution bornée et déduplication logique par tâche/activation/destinataire. SMTP reste hors transaction ; l’absence de configuration n’empêche ni la tâche ni son apparition dans l’inbox. Le SMTP ne garantit pas une livraison exactement une fois.
- **P4 :** collecte puis validation globale des déclarations, diagnostics localisés dans le vocabulaire auteur, aperçu déterministe du graphe et des références résolues. Callbacks de portée plutôt que couples `EndField`/`EndStage` ; syntaxe finale encore indicative.

## Points ouverts avant les sous-lots concernés

| ID | Question | Recommandation à soumettre, pas décision acquise |
| --- | --- | --- |
| O1 | Réutiliser une révision antérieure signifie-t-il référencer l’ancien artefact ou redéclarer le contrat inchangé dans la release courante ? | Référence explicite à l’ancien propriétaire ; conserver l’artefact et refuser la double registration. Définir la réutilisation séparément pour formulaire, handler et processus, dont le hash inclut les artefacts. Aucun fallback « dernière version ». |
| O2 | Une permission attribuée vaut-elle automatiquement pour les futures versions et toutes les étapes qui l’exigent ? Les grants L5b restent-ils nécessaires ? | Choisir un modèle unique d’autorisation métier : permissions réutilisables avec contexte serveur exact, ou mapping explicite vers grants exacts. Éviter d’imposer deux configurations redondantes sans motif. Documenter révocation, migration et portée avant modification des stores. |
| O3 | Notifier à l’activation seulement, à la release du claim, ou aussi lors d’un nouveau grant ? Qui reçoit si les permissions diffèrent par action ? | Première portée à l’activation ; destinataires éligibles à au moins une action, pas tous les membres d’un profil. Pas de données sensibles dans les emails. Fixer volume, pagination, retries, annuaire non énumérable et indisponibilité sans perdre la tâche. |
| O4 | Comment la tâche de confirmation revient-elle au demandeur ? | Ajouter une politique explicite d’affectation à l’initiateur, avec les permissions ordinaires ; ne pas utiliser `Approval` ou `None` pour simuler ce comportement. |
| O5 | Quel contenu et quelle validation pour le formulaire standard ? | Actions déclarées, commentaire facultatif configurable par action, résumé autorisé explicite. L’obligation d’un motif de refus est une règle déclarée, jamais une convention cachée. |

La désignation conditionnelle doit être exigée uniquement pour la branche qui active la tâche cible : un refus ne doit pas demander de choisir un manager pour une étape qui ne sera pas exécutée. Une désignation devenue invalide doit produire un diagnostic explicite ; la procédure de récupération sans réaffectation silencieuse doit être décrite. Le contrat d’idempotence doit inclure la sélection du destinataire, même si celle-ci est transportée séparément des champs métier.

## Validation attendue pour L8b

- Release et révisions héritées, override explicite, schéma obligatoire, collisions, qualification, références absentes et compilation déterministe.
- Réutilisation d’une ancienne révision sans substitution de code ; coexistence des artefacts et conservation des anciennes instances et UI.
- Permissions de stage/action et combinaison choisie, profils inconnus du module, révocation, lecture des champs séparée et appels API directs.
- Sélection depuis la tâche précédente : branche conditionnelle, cible invalide, identité arbitraire, retrait de droits, homonymes, double soumission avec destinataire différent, transaction atomique et reprise.
- Pool : candidats filtrés, claim concurrent unique, notifications bornées/retryables, SMTP absent ou en panne, absence de droit conféré par un email.
- Formulaire standard et remplacement, validation serveur, commentaire configuré et refus d’exposition de l’état brut.
- LeaveRequest : formulaire initial → confirmation métier du demandeur → RH → manager désigné depuis l’étape précédente → service de clôture → fin ; refus explicites, aucune boucle ni étape inaccessible.
- Contrats et migrations communs qualifiés sur SQLite/Oracle ; UI qualifiée sur le parcours navigateur. Les tests historiques ne valent pas preuve des nouvelles capacités.
