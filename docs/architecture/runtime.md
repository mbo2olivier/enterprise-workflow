# Modèle et exécution durable

Statut : conventions L2, binding L4a et worker/outbox L4b implémentés selon les ADR 0013 à 0015. Les stores SQLite et Oracle portent le même contrat atomique ; leur qualification L4b est documentée dans la preuve du lot.

## Binding d’exécution L4a

`EnterpriseWorkflow.Runtime.Abstractions` expose deux interfaces pures : `IServiceNodeHandler` et `IDecisionNodeHandler`. Elles reçoivent un `NodeExecutionContext` borné contenant les identités durables d’instance et d’activation, le nœud, le numéro de tentative, l’état complet et la configuration canonique. L’activation reste stable à travers les retries ; la tentative sert au diagnostic et ne doit pas devenir une nouvelle clé d’effet.

Les résultats ne contiennent ni prochain nœud ni délai : succès avec mise à jour d’état facultative, échec retryable ou permanent avec code technique ; une décision réussie fournit en plus son issue nommée. Un succès Service peut joindre des `ExternalEffectIntent` bornées avec opération, destination, type de contenu et JSON canonique. L4b vérifie les transitions et applique seul la politique de retry.

`EnterpriseWorkflow.Runtime` construit un registre immuable de clés globales `(HandlerId, HandlerVersion)`. La composition DI refuse immédiatement un doublon. `WorkflowDefinitionBindingValidator` refuse avant publication une clé absente ou un rôle Service/Decision incompatible. `IScopedWorkflowHandlerResolver` exige ensuite la version exacte et crée un scope distinct par tentative ; aucun fallback vers une autre version ou un autre rôle n’existe.

```csharp
services.AddEnterpriseWorkflowHandlers(registry => registry
    .AddService<PostInvoiceHandler>("finance.post-invoice", 1)
    .AddDecision<RouteInvoiceHandler>("finance.route-invoice", 1));

var registry = serviceProvider.GetRequiredService<IWorkflowHandlerRegistry>();
WorkflowDefinitionBindingValidator.Validate(definition, registry).EnsureValid();
```

L’appel au store de publication vient seulement après cette porte. Le store reste une primitive atomique de bas niveau et ne dépend pas du conteneur DI.

## Définition canonique

Une définition porte `DefinitionId`, version immuable, version de schéma, empreinte normalisée, références des artefacts, métadonnées, nœuds et transitions. Chaque nœud possède un identifiant stable, un type enregistré avec version de contrat et une configuration sérialisable. Chaque transition explicite source, issue et destination.

L’implémentation L2 sérialise les propriétés d’objet JSON, nœuds, transitions, issues et références d’artefacts en ordre ordinal. L’ordre des tableaux métier internes aux configurations est préservé. Les nombres JSON sont réémis depuis leur lexème valide, sans passage par `double` ou `decimal` ; `1`, `1.0` et `1e0` peuvent donc conserver des empreintes différentes. L’empreinte de définition est le SHA-256 minuscule de l’UTF-8 canonique au schéma 1 ; elle reste distincte des empreintes d’artefacts référencées.

Le SDK et le Studio produisent ce modèle ; le moteur ne dépend pas de leur syntaxe. DTO et JSON versionnés aux frontières ; aucune lambda, instance de service ou nom CLR arbitraire n’est un format durable. Une décision référence un évaluateur C# enregistré qui retourne une issue nommée.

Périmètre accepté dans l’ADR 0007 : un point de départ, au moins une fin, graphe acyclique, parcours séquentiel et branches exclusives. Le validateur rejette doublons, références absentes, nœuds inatteignables, cycles, types inconnus et sorties ambiguës. Toute branche doit mener à une fin. Un résultat non déclaré constitue une erreur explicite, pas une transition improvisée.

## Données logiques

| Objet | Identité et contenu | Invariant |
| --- | --- | --- |
| Définition publiée | ID, version, hash, schéma, module/hash | Un contenu ne peut remplacer une version déjà publiée |
| Instance | ID, définition/version, initiateur, état JSON, statut, révision, dates | Lien immuable vers les artefacts utilisés |
| Exécution de nœud | ID d’activation, instance, nœud, état | Identité logique stable à travers les tentatives |
| Tentative | Activation, numéro, dates, erreur qualifiée | Pas de nouvelle clé métier d’effet à chaque retry |
| Travail | ID, activation, échéance, propriétaire, génération, expiration | Une seule possession valide peut committer |
| Tâche humaine | ID, activation, affectation, formulaire/version, révision, résultat | Une complétion gagnante |
| Timer | ID, activation, échéance UTC, statut | Un réveil logique unique |
| Reçu de commande | Portée, clé, empreinte de requête, résultat | Même clé + contenu différent = conflit |
| Audit | Acteur, action, ressource, corrélation, date | Mutation critique et audit validés ensemble |
| Outbox | ID d’opération, destination, contenu, livraison, tentatives | Intention persistée avec la transition |

Les événements externes ne sont pas inclus tant que leur contrat n’est pas défini. Un simple `CurrentNodeId` ne doit pas servir d’unique historique d’exécution.

## États de l’instance

| De | Vers | Déclencheur et préconditions |
| --- | --- | --- |
| Absence | Created | Démarrage autorisé, définition valide ; instance et premier travail atomiques |
| Created | Running | Premier claim valide, ou activation atomique équivalente |
| Running | Running | Transition réussie ou retry programmé ; révision et possession valides |
| Running | Waiting | Création atomique d’une tâche humaine ou d’un timer ; travail précédent clôturé |
| Waiting | Running | Complétion autorisée ou timer arrivé à échéance ; suite persistée une seule fois |
| Running | Completed | Nœud final validé ; aucun travail métier restant |
| Running | Failed | Échec permanent ou retries épuisés |
| Created, Running, Waiting | Cancelled | Commande autorisée ; invalidation des travaux et attentes dans la même transaction |

