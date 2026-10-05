# Cadrage et registre des décisions

Date : 30 septembre 2026. Statut : exigences consolidées ; architecture proposée à relire.

## Autorité des sources

S1 : [document fourni et archivé](references/README.md), daté du 30 septembre 2026, synthèse d’une discussion dont la réponse architecturale initiale n’a été récupérée que partiellement. S2 : demande actuelle de documentation préalable et choix du nom Enterprise Workflow. S3 : réponses du porteur aux cinq questions de cadrage. S4 : précisions ultérieures sur Oracle 19c minimum, sécurité par extensions, AD avec identifiant/mot de passe et support distinct par adaptateur. S5 : réponse « 1A, 2A », validant l’auto-approbation configurable et interdite par défaut, ainsi que Razor/Blazor avec formulaires déclaratifs et composants personnalisés.

S6 : acceptation des ADR 0006 et 0008, choix GitHub/GitHub Actions, puis confirmation de la proposition initiale de l’ADR 0007 après examen du parallélisme. Le parallélisme reste différé ; GitLab sera envisagé ultérieurement.

S7 : validation de L2-D1 à L2-D6 le 1er octobre 2026 ; aucune instance Oracle disponible, Docker installé et environnement de test Docker Compose autorisé. Voir [ADR 0013](adr/0013-contrats-l2.md).

S8 : validation des recommandations L4a 1A, 2A et 3A le 1er octobre 2026 : clé globale identifiant/version, résultats bornés sans contrôle de transition et registre immuable validé avant publication. Voir [ADR 0014](adr/0014-binding-handlers-l4a.md).

S9 : validation le 3 octobre 2026 des recommandations L4b 1A à 5A puis 6A : snapshot atomique au claim, politique entièrement configurable, exceptions inattendues permanentes, intentions outbox atomiques avec dead letter bornée, conservation de version d’état et portes futures explicites pour attente/callback et migration d’état. Voir [ADR 0015](adr/0015-worker-retry-outbox-l4b.md).

S10 : validation le 3 octobre 2026 des choix sécurité L5 1A à 5A, avec politique de sécurité entièrement configurable, profils internes associables directement aux identités ou aux groupes AD facultatifs, et scission de G2 en sécurité, contrats métier et rendu UI. Voir [ADR 0016](adr/0016-securite-profils-l5.md). Le mode précis de rendu et la source des responsables ne sont pas sélectionnés par cet accord.

S11 : décision le 3 octobre 2026 de désigner explicitement l’approbateur au démarrage ou lors de l’activité concernée, parmi les identités habilitées par les profils internes. Q11 et l’arbitrage sur la source du responsable sont résolus ; les autres contrats G2a restent à formaliser. Voir [ADR 0017](adr/0017-designation-approbateur.md).

S12 : recadrage du 3 octobre 2026 : compatibilité LDAP indépendante d’AD et habilitations de stages par workflow/nœud/action, à prioriser avant L6. Voir ADR 0018/0019 et [arbitrages R1–R4](spec-ldap-stages.md).

S13 : précision ultérieure du porteur : les nœuds sont les stages dans l’administration. Aucune entité stage supplémentaire.

S14 : confirmation par le porteur le 4 octobre 2026 de la réussite du job Linux GitHub Actions qualifiant l’extension sur OpenLDAP réel en LDAPS.

S15 : validation le 4 octobre 2026 de R2-A, R3-A, R5-A, R6-A et R7-A pour L5b : modes d'affectation explicites, grants exacts positifs et révocables, catalogue typé, séparation L5b/L6 et lecture séparée des actions de stage.

S16 : validation le 4 octobre 2026 de D1-B et D2-A à D7-A pour L6 : correction directe du schéma canonique v1 avant le premier tag officiel, tâches et timers typés, cycles d'affectation/claim explicites, formulaire/handler versionnés, reçu idempotent lié à l'acteur et au contenu, timer fondé sur l'horloge du store, séparation d'acteurs versionnée et inbox bornée. Voir [ADR 0020](adr/0020-attentes-metier-l6.md).

