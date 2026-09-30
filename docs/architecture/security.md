# Sécurité et administration extensibles

Décidé : administration intégrée ; fournisseurs locaux ou distants sélectionnés par extensions ; AD par identifiant/mot de passe ; contrats d’authentification et d’autorisation indépendants du cœur. Les mécanismes ci-dessous sont proposés.

## Frontières et contrats

| Contrat conceptuel | Entrée / sortie | Responsabilité |
| --- | --- | --- |
| AuthenticationProvider | Preuve transitoire → succès avec sujet stable, échec, indisponibilité ou challenge | Établir l’identité ; ne jamais persister la preuve dans le workflow |
| AuthorizationProvider | Sujet, action, ressource, contexte → Allow/Deny/Unavailable | Décider l’accès avec motif technique auditable |
| IdentityDirectory | Référence stable → profil minimal, groupes, capacités de recherche | Résoudre les identités sans dépendance AD du moteur |
| IdentityAdministration | Opérations déclarées disponibles | Créer/désactiver/réinitialiser seulement si l’extension le permet |
| AssignmentResolver | Règle versionnée et contexte métier → sujet/groupe | Affecter une tâche ; distinct de l’autorisation |
| SessionIntegration | Résultat d’authentification → session du Host | Durée, révocation, cookies ou schémas HTTP dans l’intégration |

Ces noms sont conceptuels, pas des signatures C# livrées. Les contrats purs n’exposent ni HttpContext, ni LDAP, ni EF. L’installation doit associer chaque fournisseur à un identifiant stable ; un sujet est identifié par `(ProviderId, SubjectId)`, jamais par son seul nom affiché ou email.

Proposition MVP : un fournisseur d’authentification actif par parcours configuré et un fournisseur d’autorisation explicite. Aucun essai automatique du mot de passe auprès de tous les connecteurs. Plusieurs extensions peuvent être installées sans toutes être actives. Aucune agrégation implicite de décisions Allow venant de fournisseurs différents.

Les contrôles structurels — instance terminale, tâche clôturée, révision et affectation — ne sont pas contournables par un fournisseur qui autorise l’action. Une panne d’autorisation refuse l’opération et signale une indisponibilité ; elle ne donne pas de droits par défaut.

## Administration intégrée

L’administration présente les fournisseurs installés/actifs, leurs capacités, la configuration non secrète, les sujets/groupes consultables, les politiques et l’audit autorisés. Elle ne crée pas un magasin de mots de passe dans le Core.

Une extension locale peut exposer création, désactivation et réinitialisation. Une extension AD en lecture ne montre pas ces actions comme disponibles. Un fournisseur d’autorisation distant peut rendre les règles consultables sans modification locale. Les mêmes restrictions s’appliquent côté serveur, pas uniquement à l’affichage.

Les paramètres secrets sont référencés depuis le mécanisme de configuration approuvé, jamais renvoyés en clair par l’administration. Les changements sensibles exigent un droit spécifique, validation et audit ; un changement de fournisseur doit traiter les identités et affectations existantes par mapping explicite, sans fusion par email.

L’administration est un composant intégré au Host et utilise les mêmes services applicatifs. Une instance isolée n’est pas une administration centrale des succursales. Le déploiement d’une extension exécutable relève de l’exploitant ; pas de téléversement de DLL par une page métier au MVP.

## Extension AD

Décision utilisateur : vérifier un identifiant et un mot de passe auprès du contrôleur ; SSO Windows différé.