Completed, Failed et Cancelled sont terminaux au MVP. Pas de reprise manuelle de Failed sans contrat futur explicite. Un échec d’authentification, une indisponibilité SQL ou une erreur d’entrée ne fait pas automatiquement échouer l’instance.

`Cancelled` signifie que le moteur interdit la suite ; il ne prouve pas l’arrêt instantané d’un appel externe en cours. Un CancellationToken est propagé au mieux. Un ancien résultat est rejeté après invalidation, mais un effet déjà réalisé reste possible.

## États des travaux, nœuds et tâches

Travail : Ready → Leased → Done ; Leased → Ready à la reprise après expiration ou au retry programmé. Toute nouvelle possession change la génération. Un travail invalidé est Cancelled. Le renouvellement de bail est conditionnel à la génération courante et à une possession encore valide.

Nœud : Pending → Running → Succeeded, Waiting ou Failed ; Waiting → Succeeded lors de la résolution atomique ; Running → Pending lorsqu’un retry est programmé. L’historique des tentatives reste distinct. Annulation depuis un état non terminal vers Cancelled.

Tâche : Open → Completed ou Cancelled. Un état Claimed et une réaffectation sont différés pour limiter les courses du MVP ; une affectation peut désigner un groupe, dont le premier membre autorisé à committer gagne.

## Transactions et boucle du worker

```mermaid
sequenceDiagram
    participant C as Commande
    participant S as Store
    participant W as Worker
    participant N as Nœud métier
    C->>S: Créer instance + travail + reçu + audit
    S-->>C: Résultat après commit
    W->>S: Réclamer un travail éligible
    S-->>W: Travail + génération + bail + définition/état/tentative
    W->>N: Exécuter hors transaction SQL longue
    N-->>W: Résultat explicite
    W->>S: Commit conditionnel résultat + suite + audit/outbox
    S-->>W: Succès ou conflit de possession
```

Une transaction de commit vérifie statut de l’instance, révision, génération et expiration ; elle met à jour activation et instance, clôture le travail et persiste les suites, attentes et intentions de livraison. Une course perdue ne doit produire aucune écriture partielle.

L’horloge de référence des baux doit être cohérente entre claim, renouvellement et commit. D5 acceptée : temps fourni par le store, à implémenter et tester par provider ; `TimeProvider` pour le moteur et les tests. Ne pas comparer des horloges de workers non synchronisées en prétendant garantir le fencing.

Le worker et le dispatcher sont des `BackgroundService` à concurrence bornée. Le polling SQL reste la source de vérité ; un redémarrage fonctionne sans signal mémoire. Aucun DbContext ni transaction n’est conservé durant un handler ou un appel externe. Le bail métier est renouvelé pendant le handler ; perte du bail ou arrêt du Host annule au mieux le handler et interdit son commit.

Les valeurs par défaut L4b sont concurrence 1, bail 30 s, renouvellement 10 s, polling 250 ms, cinq tentatives, délai exponentiel avec full jitter depuis 1 s et plafond 30 s, timeout cinq minutes. L’outbox utilise concurrence 1, bail 30 s, polling 250 ms, dix tentatives, délai depuis 1 s et plafond une minute. Toutes ces valeurs sont des options de composition validées au démarrage et peuvent être adaptées par chaque entreprise.

## Idempotence et effets externes

- Une activation peut être réexécutée après incident : sémantique au moins une fois.
- Une clé d’effet est stable, par exemple installation + instance + activation + nom de l’opération ; le numéro de tentative n’en fait pas partie.
- La destination doit accepter cette clé ou la duplication doit être tolérée/traitée par l’intégration.
- L’outbox rend l’intention atomique avec le moteur ; le destinataire peut recevoir plusieurs livraisons. Erreur permanente ou tentatives épuisées place l’intention en `Failed` sans modifier l’instance métier.
- Les reçus de démarrage sont scoped par installation, acteur stable et type de commande ; empreinte de la requête et résultat sont conservés. D5 accepte l’absence de purge automatique au MVP initial ; toute expiration future exigera une décision explicite.
- Pour une tâche : requête identique avec même clé rend le résultat connu après contrôle d’accès ; même clé avec autre contenu ou autre décision après clôture rend un conflit. Aucun deuxième réveil.

Un défaut transitoire explicite autorise un retry avec délai progressif, jitter et plafond configurable. Une exception non gérée est permanente ; un timeout est retryable. Erreur permanente ou retries épuisés produisent `Failed`. Un remplacement d’état conserve sa version de schéma jusqu’à la définition d’un mécanisme de migration explicite.

L’outbox couvre les commandes et événements asynchrones. Elle ne couvre pas une requête distante dont la réponse doit déterminer immédiatement la suite : la porte G1a du plan impose de décider corrélation, callback, timeout, sécurité et reprise avant d’introduire ce besoin.

## Courses à résoudre

Complétion contre annulation : la transaction gagnante change la révision ; l’autre recharge et renvoie un conflit sans réveil. Timer contre annulation : même règle. Bail expiré contre ancien worker : le commit tardif échoue même si aucun effet externe ne peut être annulé. Fin d’instance contre livraison outbox : Completed décrit la fin du workflow ; une livraison encore en attente reste visible séparément et n’est pas déclarée livrée.

## Versionnement

Définition, schéma JSON, contrats de nœuds, module et packages ont des versions distinctes. Hash de graphe et hash d’artefact sont tous deux requis. Une ancienne instance ne peut se poursuivre avec du code remplacé sous le même nom. Voir [modules](extensions.md) et ADR 0009.
