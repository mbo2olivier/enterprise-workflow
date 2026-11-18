# L11 — recette de validation déléguée au porteur

Date : 18 novembre 2026. Statut : à exécuter. Les étapes lourdes de ce document sont déléguées au porteur à sa demande pour limiter la consommation de tokens. **Une commande proposée n’est pas une preuve d’exécution.** Ne clôturer L11 qu’après consolidation des résultats dans [la preuve](../evidence/l11.md).

## 0. Préparer une exécution reproductible

1. Utiliser la révision qui contient les changements L11, depuis la racine du dépôt. Conserver le SHA (`git rev-parse HEAD`) et indiquer si des modifications locales sont présentes (`git status --short`).
2. Vérifier `dotnet --version` : le SDK doit correspondre exactement à `global.json` (10.0.401). Pour le wiki, utiliser la version Node déclarée dans `wiki/package.json`. Prévoir Python 3 pour les inspections SQLite, PowerShell 7 pour installer Chromium et Docker pour les services externes.
3. Ne jamais viser une base de production. Les tests Oracle détruisent et recréent leurs objets ; leur compte doit appartenir à un schéma dédié et jetable.
4. Créer un dossier de résultats **hors des fichiers à versionner** :

   ```sh
   export EW_L11_ROOT="$(mktemp -d -t ew-l11.XXXXXX)"
   git rev-parse HEAD > "$EW_L11_ROOT/revision.txt"
   dotnet --info > "$EW_L11_ROOT/dotnet-info.txt"
   ```

5. Ne pas joindre de `.env`, mots de passe, cookies, codes OTP ou chaînes de connexion aux résultats. Les marqueurs utilisés plus bas sont fictifs.
6. Exécuter chaque commande séparément. Si une étape échoue, conserver son erreur, corriger puis reprendre cette étape. Ne pas compter un test ignoré comme réussi. Ne pas masquer les codes d’échec avec une redirection ou un pipeline.

## 1. Restaurer et compiler

1. Restaurer les projets de la solution :

   ```sh
   dotnet restore EnterpriseWorkflow.slnx --locked-mode
   ```

2. Compiler la configuration Release :

   ```sh
   dotnet build EnterpriseWorkflow.slnx --configuration Release --no-restore
   ```

3. Le projet de tests Oracle n’est pas dans la solution principale ; le préparer séparément :

   ```sh
   dotnet restore tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --locked-mode
   dotnet build tests/EnterpriseWorkflow.Persistence.Oracle.Tests/EnterpriseWorkflow.Persistence.Oracle.Tests.csproj --configuration Release --no-restore
   ```

4. Attendu : zéro erreur et zéro avertissement. Ne pas régénérer silencieusement les lock files si `--locked-mode` échoue : relever la différence.

## 2. Exécuter les tests automatisés du socle

1. Exécuter la solution avec le runner défini dans `global.json` :

   ```sh
   dotnet test --solution EnterpriseWorkflow.slnx --configuration Release --no-build --no-restore
   ```

2. Contrôler les résultats de chaque module, particulièrement :

   - `Core.Tests` : dépendances architecturales, sérialisation, hashes historiques et `ConventionalLocalizationTests` (catalogues sans classe dédiée).
   - `Security.Tests` : providers existants et scénarios maker/checker.
   - `Security.Persistence.Sqlite.Tests` : migrations et stores.
   - `Presentation.Tests` : autorisation, formulaires et notifications.
   - `Web.Tests` : builder sans UI, second facteur optionnel, parcours HTTP réel, préférence linguistique, traductions et SQL Oracle hors ligne.
   - `Cli.Tests` : `.env`, priorités, secret absent du snapshot et configuration dev.

3. Pour isoler les nouveaux tests web avec le runner xUnit direct :

   ```sh
   dotnet run --project tests/EnterpriseWorkflow.Web.Tests --configuration Release --no-build -- -class '*HostingTests' -class '*SecondFactorTests' -class '*LocalizationTests' -class '*OracleMigrationScriptTests'
   ```

4. Attendu : succès des tests locaux. Les recettes explicitement opt-in peuvent être ignorées à cette étape ; les exécuter dans les sections suivantes. Une sortie « zéro tests exécutés » est un échec de recette.

