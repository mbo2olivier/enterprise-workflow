# ADR 0016 — Sécurité, sessions et profils internes L5

Date : 3 octobre 2026. **Statut : acceptée.** Source : validation du porteur de 1A à 5A, avec configuration complète de la politique de sécurité, remplacement de 6 par des profils internes et acceptation de la scission de G2.

## Décisions

- Séparer authentification, autorisation, annuaire, administration et affectation. Identité stable `(ProviderId, SubjectId)` ; un fournisseur d’authentification explicitement choisi par parcours et un fournisseur d’autorisation configuré. Aucun essai automatique des credentials auprès de plusieurs fournisseurs ni agrégation implicite de leurs autorisations. Une indisponibilité d’autorisation refuse l’opération.
- Initialiser le premier administrateur par commande locale ponctuelle et auditée, avec une identité locale ou externe. Empêcher le retrait du dernier accès administratif connu ; récupération par opération locale explicite et auditée, sans endpoint public permanent ni compte universel.
- Sessions révocables côté serveur. Défauts : 30 minutes d’inactivité, huit heures de durée maximale et intervalle maximal de cinq minutes pour recontrôler le statut distant. Revalider les permissions à chaque commande sensible. Toutes les durées, fréquences de revalidation, règles de cache, verrouillage et paramètres opérationnels de sécurité sont pilotables depuis la configuration de l’entreprise ; valider leur cohérence au démarrage. Ces valeurs ne sont pas des constantes produit. Documenter le délai effectif de révocation distante et le comportement en cas de panne ; ne pas promettre de révocation instantanée.
- AD en lecture seule, authentification identifiant/mot de passe sur TLS validé, un domaine initial. Choisir la bibliothèque après prototype Windows/Linux/macOS et vérifier les politiques du domaine réel. Les groupes sont une capacité facultative : leur absence ne bloque pas l’authentification ni les affectations directes de profils. Qualification AD réelle nécessaire pour clôturer L5.
- Comptes locaux dans une extension optionnelle fondée sur ASP.NET Core Identity, hors Core ; stockage et migrations distincts, qualifiés sur SQLite et Oracle avant clôture de L5.
- Introduire des profils internes administrables : un profil est un ensemble de permissions. L’administration permet d’associer une identité stable directement à un profil existant et de mapper un groupe AD stable vers un profil existant. Une entreprise peut utiliser AD uniquement pour l’authentification et l’annuaire, et gérer intégralement ses permissions par profils internes sans groupes AD. Une entreprise utilisant les groupes AD peut activer leur mapping. Les profils ne créent ni ne modifient les groupes AD.
- Le fournisseur interne d’autorisation évalue les profils et les contraintes de la ressource indépendamment du fournisseur d’authentification. Les extensions d’autorisation distantes restent possibles par sélection explicite. Aucun profil, y compris administratif, ne contourne affectation, état terminal, révision ou règle d’auto-approbation.

## Précisions à finaliser pendant la conception L5

Les signatures C#, le modèle de persistance et les permissions exactes restent à détailler. Proposition technique, non arbitrage accepté implicitement : autoriser plusieurs profils par identité et cumuler les permissions des profils directs et des mappings de groupes au sein du seul fournisseur interne ; aucune permission en l’absence d’association. Formaliser avant implémentation la fraîcheur des appartenances, le traitement des groupes indisponibles et les effets du retrait d’un profil, sans conserver silencieusement des droits périmés.

La source du responsable hiérarchique, initialement ouverte sous Q11, est désormais résolue par l’[ADR 0017](0017-designation-approbateur.md) : désignation explicite parmi les utilisateurs habilités, au démarrage ou lors de l’activité concernée. Les profils déterminent l’éligibilité ; la désignation détermine l’affectation. Le rendu précis Razor/Blazor reste ouvert sous Q09b ; l’accord sur la scission des portes ne vaut pas sélection d’un mode de rendu.

## Portes et validation

G2 devient la porte sécurité L5, franchie pour les choix ci-dessus ; G2a porte les contrats métier avant L6, notamment affectation et responsable ; G2b porte le rendu UI avant L7a/L8. Les prérequis de qualification AD restent opérationnels et ne sont pas présumés disponibles.

L5 doit vérifier les parcours local et AD, identité AD avec profil direct sans groupes, groupe AD mappé vers un profil, gestion autorisée et auditée des profils/associations, refus d’accès sans permission, révocation, indisponibilité et protection du dernier accès administratif. Tester les stores locaux et de profils sur SQLite et Oracle. Une acceptation documentaire ne constitue pas une preuve d’implémentation.