Proposition : LDAP sur TLS validé, endpoints configurés par l’exploitant, authentification puis lecture du profil/groupes nécessaires. AD supporte LDAPS ; la disponibilité et les politiques du contrôleur doivent être testées. [Documentation Microsoft](https://learn.microsoft.com/en-us/troubleshoot/windows-server/active-directory/enable-ldap-over-ssl-3rd-certification-authority).

Exigences du connecteur : refuser mot de passe vide, certificat invalide et retour anonyme ; borner timeout et résultats ; échapper les filtres LDAP ; ne pas journaliser les preuves ; distinguer indisponibilité et refus en interne tout en évitant l’énumération de comptes en réponse publique. Ne pas conserver le mot de passe pour une exécution différée du workflow.

La bibliothèque LDAP et sa portabilité seront choisies après un prototype Windows/Linux/macOS. TLS ne permet pas de présumer la compatibilité avec toutes les politiques de signature/channel binding d’un domaine. Groupes imbriqués, domaines multiples et attribut du responsable nécessitent une politique explicite, pas une recherche récursive illimitée.

## Extension locale

Extension optionnelle, notamment pour tests ou installations autonomes. Comptes réels distincts des identités simulées de développement. Utiliser un composant éprouvé tel qu’ASP.NET Core Identity dans l’extension, sans imposer ses entités au modèle workflow. [Capacités d’Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0).

Mots de passe hachés par le composant retenu, politique de verrouillage, révocation et réinitialisation à définir. Pas d’utilisateur/mot de passe administrateur universel. Le stockage de l’extension et ses migrations restent identifiés séparément ; s’il revendique SQLite et Oracle, les deux mappings sont qualifiés.

Le premier administrateur doit être initialisé par une opération locale explicite, ponctuelle et auditée, avec un sujet local ou externe selon l’extension. Le parcours exact et la prévention de la suppression du dernier administrateur sont à finaliser avant L5. Aucun endpoint de bootstrap public permanent.

## Extension API tierce

Le SDK expose les mêmes contrats aux auteurs d’extensions ; une API existante peut avoir son protocole propre. Le MVP fournit un exemple de connecteur HTTP et une suite de conformité ; cela ne constitue pas une qualification de toutes les API d’entreprise.

Avant un connecteur de production : fixer schémas versionnés, preuve acceptée, authentification machine-à-machine, identité stable, expirations et statuts d’erreur. Les URLs proviennent de la configuration approuvée, jamais d’un champ libre d’utilisateur. HTTPS validé, délais et tailles bornés ; aucune redirection de credentials vers un autre hôte. Aucun transfert de mot de passe vers une API par défaut.

## Permissions et fraîcheur

Permissions conceptuelles distinctes : voir un processus, démarrer, lire une instance, lire une tâche, réaliser une tâche, annuler, voir l’audit, gérer les accès et gérer les fournisseurs. Aucun rôle administrateur ne devient approbateur métier implicitement.

Contrôles serveur sur lectures et commandes ; filtrage cohérent de l’accueil/inbox ; identité technique distincte de l’acteur humain. L’affectation ne remplace pas l’autorisation, et l’autorisation ne permet pas d’écrire sur une tâche terminale.

Revalider droits et affectation lors de la soumission. Les règles de cache, durée de session et détection d’une révocation distante doivent être explicites avant production ; ne pas annoncer une révocation instantanée sans mécanisme adapté. La règle d’auto-approbation est décidée : configurable par workflow et interdite par défaut. Le contrôle serveur concerne les tâches d’approbation, sans interdire les tâches de saisie du demandeur. Une exception explicite ne dispense pas des autres permissions et de l’affectation. Voir [ADR 0011](../adr/0011-auto-approbation.md).

## Protection et preuves

TLS pour l’accès navigateur ; cookies sécurisés et protection CSRF pour sessions cookie ; limitation des tentatives et taille des entrées ; encodage des valeurs affichées. Secrets hors définitions et traces. Audit des mutations critiques et événements de sécurité avec données minimales.

Tests obligatoires : fournisseur indisponible, mauvais credentials, compte désactivé, collision de noms entre fournisseurs, droits retirés avant soumission, action administrative non permise, double complétion et tentative d’accès à la ressource d’autrui. Le mode simulé de développement ne s’active jamais en production ; l’extension locale, elle, reste un vrai fournisseur.
