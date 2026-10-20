# Preuve locale L8b — SDK fluent et parcours métier

Date : 20 octobre 2026. Hôte local macOS arm64, SDK .NET `10.0.401`.

Statut : implémentation livrée. Le porteur confirme le 20 octobre 2026 la réussite de GitHub Actions, de Playwright Linux et d’OpenLDAP/LDAPS paginé. Aucune exécution Oracle L8b ni SMTP réel n’est revendiquée : le conteneur Oracle n’a pas pu être lancé. Le parcours métier LeaveRequest complet reste également à consigner selon [la procédure](../l8b-validation.md).

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

Oracle 19.19, livraison SMTP réelle et parcours complet LeaveRequest restent à exécuter selon la procédure dédiée. LDAPS paginé Linux, Playwright Linux et la matrice CI sont acquis. Le smoke Playwright existant ne couvre pas à lui seul la totalité du nouveau parcours LeaveRequest. Les tests Oracle ont été étendus pour le nouveau contrat ; leur compilation est vérifiée séparément, leur exécution exige le schéma jetable.

## Correction de la pagination OpenLDAP durable — 20 octobre 2026

Le premier essai Linux L8b échouait à la deuxième page : le cookie RFC 2696 avait été placé directement dans le message outbox, puis réutilisé sur une nouvelle connexion. OpenLDAP lie ce cookie à la connexion d’origine. Le provider expose désormais un offset opaque et durable ; pour chaque appel, il rejoue les contrôles paginés sur une seule connexion jusqu’à l’offset, puis lit la page demandée. Un arrêt ou un changement de worker ne rend donc plus le curseur inutilisable.

La cible Security compile en Release sans avertissement. Les **17 tests Security** passent hors LDAP, puis passent également contre la stack OpenLDAP 2.6 réelle en mode clair explicitement activé, avec pages de taille 1 sur plusieurs connexions. La stack et ses volumes jetables ont été supprimés après le test. La chaîne LDAPS Linux, son certificat et la pagination ont ensuite été confirmés par GitHub Actions le 20 octobre 2026.

## Correction et exécution du smoke navigateur — 20 octobre 2026

L’échec de `PortalAndAdministrationAreQualifiedOnSqlite` sur `.workspace` a été reproduit sur Chromium macOS arm64. Le diagnostic temporaire montrait le déplacement à `(600, 300)` alors que `document.readyState` valait encore `loading` après le POST de connexion. Le nouveau document affichait déjà le contenu SSR, mais le script n’avait pas encore attaché l’écouteur `pointermove`.

Le test attend maintenant `LoadState.Load` avant de déplacer le pointeur. L’assertion sur `--pointer-x` est conservée ; aucune temporisation arbitraire ni modification des styles par le test n’a été ajoutée. Le diagnostic temporaire a été retiré.

Compilation de la cible Playwright en Release : zéro erreur, zéro avertissement. Exécution avec `PLAYWRIGHT_TEST_ENABLED=1` : **1 réussi, 0 échec, 0 ignoré**. Le smoke couvre aussi l’administration, la création d’une identité, les réglages, le mobile et le focus clavier. Sa réussite Linux/CI est confirmée le 20 octobre 2026 ; elle ne qualifie pas à elle seule le parcours complet du sample LeaveRequest.
