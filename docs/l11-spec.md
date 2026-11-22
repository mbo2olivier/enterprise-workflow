# L11 — contrats d’hébergement, identité, secrets et localisation

Statut : **L11 clos le 19 novembre 2026**, après confirmation par le porteur de la validation locale et CI. La série L10 reste close. Le plan [L11](l11-hosting-security-localization-plan.md) porte les critères de sortie ; L12 reste distinct.

## Composition

Le SDK d’hébergement est distribué séparément dans `EnterpriseWorkflow.Hosting` : les modules existants ne référencent pas le serveur. Le builder compose les services avant construction et expose la configuration, les services, les logs et l’environnement .NET. Le kernel standard utilise cette composition avec ses providers locaux/LDAP existants. Les services propres à un hôte sont enregistrés dans le code, jamais chargés par nom de classe depuis la configuration.

La validation structurelle précède le démarrage. Les migrations et le bootstrap restent explicites et aucun service provider temporaire n’est construit pour résoudre les dépendances pendant les inscriptions. La présentation web est une capacité explicitement demandée. Les API publiées doivent rester compatibles ; les contrats optionnels s’ajoutent sans modifier les signatures existantes.

## Identité et sécurité

Authentification primaire, résolution, recherche, statut local et distant, import et approbation sont des capacités distinctes. L’identité stable est `(ProviderId, SubjectId)`. Les permissions restent locales. Un seul provider primaire est enregistré par hôte dans cet incrément ; des annuaires distincts peuvent coexister.

Le provisionnement facultatif importe seulement dans EW. Les commandes de demande/approbation incluent acteur et révision. Le provider doit garantir atomiquement l’unicité, l’interdiction d’auto-approbation, la relecture de la source, l’activation conjointe des profils et l’audit. Les services EW doivent refuser une identité non active à la connexion et à la revalidation des sessions. Le provider standard n’implémente pas cette capacité. Les changements importants de la source imposent une nouvelle revue ; le provider définit les attributs importants.

Le second facteur est désactivé par défaut. Aucun handler n’est obligatoire lorsqu’il est désactivé. Son activation sans handler est une erreur de composition. Le handler est compilé dans l’hôte et reçoit une identité et une tentative corrélée, jamais le mot de passe primaire. Il gère le challenge et ses preuves ; le moteur de workflows n’intervient pas. L’hôte ne crée la session complète qu’après succès du parcours. Arbitrages confirmés le 18 novembre : sans capacité de statut distant, expiration et révocation locales ; EW fournit un écran générique de seconde preuve (code ou confirmation/polling), les hôtes peuvent ajouter leurs parcours. Le cookie temporaire est protégé, lié au navigateur et limité à dix minutes ; il ne donne aucun accès authentifié. Le handler garantit la consommation atomique et la limitation des essais, y compris en déploiement multi-instance.

## Développement

`.env` seul est chargé automatiquement à la racine du projet/artefact. `--env-file` est relatif au répertoire courant ; un fichier explicite absent échoue. `--no-env-file` est incompatible avec `--env-file`. Aucune exécution shell, interpolation ou sélection `.env.<environnement>` implicite. Le fichier accepte commentaires, lignes vides, `KEY=value` et guillemets simples/doubles ; doublons refusés sans afficher les valeurs.

Les clés `Development__…` visent les sections de `ew.dev.json` ; `Notifications__Smtp__…` vise SMTP. Les variables du processus écrasent le fichier, les options CLI écrasent les deux. Les valeurs SMTP, de localisation et de configuration personnalisée des modules sont appliquées dans l’hôte uniquement (clés `Security__…` exclues en mode dev) ; les secrets sont exclus du snapshot disque et des arguments. Les valeurs du fichier ne sont jamais injectées dans l’environnement du processus CLI, donc pas dans ses compilations. Les clés de sécurité ne changent pas les identités dev. Un redémarrage est requis.

## Localisation

Langues disponibles, langue par défaut et langue de repli sont validées dans la configuration. La préférence utilisateur est locale et facultative. Un toggle dans la barre supérieure, adjacent à la recherche, la modifie directement via POST protégé contre CSRF ; le retour est limité à une URL locale et conserve la page et ses paramètres. Le bouton de réinitialisation restaure la langue de l’instance. Aucun menu ni page Profil n’est requis. Les textes sont résolus pour chaque affichage, avec repli vers la langue configurée puis le libellé fourni. Les valeurs persistées et identifiants ne sont pas traduits. Les catalogues d’un module appartiennent à son artefact exact ; ne jamais changer le hash d’une ancienne définition en résolvant une traduction au chargement. Les libellés littéraux restent compatibles.

## Qualification

Tests de composition avec et sans second facteur, contrôle des capacités et des sessions ; conformité maker/checker avec doublons et révisions ; `.env` priorités, absence de secrets dans le snapshot et séparation des sous-processus ; tests bilingues et de préférence. Les tests SQLite/Oracle et navigateur sont requis lorsque leurs parcours ou schémas changent. Aucune intégration bancaire réelle n’est déclarée qualifiée par un double de test.


## Convention de catalogues sans fournisseur dédié

Les ressources embarquées `Localization/<culture>.json` sont découvertes dans l’assembly exacte du module. `WorkflowTexts.Text(clé, repli)` (import statique) capture l’assembly appelante ; les helpers partagés choisissent explicitement `WorkflowTexts.For<TModule>()`. `[LabelKey(clé, repli)]` utilise l’assembly déclarant la propriété du DTO. Aucun fournisseur de catalogue dédié n’est obligatoire. Le cache est indexé par instance d’assembly (y compris contexte de chargement), les catalogues conventionnels sont immuables et les anciennes formes manuelles/fournisseurs explicites restent supportées. Sans catalogue ou sans clé, le texte de secours déclaré est conservé. Les templates incluent le glob de ressources ; l’auteur d’un projet existant ajoute la ligne `EmbeddedResource Include="Localization/*.json"`.
