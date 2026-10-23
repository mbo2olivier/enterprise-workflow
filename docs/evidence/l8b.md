# Preuve locale L8b — SDK fluent et parcours métier

Date : 20 octobre 2026. Hôte local macOS arm64, SDK .NET `10.0.401`.

Statut : implémentation livrée. Le porteur confirme le 20 octobre 2026 la réussite de GitHub Actions, de Playwright Linux et d’OpenLDAP/LDAPS paginé. Le porteur fournit ensuite une capture de qualification Oracle 19 réussie : 13 tests, 13 réussis, 0 échec, 0 ignoré. Les branches métier principales et SMTP sont ensuite qualifiés localement avec les commandes L9 et Chromium ; voir la [preuve L9](l9.md) et les compléments encore prévus par [la procédure](../l8b-validation.md).

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

Oracle 19 est acquis sur confirmation du porteur et capture de résultat. La livraison SMTP vers la capture Docker Compose et les branches principales LeaveRequest sont ensuite acquises localement avec les commandes L9 ; les compléments et la qualification CI restent des critères de sortie de ce lot, suivis dans la [preuve L9](l9.md). LDAPS paginé Linux, Playwright Linux et la matrice CI sont acquis. Le smoke Playwright existant ne couvre pas à lui seul la totalité du nouveau parcours LeaveRequest. Les tests Oracle ont été étendus pour le nouveau contrat ; leur compilation est vérifiée séparément, leur exécution exige le schéma jetable.

## Correction de la pagination OpenLDAP durable — 20 octobre 2026

Le premier essai Linux L8b échouait à la deuxième page : le cookie RFC 2696 avait été placé directement dans le message outbox, puis réutilisé sur une nouvelle connexion. OpenLDAP lie ce cookie à la connexion d’origine. Le provider expose désormais un offset opaque et durable ; pour chaque appel, il rejoue les contrôles paginés sur une seule connexion jusqu’à l’offset, puis lit la page demandée. Un arrêt ou un changement de worker ne rend donc plus le curseur inutilisable.

La cible Security compile en Release sans avertissement. Les **17 tests Security** passent hors LDAP, puis passent également contre la stack OpenLDAP 2.6 réelle en mode clair explicitement activé, avec pages de taille 1 sur plusieurs connexions. La stack et ses volumes jetables ont été supprimés après le test. La chaîne LDAPS Linux, son certificat et la pagination ont ensuite été confirmés par GitHub Actions le 20 octobre 2026.

## Correction et exécution du smoke navigateur — 20 octobre 2026

L’échec de `PortalAndAdministrationAreQualifiedOnSqlite` sur `.workspace` a été reproduit sur Chromium macOS arm64. Le diagnostic temporaire montrait le déplacement à `(600, 300)` alors que `document.readyState` valait encore `loading` après le POST de connexion. Le nouveau document affichait déjà le contenu SSR, mais le script n’avait pas encore attaché l’écouteur `pointermove`.

Le test attend maintenant `LoadState.Load` avant de déplacer le pointeur. L’assertion sur `--pointer-x` est conservée ; aucune temporisation arbitraire ni modification des styles par le test n’a été ajoutée. Le diagnostic temporaire a été retiré.

Compilation de la cible Playwright en Release : zéro erreur, zéro avertissement. Exécution avec `PLAYWRIGHT_TEST_ENABLED=1` : **1 réussi, 0 échec, 0 ignoré**. Le smoke couvre aussi l’administration, la création d’une identité, les réglages, le mobile et le focus clavier. Sa réussite Linux/CI est confirmée le 20 octobre 2026 ; elle ne qualifie pas à elle seule le parcours complet du sample LeaveRequest.

## Qualification Oracle 19 — preuve fournie par le porteur

Le 20 octobre 2026, le porteur confirme Oracle 19 et joint une capture montrant la compilation Release réussie de `EnterpriseWorkflow.Persistence.Oracle.Tests`, le conteneur `oracle-oracle-1` Healthy et l’exécution de `./tests/oracle/oracle-test.sh test --no-build --no-restore`. Résultat visible : **13 tests, 13 réussis, 0 échec, 0 ignoré**, durée **8 s 789 ms**, cible `net10.0/arm64` sur le Mac du porteur. Cette preuve lève le blocage de démarrage du conteneur précédemment consigné.

Source : capture jointe à la conversation par le porteur (`codex-clipboard-b6471631-f769-48cd-9ac0-138cd4f22915.png`), non rejouée par l’agent. La capture ne présente pas la version serveur détaillée ni le digest de l’image ; elle ne constitue pas une nouvelle qualification d’autres versions Oracle. Le minimum produit 19.19 conserve ses preuves historiques propres.

Décision associée : démarrage L9 autorisé ; parcours LeaveRequest complet et SMTP reportés à sa qualification intégrée finale, sans les déclarer déjà acquis.
