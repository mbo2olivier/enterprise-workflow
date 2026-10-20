# Preuve locale L8b — SDK fluent et parcours métier

Date : 20 octobre 2026. Hôte local macOS arm64, SDK .NET `10.0.401`.

Statut : implémentation et vérifications légères locales livrées ; qualification externe restante dans [l8b-validation.md](../l8b-validation.md). Aucune exécution Oracle, LDAP réel ou SMTP réel n’est revendiquée ici. Le smoke navigateur Chromium a ensuite été exécuté sur macOS, comme détaillé ci-dessous.

## Vérifications exécutées

- Restauration des dépendances et mise à jour des lock files pour les nouvelles références de projets.
- Compilation Debug de la solution avec `--no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1` : zéro erreur, zéro avertissement.
- Tests en Debug via `dotnet test <projet> --no-build --no-restore -v:minimal` :

| Suite | Réussis | Échecs |
| --- | ---: | ---: |
| Core | 58 | 0 |
| Extensions | 7 | 0 |
| Runtime | 17 | 0 |
| Presentation | 5 | 0 |
| Persistence.Sqlite | 9 | 0 |
| Security.Persistence.Sqlite | 7 | 0 |
| Security | 11 | 0 |
| Web | 2 | 0 |
| Total | 116 | 0 |

Le runner nécessite un canal IPC local : les tests ont été exécutés hors sandbox. Le test LDAP de la suite Security ne se connecte pas sans activation explicite ; les 11 résultats ne valent donc pas preuve LDAP. Le transport de messagerie est simulé dans les tests locaux.

## Invariants vérifiés

Métadonnées et révisions distinctes ; IDs qualifiés ; schéma initial obligatoire ; déclaration indépendante de l’ordre ; snapshot de processus immuable ; réexposition d’un processus ancien sans reconstruction ; validation des politiques et références ; exigences stage ET action avec groupe OU ; permissions communes aux versions et maintien du fournisseur historique.

Le test runtime L8b utilise une base SQLite réelle : confirmation affectée à l’initiateur, pool et claim, désignation conditionnelle, commentaire requis, identité différente sous une même clé refusée, replay identique, redémarrage et candidat révoqué laissé en attente, récupération explicite. La création atomique d’un message enfant avec l’acquittement du parent est également vérifiée.

Le sample est compilé, packagé avec le manifeste généré et chargé par le vrai loader dans les tests Extensions. Les tests Presentation vérifient le fan-out paginé, le retry d’un email avec clé stable, la révocation avant livraison et SMTP absent.

## Portée restante

Oracle 19.19, LDAP/LDAPS paginé, livraison SMTP, parcours complet navigateur et matrice CI restent à exécuter selon la procédure dédiée. Le smoke Playwright existant ne couvre pas à lui seul la totalité du nouveau parcours LeaveRequest. Les tests Oracle ont été étendus pour le nouveau contrat ; leur compilation est vérifiée séparément, leur exécution exige le schéma jetable.

## Correction et exécution du smoke navigateur — 20 octobre 2026

L’échec de `PortalAndAdministrationAreQualifiedOnSqlite` sur `.workspace` a été reproduit sur Chromium macOS arm64. Le diagnostic temporaire montrait le déplacement à `(600, 300)` alors que `document.readyState` valait encore `loading` après le POST de connexion. Le nouveau document affichait déjà le contenu SSR, mais le script n’avait pas encore attaché l’écouteur `pointermove`.

Le test attend maintenant `LoadState.Load` avant de déplacer le pointeur. L’assertion sur `--pointer-x` est conservée ; aucune temporisation arbitraire ni modification des styles par le test n’a été ajoutée. Le diagnostic temporaire a été retiré.

Compilation de la cible Playwright en Release : zéro erreur, zéro avertissement. Exécution avec `PLAYWRIGHT_TEST_ENABLED=1` : **1 réussi, 0 échec, 0 ignoré**. Le smoke complet couvre aussi l’administration, la création d’une identité, les réglages, le mobile et le focus clavier. Cette réussite locale sur macOS ne qualifie ni Linux/CI ni le parcours complet du sample LeaveRequest.
