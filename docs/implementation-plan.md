# Plan d’implémentation

Statut : plan proposé, sans engagement de calendrier. Le périmètre est confirmé ; chaque mécanisme dépend de son ADR. Les lots L1 à L4a sont **terminés localement** ; le porteur confirme le 1er octobre 2026 la réussite de GitHub Actions pour L1 (run non revérifié indépendamment dans ce dossier). L3b passe sur Oracle Free et sur l’image officielle Oracle Enterprise 19.19 ARM64. Les lots L4b à L11 ne sont pas démarrés.

Les prérequis et arbitrages de L2 sont détaillés dans la [préparation du lot L2](l2-readiness.md). Les décisions D1 à D6 sont acceptées (ADR 0013) ; aucun arbitrage bloquant ne reste pour démarrer L2.

## Portes de décision

G0 — franchie : ADR 0006 à 0008 acceptés, avec retour confirmé au MVP séquentiel sans parallélisme. `global.json` accepte les SDK stables à partir de `10.0.100` dans les feature bands .NET 10 ; les preuves locales utilisent `10.0.102`. GitHub Actions utilise les runners `ubuntu-24.04`, `windows-2025` et `macos-15`. La réponse sur la licence peut attendre la publication.

G1 — franchie pour L4a : contrats L2 cadrés par D1 à D6 ; encodage et atomicité qualifiés en L3 ; identité globale `(HandlerId, HandlerVersion)`, résultats bornés et validation avant publication acceptés dans l’ADR 0014. Les durées de bail/retry et le polling restent à finaliser avant le worker L4b.

G2 — avant sécurité/UI : arrêter contrats de fournisseurs, bootstrap, durée de session/révocation, source des responsables Q11 et mode de rendu UI Q09b. Auto-approbation configurable et interdite par défaut (ADR 0011), Razor/Blazor (ADR 0012), besoin local/distant et mode AD sont déjà résolus. Définir avant L6 les tâches d'approbation, leur affectation, le versionnement de leur politique, la référence durable `FormId/FormVersion`, le contrat des données soumises et la frontière entre validation métier L6 et rendu UI L8.

G3 — avant modules définitifs : trancher coexistence ou drainage à partir du prototype (Q07) ; une incapacité à charger deux versions doit être remontée, pas dissimulée.

G4 — avant diffusion : matrice testée, licence/noms publics, objectifs mesurables Q06, migrations/restauration et connecteurs qualifiés. Pas de revendication de MVP complet sans Oracle et sécurité livrés.

## Lots et dépendances