S17 : confirmation par le porteur le 5 octobre 2026 de G3-A pour L7b : coexistence nominale des versions de modules, rétention de l'artefact exact et retrait bloqué tant qu'il reste référencé. Voir ADR 0009/0021.

S18 : validation le 5 octobre 2026 de M1-A et M2-A pour L7b : migrations d’état explicites et atomiques fournies par l’artefact exact, inventaire durable des modules et retrait bloqué tant qu’une définition persistée référence l’artefact. La porte historique G3a est renommée G3-M afin de ne plus la confondre avec le choix G3-A. Voir ADR 0022.

S2 à S15 font autorité sur les propositions de S1. Les instructions d’implémentation de S1 ne constituent pas une demande de coder maintenant. Les recommandations non validées restent proposées.

Le dépôt était vide et sans premier commit. Aucun AGENTS.md n’a été trouvé dans le dépôt ni dans les emplacements parents inspectés. SDK local observé : `10.0.102` ; cela ne fixe pas le patch à épingler pour la future CI.

## Décisions confirmées

| ID | Décision du porteur | Source | ADR |
| --- | --- | --- | --- |
| D00 | Nom officiel Enterprise Workflow | S2 | Convention documentaire |
| D01 | .NET 10, Windows/Linux/macOS, compilation depuis les sources et intention de binaires précompilés | S3.1 | [0001](adr/0001-dotnet-et-plateformes.md) |
| D02 | SQLite et Oracle prioritaires dans le MVP | S3.2 | [0002](adr/0002-persistance.md) |
| D03 | Administration intégrée, authentification et autorisation extensibles ; LDAP et API tierce | S3.3, S12 | [0003](adr/0003-securite-extensible.md) |
| D04 | MVP moteur durable, tâches humaines, formulaires simples, Kernel et CLI ; Studio ensuite | S3.4 | [0004](adr/0004-perimetre-mvp.md) |
| D05 | Une installation par organisation ; réutilisation des modules par déploiement indépendant | S3.5 | [0005](adr/0005-isolation-organisation.md) |
| D06 | Auto-approbation configurable par workflow, interdite par défaut | S5.1A | [0011](adr/0011-auto-approbation.md) |
| D07 | UI métier et administration Razor/Blazor ; formulaires déclaratifs et composants Razor personnalisés | S5.2A | [0012](adr/0012-ui-razor-blazor.md) |
| D09 | Extension LDAP indépendante d’AD ; stage = nœud et habilitations par workflow/nœud/action | S12/S13 | [0018](adr/0018-ldap-generique.md), [0019](adr/0019-workflow-stages.md) |
| D08 | Handler global identifiant/version, résultats bornés et validation avant publication | S8.1A–3A | [0014](adr/0014-binding-handlers-l4a.md) |

Décisions S6 : ADR 0006, 0007 et 0008 acceptés ; MVP séquentiel à décisions exclusives, sans boucles ni parallélisme ; dépôt GitHub et CI GitHub Actions. La proposition de parallélisme Q12 est retirée et n’est plus un arbitrage bloquant.

Le porteur emploie Runtime pour désigner l’application d’hébergement. La nomenclature proposée distingue le moteur bibliothèque (Runtime) de l’exécutable d’accueil (Kernel/Host), afin de préserver l’intégration embarquée.

## Clarifications résolues pendant la rédaction

| ID | Réponse du porteur (S4/S5) | Conséquence |
| --- | --- | --- |
| Q01 | Oracle à partir de 19c | 19c est le minimum produit ; chaque version supérieure doit être qualifiée |
| Q02 | Local ou distant selon les extensions installées | Aucun magasin d’identifiants imposé au cœur ; administration adaptée aux capacités du fournisseur |
| Q03 | LDAP par identifiant et mot de passe (S12 remplace AD obligatoire) | Extension générique ; annuaire non AD réel pour qualification ; SSO non requis au MVP |
| Q04 | Support distinct par adaptateur accepté | Win x86 et macOS Intel avec SQLite ; Oracle seulement sur plateformes qualifiées |

