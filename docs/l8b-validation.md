# Validation restante du lot L8b — Oracle, SMTP et parcours métier

Date : 20 octobre 2026.

Statut : implémentation terminée. Le porteur confirme le 20 octobre 2026 la réussite de GitHub Actions sur les trois OS, du test Playwright Linux et d’OpenLDAP/LDAPS paginé. Restent Oracle Enterprise 19.19, dont le conteneur n’a pas pu être lancé, le SMTP réel et le parcours métier LeaveRequest complet. La [preuve](evidence/l8b.md) distingue les résultats acquis de ces capacités encore à qualifier.

## 1. Compilation et suites complètes en Release — acquises en CI

Depuis la racine :

```bash
dotnet restore EnterpriseWorkflow.slnx --locked-mode
dotnet build EnterpriseWorkflow.slnx --configuration Release --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
dotnet test --solution EnterpriseWorkflow.slnx --configuration Release --no-build --no-restore
```

Résultat acquis le 20 octobre 2026 : matrice GitHub Actions réussie sur Linux, macOS et Windows. Les commandes restent ci-dessus pour reproduction locale.

Les suites légères déjà exécutées en Debug : Core, Extensions, Runtime, Presentation, Persistence.Sqlite, Security.Persistence.Sqlite, Security et Web. Le nouveau test runtime couvre la confirmation de l’initiateur, le claim, la sélection conditionnelle, le commentaire obligatoire, la reprise, l’idempotence incluant le destinataire et la récupération après révocation. Les tests de notification simulent le transport SMTP ; aucun email réel n’a été envoyé.

## 2. Oracle Enterprise 19.19

Utiliser le schéma jetable de la stack existante, jamais un schéma contenant des données à conserver. Le script lit les secrets depuis `tests/oracle/.env` ignoré par Git. Pour une stack de test déjà initialisée :

```bash
dotnet restore tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --locked-mode
dotnet build tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --configuration Release --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
./tests/oracle/oracle-test.sh test --no-build --no-restore
```

La préparation initiale est décrite dans [tests/oracle/README.md](../tests/oracle/README.md). Ne pas relancer `reset` sur une stack à préserver : cette commande détruit son volume.

Attendu : les tests historiques et `L8bResolvedAssignmentActivationLookupAndNotificationFanOutAreDurable` réussissent. Ce dernier contrôle l’initiateur du claim, l’affectation résolue persistée, la lecture par activation après réouverture et la création atomique des messages enfants avec refus d’un ancien bail. Le test sécurité vérifie aussi la pagination des identités connues.

Les changements L8b réutilisent les tables et index existants : configuration canonique des nœuds, affectations et outbox. Aucune nouvelle migration SQL n’est attendue ; les migrations existantes restent à appliquer/valider normalement.

## 3. LDAP/LDAPS et pagination réelle — acquise sur Linux CI

La suite d’intégration LDAP contient maintenant une énumération avec pages de taille 1, vérification de fin et conservation des identités stables. Le curseur durable contient un offset opaque : les cookies RFC 2696 restent confinés à la connexion qui les a créés et sont rejoués jusqu’à cet offset à chaque page. Elle doit réussir sur un serveur réel avec contrôles LDAP de pagination.

```bash
./tests/ldap/generate-test-certificate.sh
docker compose -f tests/ldap/docker-compose.yml up -d --wait
LDAP_TEST_ENABLED=1 LDAP_TEST_LDAPS=1 LDAP_TEST_CA_FILE="$PWD/tests/ldap/certs/ca.crt" \
  LDAP_TEST_CA_DIRECTORY="$PWD/tests/ldap/certs/trust" \
  LDAPTLS_CACERT="$PWD/tests/ldap/certs/ca.crt" LDAPTLS_REQCERT=demand \
  dotnet test tests/EnterpriseWorkflow.Security.Tests/EnterpriseWorkflow.Security.Tests.csproj --configuration Release --no-build --no-restore
docker compose -f tests/ldap/docker-compose.yml down --volumes
```

Résultat acquis le 20 octobre 2026 : bind, groupes, identité après renommage, rejet du mauvais certificat et pagination réussissent dans le job Linux. Un annuaire sans pagination doit toujours produire un diagnostic explicite pour les notifications ; une recherche nominale tronquée ne doit pas être présentée comme une énumération complète.

## 4. Navigateur Linux acquis ; parcours LeaveRequest complet restant

Le smoke a été reproduit puis corrigé sur macOS, puis confirmé sur Linux CI le 20 octobre 2026. Les commandes restent disponibles pour reproduction :

```bash
dotnet build tests/EnterpriseWorkflow.Web.Playwright.Tests/EnterpriseWorkflow.Web.Playwright.Tests.csproj --configuration Release --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1
pwsh tests/EnterpriseWorkflow.Web.Playwright.Tests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium
PLAYWRIGHT_TEST_ENABLED=1 dotnet test tests/EnterpriseWorkflow.Web.Playwright.Tests/EnterpriseWorkflow.Web.Playwright.Tests.csproj --configuration Release --no-build --no-restore
```

