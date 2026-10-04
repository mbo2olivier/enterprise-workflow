# Sécurité et administration extensibles

Décidé : administration intégrée ; fournisseurs locaux ou distants sélectionnés par extensions ; LDAP par identifiant/mot de passe (ADR 0018) ; contrats d’authentification et d’autorisation indépendants du cœur ; grants exacts de stages selon l’ADR 0019.

## Frontières et contrats

| Contrat conceptuel | Entrée / sortie | Responsabilité |
| --- | --- | --- |
| AuthenticationProvider | Preuve transitoire → succès avec sujet stable, échec, indisponibilité ou challenge | Établir l’identité ; ne jamais persister la preuve dans le workflow |
| AuthorizationProvider | Sujet, action, ressource, contexte → Allow/Deny/Unavailable | Décider l’accès avec motif technique auditable |
| IdentityDirectory | Référence stable → attributs d’identité minimaux, groupes facultatifs, capacités de recherche | Résoudre les identités sans dépendance AD du moteur |
| IdentityAdministration | Opérations déclarées disponibles | Créer/désactiver/réinitialiser seulement si l’extension le permet |
| AssignmentResolver | Règle versionnée et contexte métier → sujet/groupe | Affecter une tâche ; distinct de l’autorisation |
| SessionIntegration | Résultat d’authentification → session du Host | Durée, révocation, cookies ou schémas HTTP dans l’intégration |

Les contrats L5 sont livrés dans `EnterpriseWorkflow.Security.Abstractions` et n’exposent ni `HttpContext`, ni LDAP, ni EF. L’installation associe chaque fournisseur à un identifiant stable ; un sujet est identifié par `(ProviderId, SubjectId)`, jamais par son seul nom affiché ou email.

Décision MVP : un fournisseur d’authentification actif par parcours configuré et un fournisseur d’autorisation explicite. Aucun essai automatique du mot de passe auprès de tous les connecteurs. Plusieurs extensions peuvent être installées sans toutes être actives. Aucune agrégation implicite de décisions Allow venant de fournisseurs différents.

Les contrôles structurels — instance terminale, tâche clôturée, révision et affectation — ne sont pas contournables par un fournisseur qui autorise l’action. Une panne d’autorisation refuse l’opération et signale une indisponibilité ; elle ne donne pas de droits par défaut.

## Administration intégrée

L’administration présente les fournisseurs installés/actifs, leurs capacités, la configuration non secrète, les sujets/groupes consultables, les politiques et l’audit autorisés. Elle ne crée pas un magasin de mots de passe dans le Core.

Une extension locale peut exposer création, désactivation et réinitialisation. Une extension LDAP en lecture ne montre pas ces actions comme disponibles. Un fournisseur d’autorisation distant peut rendre les règles consultables sans modification locale. Les mêmes restrictions s’appliquent côté serveur, pas uniquement à l’affichage.

Les paramètres secrets sont référencés depuis le mécanisme de configuration approuvé, jamais renvoyés en clair par l’administration. Les changements sensibles exigent un droit spécifique, validation et audit ; un changement de fournisseur doit traiter les identités et affectations existantes par mapping explicite, sans fusion par email.

L’administration est un composant intégré au Host et utilise les mêmes services applicatifs. Une instance isolée n’est pas une administration centrale des succursales. Le déploiement d’une extension exécutable relève de l’exploitant ; pas de téléversement de DLL par une page métier au MVP.

## Profils internes et associations

Un profil interne est un ensemble de permissions administré dans le système. Le fournisseur interne d’autorisation reste indépendant du fournisseur d’authentification : un utilisateur authentifié par LDAP peut recevoir directement un profil interne existant, via son identité `(ProviderId, SubjectId)`, sans groupe LDAP. L’administration peut aussi mapper un groupe LDAP identifié de façon stable vers un profil existant lorsque l’entreprise utilise cette capacité. Les groupes LDAP sont donc facultatifs ; aucune création ni modification de groupe LDAP n’est nécessaire.

Les mutations de profils, associations directes et mappings exigent une permission spécifique et un audit. Un profil administratif n’accorde pas implicitement le droit d’approuver une tâche métier. Les contrôles par ressource, d’affectation et de révision restent applicables. Le stockage des profils et associations est qualifié sur SQLite et Oracle.

L’implémentation autorise plusieurs profils par identité et calcule l’union des permissions directes et issues des groupes au sein du fournisseur interne. Les permissions directes restent utilisables si l’annuaire facultatif est indisponible ; aucun droit issu d’un groupe ancien n’est réutilisé. Les mutations sont relues à chaque commande sensible. Cela ne constitue pas une agrégation entre fournisseurs d’autorisation.

Q11 est résolue par l’[ADR 0017](../adr/0017-designation-approbateur.md) : une permission d’approbation portée par les profils habilite les candidats. Au démarrage ou lors de l’activité concernée, l’utilisateur recherche par nom et choisit explicitement un approbateur autorisé. Le serveur valide le candidat et persiste son identité stable, puis revalide droits et affectation à l’activation et à la complétion. Aucun attribut AD `manager` n’est requis. L’habilitation par profil et l’affectation à une tâche restent deux contrôles distincts.

## Extension LDAP générique

`LdapDirectoryProvider` implémente LDAPv3 en lecture seule. `LdapDirectoryOptions` configure filtre utilisateur, attributs, codecs `Utf8String`, `GuidLittleEndian`, `Hexadecimal` ou `Base64`, stratégie de groupes et statut. Les capacités recherche/groupes/statut sont annoncées explicitement. L’identité stable repose par défaut sur `entryUUID` ; aucun DN, login ou email ne sert de repli.