## 3. Qualifier le navigateur du kernel standard

1. Installer la version Chromium attendue par les tests :

   ```sh
   pwsh tests/EnterpriseWorkflow.Web.Playwright.Tests/bin/Release/net10.0/playwright.ps1 install chromium
   ```

   Sur Linux, utiliser `install --with-deps chromium` si les bibliothèques système manquent.

2. Lancer la recette autonome du portail :

   ```sh
   PLAYWRIGHT_TEST_ENABLED=1 dotnet run --project tests/EnterpriseWorkflow.Web.Playwright.Tests --configuration Release --no-build -- -class '*PortalBrowserTests'
   ```

3. Attendu : connexion locale, administration, profils, réglages et responsive inchangés ; toggle de langue à côté de la recherche permettant de passer en anglais sans quitter la page, de retrouver ce choix après rechargement puis de revenir à la langue de l’instance avec ↺. Aucun lien ni page Profil.
4. Vérifier que la route de sélection des identités dev reste absente du kernel standard. Le second facteur doit rester facultatif et le kernel standard ne doit demander aucun handler.
5. Conserver le résumé xUnit. Un navigateur absent, un test ignoré ou un serveur jamais démarré ne valide pas cette étape.

## 4. Qualifier le cycle natif de `ew dev`

1. Lancer le test opt-in qui reconstruit plusieurs fois le sample et conserve ses instances :

   ```sh
   EW_DEV_LIFECYCLE_TESTS=1 dotnet run --project tests/EnterpriseWorkflow.Cli.Tests --configuration Release --no-build -- -class '*DevelopmentLifecycleTests'
   ```

2. Attendu : générations immuables, ancienne instance conservée et terminable après reconstruction, isolation des environnements nommés et arrêt propre des processus.
3. Ne pas lancer en parallèle une autre recette qui modifie le même sample. Le test utilise sa copie de travail ; vérifier son résumé plutôt que des logs issus d’une exécution précédente.
4. Cette recette qualifie la CLI reconstruite, pas l’outil NuGet déjà installé dans le PATH.

## 5. Vérifier manuellement `.env` et les secrets

1. Créer un fichier de test sans secrets réels :

   ```sh
   cat > "$EW_L11_ROOT/l11.env" <<'ENV'
   Development__Server__Url=http://localhost:5091
   Notifications__Smtp__Password=L11_FAKE_SECRET_DO_NOT_SHIP
   ExternalServices__Example__ApiKey=L11_FAKE_MODULE_KEY
   Localization__DefaultCulture=fr
   Localization__FallbackCulture=en
   Security__AuthenticationProvider=must-not-be-used-in-dev
   Security__SecondFactor__Enabled=true
   ENV
   ```

2. Démarrer la CLI locale, dans un terminal dédié :

   ```sh
   dotnet src/EnterpriseWorkflow.Cli/bin/Release/net10.0/EnterpriseWorkflow.Cli.dll dev samples/LeaveRequest.Module --environment l11-validation --env-file "$EW_L11_ROOT/l11.env" --no-watch
   ```

3. Ouvrir `http://localhost:5091/__ew/dev`. Attendu : uniquement les identités de `ew.dev.json`, aucun appel au provider fictif et aucun second facteur, malgré les clés de sécurité du fichier.
4. Depuis un autre terminal à la racine du dépôt, inspecter les fichiers créés :

   ```sh
   python3 - <<'PY'
   from pathlib import Path
   root = Path('samples/LeaveRequest.Module/.ew/environments/l11-validation')
   assert root.exists(), 'Le runtime de recette n’a pas créé son environnement'
   markers = [b'L11_FAKE_SECRET_DO_NOT_SHIP', b'L11_FAKE_MODULE_KEY']
   leaks = [str(p) for p in root.rglob('*') if p.is_file()
            and any(marker in p.read_bytes() for marker in markers)]
   assert not leaks, f'Marqueur secret présent dans les fichiers générés : {leaks}'
   print('Aucun marqueur secret dans les fichiers générés')
   PY
   ```

