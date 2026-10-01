# Plan d’implémentation

Statut : plan proposé, sans engagement de calendrier. Le périmètre est confirmé ; chaque mécanisme dépend de son ADR. Le lot L1 est **terminé** ; le porteur confirme le 1er octobre 2026 la réussite de GitHub Actions (run non revérifié indépendamment dans ce dossier). Les lots L2 à L11 ne sont pas démarrés.

Les prérequis et arbitrages de L2 sont détaillés dans la [préparation du lot L2](l2-readiness.md). Les décisions D1 à D6 sont acceptées (ADR 0013) ; aucun arbitrage bloquant ne reste pour démarrer L2.

## Portes de décision

G0 — franchie : ADR 0006 à 0008 acceptés, avec retour confirmé au MVP séquentiel sans parallélisme. Le SDK 10.0.102 est épinglé. GitHub Actions utilise les runners `ubuntu-24.04`, `windows-2025` et `macos-15`. La réponse sur la licence peut attendre la publication.

G1 — contrats L2 cadrés par D1 à D6 : limites, déduplication sans purge automatique et temps UTC à la milliseconde acceptés. Encodage SQL et atomicité à qualifier en L3 ; durées de bail/retry et polling à finaliser en L4. Aucune instance Oracle disponible ; préparation Docker Compose autorisée pour les tests. Le provisionnement 19c reste à réaliser et ne bloque pas le modèle/DSL.

G2 — avant sécurité/UI : arrêter contrats de fournisseurs, bootstrap, durée de session/révocation, source des responsables Q11 et mode de rendu UI Q09b. Auto-approbation configurable et interdite par défaut (ADR 0011), Razor/Blazor (ADR 0012), besoin local/distant et mode AD sont déjà résolus. Définir au contrat les tâches d’approbation et le versionnement de leur politique avant L6.

G3 — avant modules définitifs : trancher coexistence ou drainage à partir du prototype (Q07) ; une incapacité à charger deux versions doit être remontée, pas dissimulée.

G4 — avant diffusion : matrice testée, licence/noms publics, objectifs mesurables Q06, migrations/restauration et connecteurs qualifiés. Pas de revendication de MVP complet sans Oracle et sécurité livrés.

## Lots et dépendances

| Lot | Travail et livrables | Dépendances | Critère de sortie |
| --- | --- | --- | --- |
| L0 | Cadrage, PRD, ADR, architecture, plan et revue des décisions | Aucune | Dossier cohérent, inconnues tracées, mécanismes du lot suivant acceptés |
| L1 — terminé | Solution minimale, Core/Abstractions/SDK, analyseurs et GitHub Actions | G0 | Build verrouillé réussi, références unidirectionnelles testées, exemple C# exécuté ; réussite GitHub confirmée par le porteur |
| L2 | Modèle canonique, validateur, DSL et contrats de store | L1, G1 pour store final | Graphe normalisé ; erreurs localisées ; table de transitions et contrats testés |
| L3a | Adaptateur SQLite et migrations | L2 | Création/claim/commit/reprise sur base fichier réelle |
| L3b | Prototype puis adaptateur Oracle 19c, mappings et migrations | L2, accès Oracle | Même suite de conformité ; différences documentées ; aucun test ignoré présenté comme réussi |
| L4 | Worker, Start/Service/End, retries, idempotence et outbox | L3a ; qualification finale avec L3b | Arrêt brutal puis reprise ; fencing et effet externe simulé dédupliqué |
| L5 | Contrats sécurité, extension locale, AD et exemple API ; administration des capacités | L1, G2 ; L3 pour les stores concrets | Contrats communs ; login AD réel ; comptes locaux hors Core ; endpoints administratifs protégés |
| L6 | HumanTask, décisions exclusives, timer et annulation | L4, L5 | Approbation durable, double soumission contrôlée, courses annulation/timer testées |
| L7a | Prototype loader, versions simultanées, dépendances et ressources UI | L2, L4 ; UI Razor/Blazor ; mode de rendu Q09b | Rapport prouvant coexistence ou recommandant drainage ; décision G3 |
| L7b | Kernel, loader et registre définitifs | L7a, G3, L5 | Nouveau module chargé au redémarrage ; retrait incompatible bloqué |
| L8 | UI métier, formulaires, thèmes et administration complète | L6, L7b, G2 | Parcours congé sur les deux bases, droits par ressource, capacités fournisseur respectées |
| L9 | CLI, templates, pack et exemple embedded | L7b, L8 | Depuis copie propre : génération, build, validation, package, chargement |
| L10 | Qualification, distribution et runbooks | L3b, L9, G4 | Matrice publiée sur preuves, restauration exécutée, artefacts vérifiés ; MVP livrable |
| L11 | Studio initial et export C# | MVP, choix bibliothèque/Q09b | Export compilable et modèle équivalent ; absent du Host production |

L’audit et l’instrumentation commencent avec L3/L4 ; ils ne sont pas reportés à L10. Le prototype Oracle doit commencer dès L2 pour détecter une divergence avant de stabiliser le contrat SQLite. Le prototype des modules précède la stabilisation des API de plugins.

## Tranches démontrables

T1 — tranche technique durable : L1, L2, L3a et L4, Start → Service simulé → End. Fournir commandes de reproduction, état avant/après arrêt et tests. Oracle est une qualification distincte à terminer, pas une raison de présenter T1 comme MVP complet.

T2 — tranche métier : L5, L6 et UI minimale de L8 ; demande, approbation après redémarrage et double clic. Cette UI peut précéder le loader complet pour tester les contrats.

T3 — produit modulaire : L7 à L10 ; module packagé, administration selon fournisseur, exemple embedded, SQLite et Oracle, distributions testées. Cette tranche atteint le MVP lorsque tous ses critères sont satisfaits.

T4 — Studio : L11, sans changer la sémantique du moteur pour contourner les limites du designer.

## Structure initiale proposée

```text
src/EnterpriseWorkflow.Abstractions/
src/EnterpriseWorkflow.Core/
src/EnterpriseWorkflow.Sdk/
tests/EnterpriseWorkflow.Core.Tests/
samples/MinimalWorkflow/
docs/
```

Ajouter Runtime, store, adaptateurs, sécurité et Host au fil des lots ; ne pas créer d’emblée des dizaines de projets vides. Le lot L1 livre uniquement les projets indiqués ci-dessus, le test d’architecture et l’exemple minimal.

## Risques et réponses

| Risque | Réponse | Décision/proof avant |
| --- | --- | --- |
| Divergence SQLite/Oracle | Suite de conformité identique, prototype Oracle tôt | L3b |
| Code ancien indisponible | Artefacts immuables, coexistence prouvée ou drainage | L7b |
| AD non portable ou politiques incompatibles | Prototype sur plateformes cibles et domaine de test | L5 terminé |
| Extension locale couplée au Core | Tests de références et fonctionnement sans comptes locaux | L5 terminé |
| UI externe ou assets Razor non chargeables | Prototype ressources et contrat UI | L7b/L8 |
| MVP annoncé sur seule base SQLite | Gate de qualification Oracle et tests end-to-end | L10 |
| Charge cible inconnue | Mesures reproductibles puis seuils convenus | G4 |
| Multiplication des distributions | Matrice explicite avec niveaux de qualification | G4 |

## Terminé pour chaque lot

Code compilable, tests pertinents exécutés, commandes de reproduction, documentation mise à jour et limites connues. Les tests non exécutés sont signalés avec motif. Toute nouvelle ambiguïté structurante est remontée avant de poursuivre la partie concernée.