Q05 est résolue pour l’auto-approbation par l’ADR 0011 ; Q09 est résolue pour la technologie UI par l’ADR 0012. La délégation reste hors de cet arbitrage ; la bibliothèque graphique et l’outillage frontend restent ouverts sous Q09b.

Ces décisions sont intégrées aux ADR 0001 à 0003, 0011 et 0012. L’ADR 0016 accepte la composition des fournisseurs, le bootstrap, les sessions configurables et les profils internes. L’ADR 0018 précise LDAPS par défaut et le clair sur sélection explicite. Le connecteur générique fondé sur System.DirectoryServices.Protocols est livré en L5a ; les nouveaux contrats d’habilitation restent prévus en L5b.

Les arbitrages R1–R4 du [recadrage](spec-ldap-stages.md) complètent les questions ci-dessous, avec échéance avant les lots concernés.

## Décisions ultérieures

| ID | Décision ouverte | Échéance |
| --- | --- | --- |
| Q06 | Volumes, latence, disponibilité, durée des instances, rétention et RPO/RTO | Avant dimensionnement et qualification de production |
| Q07 | Résolue : G3-A retient la coexistence nominale, la rétention de l'artefact exact et le retrait bloqué s'il reste référencé | Franchie pour L7b ; ADR 0009/0021 |
| Q08 | Licence, gouvernance et identifiants NuGet/CLI | Avant publication |
| Q09b | Mode de rendu résolu par D1-A/D2-A : Blazor Web App, SSR statique par défaut, Interactive Server ciblé et ressources de modules manifestées ; bibliothèque Studio encore ouverte | Rendu franchi pour L7a/L8 ; bibliothèque avant Studio |
| Q10 | Résolue pour L1 : SDK minimal 10.0.100 avec roll-forward vers les feature bands stables .NET 10 ; preuves locales sur 10.0.102 ; runners `ubuntu-24.04`, `windows-2025`, `macos-15` ; GitHub Actions | À revoir lors de la qualification de distribution L10 |
| Q11 | Résolue : approbateur explicitement choisi parmi les identités habilitées par profils internes (ADR 0017), sans attribut AD manager obligatoire | Arbitrage responsable franchi ; réalisation L5/L6/L8 |

Les ADR 0006 à 0008 sont acceptés. Les autres mécanismes non explicitement validés restent des propositions, sans acceptation par défaut.

## Risques de conception

- Portabilité .NET, support d’un provider et portabilité d’un module sont trois qualifications distinctes.
- Administration intégrée ne signifie pas automatiquement stockage local des mots de passe.
- Une organisation par installation conserve des permissions par utilisateur et ressource.
- Copier un module ne copie pas ses secrets, identités, politiques ni instances.
- Une reprise exige la définition et le code exacts, pas uniquement le graphe.
- SQLite et Oracle doivent chacun passer les tests d’atomicité sur une vraie base.
- Une outbox ou une transaction SQL ne garantit pas l’unicité d’un effet externe.

## État

L1 à L4b sont implémentés localement. SQLite et Oracle 19.19 sont qualifiés pour le worker séquentiel et l’outbox L4b ; le socle sécurité L5 est implémenté et qualifié localement. LDAP générique L5a est terminé : qualification OpenLDAP locale en clair explicite et OpenLDAP/LDAPS sur Linux GitHub Actions acquises. Les habilitations exactes par nœud/action L5b sont implémentées et qualifiées sur SQLite et Oracle 19.19 ; tâches humaines, affectation/claim durables, fonctions métier, modules et UI restent à réaliser.