5. Arrêter avec Ctrl+C. Relancer avec une variable du processus :

   ```sh
   Development__Server__Url=http://localhost:5092 dotnet src/EnterpriseWorkflow.Cli/bin/Release/net10.0/EnterpriseWorkflow.Cli.dll dev samples/LeaveRequest.Module --environment l11-validation --env-file "$EW_L11_ROOT/l11.env" --no-watch
   ```

   Attendu : port 5092. Arrêter, puis ajouter `--url http://localhost:5093` à cette commande : le port doit devenir 5093.

6. Vérifier le refus d’un fichier explicite absent et de la combinaison `--env-file ... --no-env-file`. Les erreurs ne doivent jamais afficher les valeurs du fichier.
7. Pour tester le chargement automatique, utiliser un projet de recette où `.env` n’existe pas déjà : copier le fichier à sa racine, omettre `--env-file`, puis vérifier le port. Un fichier `.env.autre` ne doit pas être sélectionné par `--environment autre`. Retirer seulement le fichier créé pour la recette ; ne pas écraser un `.env` utilisateur existant.
8. Avec `--no-env-file`, vérifier le retour aux valeurs JSON/défaut, sous réserve des variables explicites du processus qui conservent leur priorité.
9. Pour une intégration propre à un module, enregistrer dans son hôte un service lisant `ExternalServices:Example:ApiKey` et vérifier la présence de la valeur **sans l’afficher**. Le fichier doit alimenter la configuration hôte, sans injection dans les processus de compilation. Les tests automatisés couvrent la séparation du snapshot et du dictionnaire de transmission.

## 6. Valider le parcours métier bilingue

1. Relancer le sample avec la commande de la section 5 et ouvrir le sélecteur dev.
2. Choisir Alice. Cliquer sur EN dans le toggle de la barre supérieure, à côté de la recherche.
3. Vérifier `lang="en"` sur le document, navigation, catalogue, titre du processus, formulaire, dates, choix de congé et composant résumé. Les valeurs techniques doivent rester `paid`, `family`, `unpaid`, etc.
4. Créer une demande valide, confirmer et désigner le responsable ; noter la référence.
5. Choisir une autre identité habilitée, conserver sa préférence française et ouvrir la même demande/tâche. Vérifier les libellés français sur les mêmes valeurs métier.
6. Dans le détail, vérifier les cinq onglets dans les deux langues (Details/Library/Progress/History/Comments), les statuts de la liste (Waiting/Completed/Declined/Cancelled/Failed) et le maintien de `?tab=…` lors du basculement. Effectuer approbation hiérarchique puis validation RH ; vérifier détail, commentaires, pièces jointes et clôture. Les droits et l’ordre des étapes doivent rester identiques dans les deux langues.
7. Vérifier une erreur de dates et un champ requis : message lisible dans la langue choisie, saisie conservée et aucune clé de traduction affichée comme libellé.
8. Redémarrer `ew dev` : préférence utilisateur et instance doivent persister. Cliquer sur ↺ (langue de l’instance) et vérifier le repli vers `Localization:DefaultCulture`.
9. Pour le repli de traduction, utiliser le test `LocalizationTests.FallbackLanguageIsConfigurableAndInstanceCulturesAreValidated` ou un module jetable avec une clé absente dans la langue choisie. Ne pas modifier un artefact historique pour simuler cette absence.
10. Vérifier qu’un ancien artefact conservé continue à fonctionner avec ses déclarations exactes. Les traductions d’un nouvel artefact ne doivent pas changer son hash ni sa définition persistée à l’affichage.

## 7. Valider les extensions d’identité et le second facteur

### 7.1 Contrats SDK et écran générique

1. Exécuter `SecondFactorTests` de la section 2. Il vérifie le passage par un serveur HTTP réel, l’absence de session après la première phase, le refus d’une preuve incorrecte, la session après succès et le refus du rejeu.
2. Vérifier `HostingTests.EnabledSecondFactorWithoutHandlerFailsBeforeDatabaseAccess` et `HostingTests.EnablingSecondFactorRequiresReconnectionForOldSimpleSessions`.
3. Exécuter les tests de sécurité :

   ```sh
   dotnet run --project tests/EnterpriseWorkflow.Security.Tests --configuration Release --no-build -- -class '*IdentityProvisioningTests'
   ```