Le transport par défaut est LDAPS. Le mode `PlainText` doit être choisi explicitement ; il n’existe aucun downgrade automatique après erreur TLS. La chaîne système est utilisée par défaut ; sous Linux, `CertificateDirectory` peut désigner explicitement un répertoire de CA PEM indexé par `openssl rehash`, sans désactiver la validation du certificat. Les credentials de service restent fournis par `ILdapServiceCredentialProvider`, les referrals sont désactivés et les recherches, groupes et timeouts sont bornés. StartTLS et groupes imbriqués sont hors L5a. L’ancien connecteur AD est supprimé conformément à R1 ; AD n’est pas revendiqué.

## Extension locale

Extension optionnelle, notamment pour tests ou installations autonomes. Comptes réels distincts des identités simulées de développement. Utiliser ASP.NET Core Identity dans l’extension, sans imposer ses entités au modèle workflow. [Capacités d’Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0).

Mots de passe hachés par le composant retenu, politique de verrouillage, révocation et réinitialisation à définir. Pas d’utilisateur/mot de passe administrateur universel. Le stockage de l’extension et ses migrations restent identifiés séparément ; les mappings SQLite et Oracle sont qualifiés avant clôture de L5.

Le premier administrateur doit être initialisé par une opération locale explicite, ponctuelle et auditée, avec un sujet local ou externe selon l’extension. Le bootstrap utilise une commande locale ; le retrait du dernier accès administratif connu est empêché et la récupération est locale, explicite et auditée. Aucun endpoint de bootstrap public permanent.

## Extension API tierce

Le SDK expose les mêmes contrats aux auteurs d’extensions ; une API existante peut avoir son protocole propre. `ExampleApiAuthenticationProvider` fournit l’exemple L5 : base HTTPS fixe, redirections refusées, timeout et réponse bornés, et transfert de mot de passe désactivé tant que l’exploitant ne l’active pas explicitement. Cela ne constitue pas une qualification de toutes les API d’entreprise.

Avant un connecteur de production : fixer schémas versionnés, preuve acceptée, authentification machine-à-machine, identité stable, expirations et statuts d’erreur. Les URLs proviennent de la configuration approuvée, jamais d’un champ libre d’utilisateur. HTTPS validé, délais et tailles bornés ; aucune redirection de credentials vers un autre hôte. Aucun transfert de mot de passe vers une API par défaut.

## Stages : nœuds et habilitations contextualisées

Un stage est le nœud dans l’administration, sans entité `StageId` distincte (S13). Les habilitations ciblent workflow/version/nœud/action, au travers d’un profil ou d’une identité. Les candidats éligibles, les tâches visibles et chaque commande sont filtrés dans ce contexte ; le serveur dérive le nœud actif et applique aussi affectation, révision, état et auto-approbation.

Le fournisseur interne L5 conserve les permissions globales pour l’administration. L5b ajoute un service distinct qui évalue exclusivement les grants `(WorkflowId, DefinitionVersion, NodeId, ActionId)` ; une permission globale ne produit donc jamais un droit métier implicite. Les mutations sont validées par le catalogue d’actions, auditées et persistées avec une révision atomique dans SQLite et Oracle. Voir l’[ADR 0019](../adr/0019-workflow-stages.md).

## Permissions et fraîcheur

Permissions conceptuelles distinctes : voir un processus, démarrer, lire une instance, lire une tâche, réaliser une tâche, annuler, voir l’audit, gérer les accès et gérer les fournisseurs. Aucun rôle administrateur ne devient approbateur métier implicitement.

Contrôles serveur sur lectures et commandes ; filtrage cohérent de l’accueil/inbox ; identité technique distincte de l’acteur humain. L’affectation ne remplace pas l’autorisation, et l’autorisation ne permet pas d’écrire sur une tâche terminale.

Revalider droits et affectation lors de la soumission. Sessions révocables côté serveur ; défauts acceptés : 30 minutes d’inactivité, huit heures maximum et recontrôle du statut distant au plus toutes les cinq minutes. Toutes les durées, fréquences, règles de cache, verrouillage et paramètres opérationnels de sécurité sont pilotables par configuration de l’entreprise et validés au démarrage. Revalider les permissions à chaque commande sensible. Documenter le délai effectif de révocation et le comportement en cas de panne ; ne pas annoncer une révocation instantanée sans mécanisme adapté. La règle d’auto-approbation est décidée : configurable par workflow et interdite par défaut. Le contrôle serveur concerne les tâches d’approbation, sans interdire les tâches de saisie du demandeur. Une exception explicite ne dispense pas des autres permissions et de l’affectation. Voir [ADR 0011](../adr/0011-auto-approbation.md).

## Protection et preuves

TLS pour l’accès navigateur ; cookies sécurisés et protection CSRF pour sessions cookie ; limitation des tentatives et taille des entrées ; encodage des valeurs affichées. Secrets hors définitions et traces. Audit des mutations critiques et événements de sécurité avec données minimales.

Tests obligatoires : fournisseur indisponible, mauvais credentials, compte désactivé, collision de noms entre fournisseurs, droits retirés avant soumission, action administrative non permise, double complétion et tentative d’accès à la ressource d’autrui. Le mode simulé de développement ne s’active jamais en production ; l’extension locale, elle, reste un vrai fournisseur.

La qualification L5 couvre également l’identité LDAP avec profil direct sans groupes, le mapping groupe LDAP/profil, les mutations de profils autorisées et auditées, la configuration des sessions et la protection du dernier accès administratif connu. Cette cible historique est remplacée par une qualification LDAP non AD réelle en L5a ; les simulations ne clôturent pas la qualification LDAP. Les preuves historiques ne sont pas requalifiées rétroactivement.