| Lot | Travail et livrables | Dépendances | Critère de sortie |
| --- | --- | --- | --- |
| L0 | Cadrage, PRD, ADR, architecture, plan et revue des décisions | Aucune | Dossier cohérent, inconnues tracées, mécanismes du lot suivant acceptés |
| L1 — terminé | Solution minimale, Core/Abstractions/SDK, analyseurs et GitHub Actions | G0 | Build verrouillé réussi, références unidirectionnelles testées, exemple C# exécuté ; réussite GitHub confirmée par le porteur |
| L2 — terminé localement | Modèle canonique, validateur, DSL et contrats de store | L1, G1 pour store final | Graphe normalisé et hashé ; erreurs localisées ; table de transitions, déduplication et fencing testés au niveau contrat |
| L3a — terminé localement | Adaptateur SQLite et migrations | L2 | Création/claim/commit/reprise sur base fichier réelle |
| L3b-I — terminé localement | Adaptateur Oracle, mappings, migration et composition Oracle Free légère | L2, Docker | Même suite de conformité réussie sur Oracle Free réel ; différences documentées ; aucun test ignoré présenté comme réussi |
| L3b-Q — terminé localement | Qualification du minimum produit sur l’image officielle Oracle Enterprise 19.19 | L3b-I, accès registre | Migration et six tests d’intégration réussis sur 19.19 ; version et digest consignés dans la preuve |
| L4a — terminé localement | Contrats d'exécution, résultats explicites, contexte borné, registre versionné initial et résolution par scope DI | L2 ; ADR 0014 | Un handler concret est enregistré, validé puis résolu par sa clé durable ; configuration invalide, clé absente ou ambiguë refusée explicitement |
| L4b | Worker, Start/Service/End, retries, idempotence et outbox, utilisant exclusivement le registre L4a | L3a, L3b-I, L4a ; qualification finale avec L3b-Q | Start → Service concret → End ; arrêt brutal puis reprise ; fencing et effet externe simulé dédupliqué |
| L5 | Contrats sécurité, extension locale, AD et exemple API ; administration des capacités | L1, G2 ; L3 pour les stores concrets | Contrats communs ; login AD réel ; comptes locaux hors Core ; endpoints administratifs protégés |
| L6 | HumanTask, décisions exclusives, timer et annulation ; affectation, référence de formulaire versionnée et contrat de soumission sans rendu UI | L4b, L5, G2 | Approbation durable après redémarrage, acteur non autorisé refusé, double soumission contrôlée, courses annulation/timer testées |
| L7a | Prototype loader, versions simultanées, dépendances, alimentation du registre L4a et ressources UI | L2, L4b ; UI Razor/Blazor ; mode de rendu Q09b | Rapport prouvant coexistence ou recommandant drainage ; absence de collision silencieuse entre handlers ; décision G3 |
| L7b | Kernel, loader et registre définitifs | L7a, G3, L5 | Nouveau module chargé au redémarrage ; retrait incompatible bloqué |
| L8 | UI métier, schémas et rendu des formulaires, composants Razor personnalisés, thèmes et administration complète | L6, L7b, G2 | Parcours congé sur les deux bases ; formulaire/version de L6 rendu et validé côté serveur ; droits par ressource et capacités fournisseur respectés |
| L9 | CLI, templates, pack et exemple embedded | L7b, L8 | Depuis copie propre : génération, build, validation, package, chargement |
| L10 | Qualification, distribution et runbooks | L3b-Q, L9, G4 | Matrice publiée sur preuves, restauration exécutée, artefacts vérifiés ; MVP livrable |
| L11 | Studio initial et export C# | MVP, choix bibliothèque/Q09b | Export compilable et modèle équivalent ; absent du Host production |

L’audit et l’instrumentation commencent avec L3/L4 ; ils ne sont pas reportés à L10. Le prototype Oracle doit commencer dès L2 pour détecter une divergence avant de stabiliser le contrat SQLite. Le prototype des modules précède la stabilisation des API de plugins.

## Binding des comportements

Le DSL persistant ne sérialise ni type CLR, ni lambda, ni instance de service. Une référence de handler est une clé durable et versionnée. Le binding est livré en trois étapes distinctes :

1. **L2 — description seulement** : le builder conserve la clé et le compilateur en vérifie uniquement la forme. `NodeTypeCatalog` décrit les types de nœuds et valide leur configuration ; il ne résout aucun comportement métier.
2. **L4a — binding runtime explicite** : une application enregistre directement une classe concrète sous une clé de handler. À la publication ou au démarrage, le registre refuse les références absentes, incompatibles ou ambiguës. Pour chaque tentative, le worker ouvre un scope DI, résout le handler exact, lui fournit un contexte borné et consomme un résultat explicite.
3. **L7 — binding par module** : le loader découvre les déclarations approuvées, enregistre leurs services avant la construction du Host et alimente le même registre. Le chargement dynamique ne change pas la sémantique d'exécution établie en L4a.

L’ADR 0014 retient une clé globale `(HandlerId, HandlerVersion)`. Les applications et futurs modules qualifient leurs noms par domaine ; deux registrations sous une même clé sont refusées pendant la composition. Les résultats distinguent succès, échec retryable et échec permanent ; une décision réussie ajoute une issue déclarée. Le registre est figé après construction, les définitions exécutables sont validées avant publication et la résolution n’applique aucun fallback de version ou de rôle.

## Tâches humaines et formulaires

Une tâche humaine n'est pas un `Service` portant un champ JSON `form`. L6 ajoute un type de nœud et un DSL `HumanTask` explicites, avec affectation, issues autorisées, référence durable `FormId/FormVersion`, révision, données de soumission bornées et commande de complétion autorisée/idempotente. Son activation persiste une attente et ne conserve ni transaction ni worker pendant l'interaction humaine.

L6 définit la sémantique durable et la validation métier indépendante de l'UI. L8 ajoute les schémas déclaratifs de champs, leur rendu Razor/Blazor, la validation serveur de présentation, l'inbox, les pages de tâche et l'échappatoire vers un composant Razor personnalisé. Une ancienne instance continue à référencer la version exacte du formulaire avec laquelle elle a été créée.