4. Attendu : absence de droits tant que l’identité est en attente, permission distincte, refus d’auto-approbation, refus de révision obsolète, refus de source modifiée et une seule approbation concurrente réussie.
5. Le handler de `samples/CustomKernel` retourne volontairement `Unavailable`. Il permet de vérifier le branchement d’un handler compilé, **pas** un succès OTP. Utiliser le handler de test pour la conformité du socle ; ne pas mettre un code fixe dans un kernel exploité.

### 7.2 Recette supplémentaire du futur kernel bancaire

Cette partie nécessite les implémentations de l’organisation ; elle n’est pas fournie par le kernel standard et n’est pas prouvée par les doubles mémoire.

1. Enregistrer le provider dans le code avec `AddAuthenticationProvider<T>()` et ses capacités avec `AddIdentityProvider<T>()`. Vérifier que les deux inscriptions partagent la même instance lorsque `T` est identique.
2. Soumettre un identifiant existant dans la source tierce. Vérifier les détails récupérés et l’absence de création de compte dans cette source.
3. Tenter de se connecter, d’obtenir des droits ou d’attribuer une tâche à l’identité en attente : refus.
4. Tester maker = checker, checker non habilité, doublon, révision périmée et deux approbations concurrentes.
5. Modifier les attributs importants de la source entre soumission et approbation : nouvelle revue obligatoire, aucun profil activé.
6. Approuver avec un checker distinct. Vérifier l’activation conjointe identité/profils et la durabilité de l’audit après redémarrage.
7. Suspendre l’identité : une session existante et les nouvelles autorisations doivent être refusées.
8. Sans capacité de statut distant, vérifier expiration/révocation locales. Avec cette capacité, vérifier désactivation distante et panne : aucune poursuite silencieuse après une revalidation échouée.
9. Avec second facteur activé, tester expiration, annulation, essais maximaux, panne du service et succès. Aucun échec ne doit créer de session complète.
10. Sur deux réplicas partageant le stockage et les clés, commencer sur A et vérifier sur B ; tenter deux consommations simultanées. Une seule doit réussir.
11. Redémarrer pendant une demande maker/checker ou un challenge : vérifier la politique durable définie par l’implémentation et l’absence de contournement.

## 8. Qualifier Oracle 19 réel

1. Démarrer Docker et lire [le guide Oracle](../../tests/oracle/README.md). Utiliser uniquement la stack jetable du dépôt ou un schéma de recette explicitement isolé.
2. Préparer puis démarrer la stack existante :

   ```sh
   ./tests/oracle/oracle-test.sh init
   ./tests/oracle/oracle-test.sh up
   ./tests/oracle/oracle-test.sh status
   ```

   `reset`/`destroy` sont destructifs pour cette stack. Ne les utiliser que si ses données de test peuvent être supprimées ; ils ne sont pas requis pour un simple démarrage.

3. Exécuter :

   ```sh
   ./tests/oracle/oracle-test.sh test --no-build --no-restore
   ```

4. Attendu : migrations réussies sur Oracle Enterprise 19.19, persistance des préférences linguistiques d’une identité externe, suppression de la préférence et conservation de `SecondFactorVerified`. Ces assertions ont été ajoutées à `OracleSecurityStoreTests`.
5. Consigner version/digest Oracle, nombre de tests et erreurs/ignorés. Le test de génération SQL dans `Web.Tests` et Oracle Free 23 ne remplacent pas Oracle 19 réel.
6. Arrêter sans suppression des volumes si nécessaire : `./tests/oracle/oracle-test.sh down`.

## 9. Qualifier notifications SMTP et compatibilité LDAP

### SMTP

1. Démarrer la capture :

   ```sh
   docker compose -f tests/smtp/docker-compose.yml up -d --wait
   ```

