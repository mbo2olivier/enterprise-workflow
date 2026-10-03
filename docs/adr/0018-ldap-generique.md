# ADR 0018 — Extension LDAP indépendante d’Active Directory

Date : 3 octobre 2026. **Statut : acceptée ; mécanismes L5a arbitrés et implémentés.**

## Décision de périmètre

Le framework doit disposer d’une extension LDAP, indépendante d’AD. Cette décision remplace l’obligation AD des ADR 0003 et 0016 et de la qualification L5. AD devient un annuaire compatible possible, dont le schéma doit être configuré et qualifié séparément ; un domaine AD réel n’est plus une condition de livraison du MVP LDAP. Les autres décisions de sécurité restent applicables.

Le code L5 et ses preuves sont conservés. Avant L5a, `ActiveDirectoryProvider` n’était pas un connecteur LDAP générique qualifié : ses valeurs par défaut, sa conversion des identifiants binaires en `Guid` et sa résolution des groupes depuis leurs DN étaient spécifiques à AD. L5a le remplace intégralement.

## Contrat L5a

- LDAPv3 en lecture seule ; authentification identifiant/mot de passe par bind utilisateur après recherche bornée via un compte de service aux droits minimaux. Aucun mot de passe conservé dans une instance.
- LDAPS par défaut avec chaîne et nom du serveur validés par la pile native. LDAP clair est disponible uniquement par sélection explicite de `PlainText` dans la configuration ; aucun downgrade automatique après erreur TLS n’est effectué. StartTLS est reporté à une extension qualifiée séparément. Rejeter mot de passe vide, bind anonyme ou non authentifié, certificat invalide, filtre/configuration invalide et résultat ambigu.
- Configuration validée : endpoint, base et portée de recherche, filtre/classe des utilisateurs, attribut de login et d’affichage, identifiant stable, codec de cet identifiant, groupes et statut de compte selon capacités. Les valeurs utilisateur sont échappées ; noms d’attributs et modèles de filtres sont validés au démarrage. Timeout, tailles et nombre de groupes sont bornés ; referrals ne transmettent pas implicitement les credentials à un autre endpoint.
- Identité `(ProviderId, SubjectId)` stable après renommage/déplacement. Attribut immuable configuré, par exemple `entryUUID` lorsqu’il est disponible ; `objectGUID` uniquement avec codec AD explicite. Ni DN, login, nom affiché ni email ne servent de clé durable par défaut. L’absence de clé stable est un diagnostic de configuration, pas une substitution silencieuse.
- Groupes facultatifs : stratégie explicite selon le schéma (appartenance côté utilisateur ou recherche de groupes par membre), identifiant stable et codec dédiés. Ne pas présumer `memberOf`, `member`, `uniqueMember` ou les groupes imbriqués universels. Groupes imbriqués hors MVP initial ; limites annoncées.
- Capacités déclarées pour recherche, groupes et statut distant. Un attribut AD de désactivation ne peut être présumé sur un annuaire générique. La revalidation de session doit avoir une règle documentée et testée ; sans moyen fiable de vérifier le statut, ne pas annoncer la révocation distante comme couverte. Une opération non supportée est distinguée d’une réponse vide ou d’un compte actif.

LDAP décrit un protocole, pas un schéma d’entreprise uniforme. Références : [authentification/TLS, RFC 4513](https://www.rfc-editor.org/info/rfc4513/), [identifiant entryUUID, RFC 4530](https://www.rfc-editor.org/info/rfc4530/).

## Viabilité de l’administration

| Attente | Viabilité avec LDAP | Condition / limite |
| --- | --- | --- |
| Authentification et sujets stables | Oui | LDAPS par défaut ; clair seulement par consentement explicite ; schéma/codec qualifiés |
| Profils internes, associations directes et droits de stages | Oui | Stockage interne ; aucune écriture dans l’annuaire |
| Recherche et sélection d’approbateurs | Oui | Lecture/recherche autorisée et bornée ; filtrage serveur par workflow/stage/nœud/action |
| Mapping groupes/profils | Oui, facultatif | Stratégie de membership et identifiants stables disponibles ; profils directs restent possibles sans groupes |
| Sessions locales révocables, bootstrap et audit | Oui | Contrats L5 conservés ; protection du dernier accès administratif |
| Désactivation distante et fraîcheur des groupes | Conditionnel | Politique et capacités réelles qualifiées ; aucune réutilisation silencieuse de groupes périmés |
| Création, désactivation, reset de mot de passe LDAP | Non dans l’extension en lecture seule | Actions masquées et refusées côté serveur ; gestion dans l’annuaire ou future extension d’écriture distincte |
| Configuration des fournisseurs | Oui | Secrets référencés et jamais renvoyés ; mutations autorisées et auditées |

La restriction de gestion des comptes distants existait déjà pour AD. Toutes les attentes administratives restent viables sous les conditions déclarées ; la simple compatibilité au protocole ne garantit pas recherche, groupes et statut sur tous les annuaires.

## Qualification et migration

L5a utilise un serveur LDAP non AD réel et reproductible (OpenLDAP recommandé), avec et sans mapping de groupes, sur les OS cibles revendiqués. Vérifier login, identité stable après déplacement/renommage, recherche, codecs, groupes, statut, certificats (y compris bon émetteur mais mauvais nom), pannes, sessions et administration locale. Une simulation ne suffit pas. AD n’est revendiqué que sur preuve dédiée, sans bloquer la cible LDAP non AD.

Le framework n’étant pas encore utilisé, R1 retient une rupture propre : `ActiveDirectoryProvider`, `ActiveDirectoryOptions` et `IActiveDirectoryServiceCredentialProvider` sont remplacés par les contrats LDAP génériques, sans façade de compatibilité ni migration. Une future installation qui change d’annuaire devra néanmoins conserver `ProviderId` et la représentation de `SubjectId`, ou effectuer un mapping explicite audité. Aucun rapprochement par email.

La qualification réelle utilise `vegardit/openldap:2.6.x`, épinglée par digest. Le job Linux exécute les parcours LDAPS réels ; Windows et macOS exécutent les tests unitaires et contractuels sans prétendre disposer d’une instance LDAP. Sur macOS, la qualification fonctionnelle locale peut sélectionner explicitement le mode clair ; cela ne constitue pas une preuve LDAPS.