## Samples progressifs

Chaque sample doit annoncer la capacité qu'il démontre et ne pas simuler une fonctionnalité future avec une configuration JSON sans sémantique :

- L2 : `DefinitionOnly`, construction et validation uniquement, avec handlers fictifs explicitement signalés comme non résolus ; aucun formulaire ni approbation humaine prétendus ;
- L4b : `ExecutableWorkflow`, classe de handler concrète, enregistrement DI, résolution par clé, exécution durable Start → Service → End et reprise ;
- L6 : `HumanApproval`, attente durable, affectation et complétion sécurisée sans dépendre encore d'un rendu graphique complet ;
- L8 : `LeaveRequest`, formulaire de démarrage, inbox, approbation/refus et notification dans le parcours UI complet.

Le sample courant de demande de congé doit être renommé/réécrit au prochain changement de code : sa propriété JSON `form` n'a actuellement aucune sémantique et sa `Decision` représente une évaluation automatique, pas une approbation humaine.

## Tranches démontrables

T1 — tranche technique durable : L1, L2, L3a, L4a et L4b, Start → Service concret résolu par le registre → End. Fournir commandes de reproduction, mapping clé/classe, état avant/après arrêt et tests. Oracle est une qualification distincte à terminer, pas une raison de présenter T1 comme MVP complet.

T2 — tranche métier : L5, L6 et UI minimale de L8 ; demande, formulaire versionné, approbation après redémarrage et double clic. Cette UI peut utiliser un enregistrement direct L4a avant le loader complet pour tester les contrats.

T3 — produit modulaire : L7 à L10 ; module packagé, administration selon fournisseur, exemple embedded, SQLite et Oracle, distributions testées. Cette tranche atteint le MVP lorsque tous ses critères sont satisfaits.

T4 — Studio : L11, sans changer la sémantique du moteur pour contourner les limites du designer.

## Structure initiale proposée

```text
src/EnterpriseWorkflow.Abstractions/
src/EnterpriseWorkflow.Core/
src/EnterpriseWorkflow.Sdk/
src/EnterpriseWorkflow.Persistence.Abstractions/
tests/EnterpriseWorkflow.Core.Tests/
samples/MinimalWorkflow/
docs/
```

Ajouter Runtime, adaptateurs, sécurité et Host au fil des lots ; ne pas créer d’emblée des dizaines de projets vides. L2 ajoute uniquement `Persistence.Abstractions` aux trois bibliothèques initiales ; aucun projet EF ou provider vide n’est anticipé.

## Risques et réponses

| Risque | Réponse | Décision/proof avant |
| --- | --- | --- |
| Divergence SQLite/Oracle | Suite de conformité identique sur Oracle Free, puis répétition sur le minimum 19.19 | L3b-I puis L3b-Q |
| Code ancien indisponible | Artefacts immuables, coexistence prouvée ou drainage | L7b |
| Handler textuel sans implémentation ou collision de clé | Identité arrêtée avant L4a, validation contre registre et refus des doublons | L4a |
| Sample présentant une capacité non exécutable | Samples progressifs nommés par capacité et critères de sortie exécutés | Chaque lot |
| AD non portable ou politiques incompatibles | Prototype sur plateformes cibles et domaine de test | L5 terminé |
| Extension locale couplée au Core | Tests de références et fonctionnement sans comptes locaux | L5 terminé |
| UI externe ou assets Razor non chargeables | Prototype ressources et contrat UI | L7b/L8 |
| MVP annoncé sur seule base SQLite | Gate de qualification Oracle et tests end-to-end | L10 |
| Charge cible inconnue | Mesures reproductibles puis seuils convenus | G4 |
| Multiplication des distributions | Matrice explicite avec niveaux de qualification | G4 |

## Terminé pour chaque lot

Code compilable, tests pertinents exécutés, commandes de reproduction, documentation mise à jour et limites connues. Tout sample distingue explicitement définition, exécution, interaction humaine et rendu UI ; il ne présente pas une référence textuelle ou un JSON libre comme une fonctionnalité opérationnelle. Les tests non exécutés sont signalés avec motif. Toute nouvelle ambiguïté structurante est remontée avant de poursuivre la partie concernée.