Ce smoke qualifie le portail et l’administration ; il ne remplace pas le parcours métier suivant. Charger le sample packagé selon le [README du module](../samples/LeaveRequest.Module/README.md), avec manifeste produit par `WorkflowModuleManifestWriter.Create`, puis configurer trois identités distinctes et leurs profils.

1. Dans l’administration, vérifier que les quatre permissions du module sont disponibles avec leurs libellés. La page Habilitations présente les exigences métier et renvoie vers les profils ; elle ne propose pas de grants exacts pour ces nouveaux processus. Un POST de grant exact doit être refusé. Attribuer `request` au demandeur, `hr.review` aux RH et `manager.approve` au responsable. Les IDs sont préfixés par `sample.leave-request.`. La lecture d’instance utilise une permission distincte `read`.
2. Soumettre le formulaire initial : l’instance est créée, aucun manager n’est demandé à ce stade. Le demandeur reçoit la confirmation ; une autre personne ne peut pas la compléter à sa place.
3. Confirmer : la tâche RH apparaît dans le pool. Deux sessions RH tentent de la prendre ; un seul claim gagne. La libération permet un autre claim.
4. Vérifier le résumé et l’absence de `privateNote`. Les libellés métier doivent apparaître dans le résumé et sur les actions.
5. Rechercher un manager depuis la tâche RH. Seules les personnes éligibles sont proposées ; les homonymes restent distinguables. Valider sans sélection doit être refusé. Refuser ne nécessite aucune sélection et exige un commentaire.
6. Valider avec un manager éligible. Redémarrer le Host : la désignation est conservée. Le responsable traite le formulaire standard, puis la clôture Service mène à la fin.
7. Retirer la permission du destinataire après sélection mais avant activation : la tâche attend une affectation, aucune substitution n’est faite. Avec les droits de lecture métier et d’administration des accès, rechercher puis affecter explicitement un candidat éligible. Un simple changement d’identité soumis par API doit rester refusé.
8. Modifier une sélection sous une clé d’idempotence déjà consommée : conflit. Rejouer la soumission identique : aucune seconde progression.
9. Vérifier une ancienne définition sans `$workflow` : les grants exacts restent nécessaires, aucune conversion implicite en permissions métier.

Le handler standard valide les commentaires sans les fusionner dans l’état métier. Tester un handler personnalisé si la conservation métier des commentaires est requise. Les composants personnalisés ne dispensent jamais des contrôles serveur.

## 5. SMTP réel et fan-out durable

Configurer un SMTP de test et une adresse d’expédition. Les options se trouvent sous `Notifications:Smtp` : `Host`, `Port`, `EnableSsl`, `From`, `BaseUrl`, `PageSize` (1–100), `TimeoutSeconds` (1–20), `UserName`/`Password` si nécessaires. Fournir les secrets par le mécanisme de configuration habituel.

Pour les comptes locaux, configurer `Addresses` avec une clé produite par `WorkflowNotificationTransport.AddressKey(identity)`, généralement `local|<subjectId>`. Pour les annuaires, l’email du candidat est utilisé, sauf correspondance explicite. L’énumération des identités directes et des annuaires est paginée ; chaque email a sa propre opération durable.

Vérifier :

- sans `Host`, aucune notification n’est requise et la tâche est disponible dans l’inbox ;
- à l’activation du pool, les personnes éligibles reçoivent un message minimal avec lien, sans motif, note privée ou autre donnée métier ;
- sur plusieurs pages, chaque personne n’a qu’une opération logique par activation, même si elle est trouvée par plusieurs sources ;
- serveur arrêté puis redémarré : retries par destinataire, aucune recréation de tâche et aucune seconde progression ;
- arrêt du Host pendant le fan-out : les messages enfants et la page suivante reprennent depuis l’outbox ;
- adresse absente/invalide ou annuaire non énumérable : erreur exploitable dans l’outbox, sans blocage de la tâche ;
- claim ou retrait d’habilitation avant envoi : pas d’email conférant un accès devenu invalide ; les droits sont de toute façon revérifiés à l’ouverture ;
- aucune relance automatique après libération du claim ou ajout d’un grant dans cette version.

SMTP peut délivrer deux fois après un crash entre envoi et acquittement. L’en-tête `X-Workflow-Idempotency-Key` reste stable ; il ne garantit pas une déduplication par le serveur de messagerie.

## 6. Matrice acquise et retour restant

GitHub Actions est confirmé sur Linux, macOS et Windows ; le job Linux a inclus LDAP et navigateur activés. Il reste à vérifier Oracle 19.19, SMTP réel, coexistence d’artefacts, ressources UI, parcours LeaveRequest complet et réutilisation explicite d’une ancienne révision sans substitution de handler.

Retour de succès attendu :

```text
OK L8b / Release / Oracle 19.19 / LDAP paginé / navigateur LeaveRequest / SMTP / CI
```

En cas d’échec, transmettre la commande ou l’étape et le journal depuis la première erreur, sans secrets. Les étapes réussies peuvent être résumées.
