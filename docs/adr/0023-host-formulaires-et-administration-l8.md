# ADR 0023 — Host, formulaires, administration et qualification L8

Date : 5 octobre 2026. **Statut : acceptée pour L8-D1-A à L8-D8-A.**

## Contexte

L6 fournit les contrats durables de tâches humaines et L7b le Kernel modulaire. L8 doit livrer l'application métier et l'administration sans exposer l'état brut, contourner la validation serveur ni rendre mutables les décisions figées dans une définition publiée. Le dossier `prototype/` contient déjà le système visuel et les parcours validés.

## Décisions

- **L8-D1-A** : une Razor Class Library `EnterpriseWorkflow.Web` porte les composants réutilisables ; `EnterpriseWorkflow.Kernel` compose l'application Blazor Web App ; `LeaveRequest` qualifie le parcours. SSR statique par défaut, Interactive Server ciblé.
- **L8-D2-A** : les modules enregistrent des descripteurs exacts de processus et de formulaires déclaratifs versionnés. Les composants personnalisés ne remplacent que le rendu ; validation et canonicalisation restent serveur.
- **L8-D3-A** : l'UI reçoit des projections autorisées de champs explicitement déclarés. Le JSON d'état brut n'est jamais une réponse navigateur.
- **L8-D4-A** : le Host choisit le fournisseur d'authentification par configuration et utilise un cookie opaque sécurisé adossé aux sessions révocables L5, avec antiforgery sur les mutations.
- **L8-D5-A** : `Database:MigrationMode` vaut `Validate` par défaut ou `Apply` explicitement. Une commande opérateur ponctuelle applique les migrations ; `Validate` bloque la readiness si le schéma est en retard.
- **L8-D6-A** : modes d'affectation et séparation d'acteurs d'une version publiée sont en lecture seule. L'administration ne modifie que les grants exacts ; changer la sémantique exige une nouvelle définition.
- **L8-D7-A** : seuls les réglages de présentation sûrs sont persistables et audités. Runtime, sécurité, base et secrets restent en lecture seule. Le français du prototype est externalisé et la localisation demeure extensible.
- **L8-D8-A** : contrats et composants restent dans la matrice multi-OS ; Playwright .NET qualifie sur Linux les parcours navigateur SQLite. Oracle et LDAP gardent leurs qualifications réelles dédiées.

Le prototype est traduit fidèlement en Razor/Blazor : shell, pages, interactions, responsive et tokens sont réutilisés. Son runtime React/Vite et ses données simulées ne font pas partie du produit.

## Conséquences

La présentation possède une frontière contractuelle séparée du Core et de la persistance. Un module exact fournit formulaires et composants correspondant à son artefact. Les projections, commandes et contrôles d'autorisation passent par des services applicatifs ; les composants n'accèdent jamais directement aux stores.

Une instance ancienne conserve sa définition et son artefact exacts. Les réglages d'apparence ne changent ni droits, ni comportement runtime. Les déploiements multi-instance peuvent valider le schéma sans lancer simultanément des migrations.

## Validation attendue

Parcours `LeaveRequest` complet sur SQLite et Oracle 19.19, formulaires déclaratif et personnalisé, connexion/session révocable, désignation et inbox filtrées, administration capability-driven, thème, accessibilité/responsive, double soumission contrôlée et tests navigateur Linux. La matrice GitHub Actions doit rester verte sur les trois OS.
