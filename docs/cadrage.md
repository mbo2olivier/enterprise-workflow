# Cadrage et registre des décisions

Date : 30 septembre 2026. Statut : exigences consolidées ; architecture proposée à relire.

## Autorité des sources

S1 : [document fourni et archivé](references/README.md), daté du 30 septembre 2026, synthèse d’une discussion dont la réponse architecturale initiale n’a été récupérée que partiellement. S2 : demande actuelle de documentation préalable et choix du nom Enterprise Workflow. S3 : réponses du porteur aux cinq questions de cadrage. S4 : précisions ultérieures sur Oracle 19c minimum, sécurité par extensions, AD avec identifiant/mot de passe et support distinct par adaptateur. S5 : réponse « 1A, 2A », validant l’auto-approbation configurable et interdite par défaut, ainsi que Razor/Blazor avec formulaires déclaratifs et composants personnalisés.

S6 : acceptation des ADR 0006 et 0008, choix GitHub/GitHub Actions, puis confirmation de la proposition initiale de l’ADR 0007 après examen du parallélisme. Le parallélisme reste différé ; GitLab sera envisagé ultérieurement.

S7 : validation de L2-D1 à L2-D6 le 1er octobre 2026 ; aucune instance Oracle disponible, Docker installé et environnement de test Docker Compose autorisé. Voir [ADR 0013](adr/0013-contrats-l2.md).

S8 : validation des recommandations L4a 1A, 2A et 3A le 1er octobre 2026 : clé globale identifiant/version, résultats bornés sans contrôle de transition et registre immuable validé avant publication. Voir [ADR 0014](adr/0014-binding-handlers-l4a.md).

S2 à S8 font autorité sur les propositions de S1. Les instructions d’implémentation de S1 ne constituent pas une demande de coder maintenant. Les recommandations non validées restent proposées.

Le dépôt était vide et sans premier commit. Aucun AGENTS.md n’a été trouvé dans le dépôt ni dans les emplacements parents inspectés. SDK local observé : `10.0.102` ; cela ne fixe pas le patch à épingler pour la future CI.

## Décisions confirmées

| ID | Décision du porteur | Source | ADR |
| --- | --- | --- | --- |
| D00 | Nom officiel Enterprise Workflow | S2 | Convention documentaire |
| D01 | .NET 10, Windows/Linux/macOS, compilation depuis les sources et intention de binaires précompilés | S3.1 | [0001](adr/0001-dotnet-et-plateformes.md) |
| D02 | SQLite et Oracle prioritaires dans le MVP | S3.2 | [0002](adr/0002-persistance.md) |
| D03 | Administration intégrée, authentification et autorisation extensibles ; AD et API tierce | S3.3 | [0003](adr/0003-securite-extensible.md) |
| D04 | MVP moteur durable, tâches humaines, formulaires simples, Kernel et CLI ; Studio ensuite | S3.4 | [0004](adr/0004-perimetre-mvp.md) |
| D05 | Une installation par organisation ; réutilisation des modules par déploiement indépendant | S3.5 | [0005](adr/0005-isolation-organisation.md) |
| D06 | Auto-approbation configurable par workflow, interdite par défaut | S5.1A | [0011](adr/0011-auto-approbation.md) |
| D07 | UI métier et administration Razor/Blazor ; formulaires déclaratifs et composants Razor personnalisés | S5.2A | [0012](adr/0012-ui-razor-blazor.md) |
| D08 | Handler global identifiant/version, résultats bornés et validation avant publication | S8.1A–3A | [0014](adr/0014-binding-handlers-l4a.md) |

Décisions S6 : ADR 0006, 0007 et 0008 acceptés ; MVP séquentiel à décisions exclusives, sans boucles ni parallélisme ; dépôt GitHub et CI GitHub Actions. La proposition de parallélisme Q12 est retirée et n’est plus un arbitrage bloquant.

Le porteur emploie Runtime pour désigner l’application d’hébergement. La nomenclature proposée distingue le moteur bibliothèque (Runtime) de l’exécutable d’accueil (Kernel/Host), afin de préserver l’intégration embarquée.

## Clarifications résolues pendant la rédaction

| ID | Réponse du porteur (S4/S5) | Conséquence |
| --- | --- | --- |
| Q01 | Oracle à partir de 19c | 19c est le minimum produit ; chaque version supérieure doit être qualifiée |
| Q02 | Local ou distant selon les extensions installées | Aucun magasin d’identifiants imposé au cœur ; administration adaptée aux capacités du fournisseur |
| Q03 | AD par identifiant et mot de passe | Connecteur de vérification auprès du contrôleur ; SSO non requis au MVP |
| Q04 | Support distinct par adaptateur accepté | Win x86 et macOS Intel avec SQLite ; Oracle seulement sur plateformes qualifiées |

Q05 est résolue pour l’auto-approbation par l’ADR 0011 ; Q09 est résolue pour la technologie UI par l’ADR 0012. La délégation reste hors de cet arbitrage ; la bibliothèque graphique et l’outillage frontend restent ouverts sous Q09b.

Ces décisions sont intégrées aux ADR 0001 à 0003, 0011 et 0012. Le protocole sécurisé exact, la bibliothèque LDAP, les contrats techniques et leurs capacités restent des propositions d’implémentation.

## Décisions ultérieures

| ID | Décision ouverte | Échéance |
| --- | --- | --- |
| Q06 | Volumes, latence, disponibilité, durée des instances, rétention et RPO/RTO | Avant dimensionnement et qualification de production |
| Q07 | Coexistence des versions de DLL ou drainage | Avant loader définitif ; ADR 0009 proposé |
| Q08 | Licence, gouvernance et identifiants NuGet/CLI | Avant publication |
| Q09b | Mode de rendu Razor/Blazor, bibliothèque graphique du Studio et outillage frontend de build | Mode de rendu avant UI ; bibliothèque avant Studio |
| Q10 | Résolue pour L1 : SDK minimal 10.0.100 avec roll-forward vers les feature bands stables .NET 10 ; preuves locales sur 10.0.102 ; runners `ubuntu-24.04`, `windows-2025`, `macos-15` ; GitHub Actions | À revoir lors de la qualification de distribution L10 |
| Q11 | Source des responsables et mapping des groupes | Avant affectation corporate |

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

L1 à L4a sont implémentés localement. SQLite et Oracle 19.19 sont qualifiés pour le périmètre L3 ; L4a fournit les contrats, le registre, la validation avant publication et la résolution scoped. Le worker L4b et les fonctions métier ultérieures ne sont pas livrés.