2. Suivre [le guide SMTP](../../tests/smtp/README.md) pour les adresses et les options. Ces valeurs peuvent être fournies en `.env` avec `Notifications__Smtp__Host`, `Port`, `EnableSsl`, `From`, `BaseUrl`, `PageSize`. Conserver les adresses composites des destinataires dans `smtp.addresses` du JSON comme dans le guide SMTP.
3. Donner à Bob une préférence anglaise et à Carol une préférence française. Déclencher une tâche de pool dont ils sont destinataires éligibles.
4. Dans `http://localhost:8025`, vérifier destinataires, langue de l’objet/corps, lien vers la bonne tâche et absence de secret. Une préférence absente doit utiliser la langue de l’instance.
5. Arrêter SMTP (`docker compose -f tests/smtp/docker-compose.yml stop smtp`), créer une nouvelle tâche et vérifier que l’inbox reste utilisable. Redémarrer SMTP avec `start smtp` et vérifier la reprise, sans répéter la transition métier.
6. Conserver les observations de livraison ; la capture ne qualifie pas la délivrabilité ou l’authentification TLS de production.

### LDAP/LDAPS existant

1. Suivre [le guide LDAP](../../tests/ldap/README.md), générer les certificats de test puis démarrer la stack :

   ```sh
   ./tests/ldap/generate-test-certificate.sh
   docker compose -f tests/ldap/docker-compose.yml up -d --wait
   ```

2. Sous Linux avec les dépendances OpenLDAP disponibles :

   ```sh
   LDAP_TEST_ENABLED=1 LDAP_TEST_LDAPS=1 \
   LDAP_TEST_CA_FILE="$PWD/tests/ldap/certs/ca.crt" \
   LDAP_TEST_CA_DIRECTORY="$PWD/tests/ldap/certs/trust" \
   LDAPTLS_CACERT="$PWD/tests/ldap/certs/ca.crt" LDAPTLS_REQCERT=demand \
   dotnet test --project tests/EnterpriseWorkflow.Security.Tests/EnterpriseWorkflow.Security.Tests.csproj --configuration Release --no-build --no-restore
   ```

3. Vérifier authentification, recherche, groupes, statut, panne, pagination et refus d’un nom TLS incorrect. Ne pas revendiquer Active Directory ou LDAP réel Windows/macOS à partir de cette seule exécution.

## 10. Documentation, packaging et matrice

1. Construire le wiki et vérifier ses liens :

   ```sh
   npm --prefix wiki ci
   npm --prefix wiki run build
   ```

2. Vérifier que les guides présentent L11 comme source en qualification, sans annoncer sa disponibilité dans l’ancienne release.
3. Packager le SDK d’hébergement dans le dossier de recette, sans publication :

   ```sh
   dotnet pack src/EnterpriseWorkflow.Hosting/EnterpriseWorkflow.Hosting.csproj --configuration Release --no-build --no-restore --output "$EW_L11_ROOT/packages"
   ```

4. Vérifier dépendances du package, compilation de `samples/CustomKernel`, ressources embarquées de LeaveRequest et absence de `.env`/marqueurs secrets dans les artefacts. Une diffusion future doit utiliser une nouvelle version ; ne jamais republier un numéro déjà diffusé.
5. Exécuter la pipeline CI du dépôt sur Linux, macOS et Windows avec les mêmes sources. Relever séparément le job Playwright Linux et les services externes. Ne pas déduire cette matrice d’une compilation locale.
6. Ne pas publier de packages ni déployer le wiki comme simple effet de la recette.

## 11. Compte rendu à retourner

Compléter ce tableau et joindre uniquement les erreurs utiles et résumés, sans secrets :

| Étape | Résultat (OK / échec / non exécuté) | Tests réussis / échoués / ignorés | Preuve ou erreur |
| --- | --- | --- | --- |
| SHA et compilation Release | | | |
| Tests solution | | | |
| Navigateur kernel + préférence | | | |
| Cycle natif `ew dev` | | | |
| `.env`, priorité et confinement | | | |
| LeaveRequest bilingue | | | |
| SDK identité et second facteur | | | |
| Oracle 19 réel | | | |
| SMTP langue/panne/reprise | | | |
| LDAP/LDAPS Linux | | | |
| Wiki et packaging local | | | |
| CI Linux/macOS/Windows | | | |
| Intégration bancaire (si disponible) | | | |

La qualification du framework et celle du kernel bancaire sont distinctes. Tout reste non exécuté doit être nommé avant clôture ; aucune étape ne devient réussie par simple délégation.
