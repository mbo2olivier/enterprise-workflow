# Préparation du lot L8

Date : 5 octobre 2026. Statut mis à jour le 7 octobre 2026 : recommandations L8-D1-A à L8-D8-A acceptées et implémentées ; solution complète, Playwright Linux, Oracle Enterprise 19.19 et GitHub Actions qualifiés avec succès. Voir [la procédure L8](l8-validation.md), l'[ADR 0023](adr/0023-host-formulaires-et-administration-l8.md) et la [preuve du lot](evidence/l8.md).

## Point de départ acquis

L6 fournit les tâches humaines, l'affectation, le claim, l'inbox, les formulaires référencés par identifiant/version et les commandes serveur sécurisées. L7b fournit les modules exacts, les composants Razor compilés et les ressources immuables. La matrice GitHub Actions Linux/macOS/Windows est verte et SQLite/Oracle Enterprise 19.19 sont qualifiés sur les lots précédents.

Le dossier `prototype/` est la source de vérité UI : shell métier et administratif, navigation, pages Today/catalogue/inbox/détail/demandes, action dock, écrans d'accès/workflows/exploitation/audit/réglages, responsive et tokens visuels. L8 traduit ces surfaces en Razor/Blazor ; il ne conserve ni React/Vite dans le produit, ni les données simulées.

## Décisions confirmées

### L8-D1 — topologie du produit web

**Contrainte.** Le Kernel doit être exécutable comme produit intégré, tandis que L9 doit encore permettre l'usage embarqué. Mettre toutes les pages dans un exécutable rendrait leur réutilisation difficile ; transformer le prototype en SPA React introduirait un second runtime contraire à D1-A.

**Recommandation A.** Créer une Razor Class Library réutilisable `EnterpriseWorkflow.Web` pour les composants, formulaires et services de présentation, un exécutable Blazor Web App `EnterpriseWorkflow.Kernel` pour la composition/configuration, et un module/sample `LeaveRequest` pour le parcours de qualification. SSR statique par défaut ; Interactive Server uniquement sur les îlots qui en ont besoin.

**Alternative B.** Tout placer dans `EnterpriseWorkflow.Kernel`. Moins de projets immédiatement, mais réutilisation embarquée et tests de composants plus difficiles.

**Alternative C.** Conserver le prototype React comme frontend et exposer une API. Cela dupliquerait navigation, session et validation et contredirait le choix Razor/Blazor déjà accepté ; option déconseillée.

### L8-D2 — catalogue versionné des processus et formulaires

**Contrainte.** L6 ne persiste qu'une `WorkflowFormReference`; il ne définit ni champs, ni formulaire de démarrage, ni texte de catalogue. Une ancienne instance doit continuer à résoudre le formulaire exact de son artefact, et un composant personnalisé ne doit pas pouvoir contourner la validation serveur.

**Recommandation A.** Ajouter des contrats de présentation indépendants de Razor : descripteur de processus exact, formulaire de démarrage, formulaires de tâche, champs déclaratifs (`ShortText`, `LongText`, `Date`, `Number` à représentation décimale, `Boolean`, `Choice`), contraintes bornées et éventuel identifiant de composant personnalisé. Le module exact enregistre ces descripteurs au démarrage. Le serveur valide et canonicalise toujours la soumission ; le composant personnalisé ne remplace que le rendu.

**Alternative B.** Encoder les formulaires dans la configuration JSON des nœuds. Cela mélange présentation et moteur, rend la validation moins typée et complique le formulaire de démarrage.

**Alternative C.** Laisser chaque composant Razor posséder son propre contrat et sa validation. Plus flexible, mais impossible à administrer et à sécuriser uniformément ; option déconseillée.

### L8-D3 — projection et visibilité des données

**Contrainte.** Le droit `instance.read`, le droit d'agir sur une tâche et le droit de voir chaque donnée ne sont pas équivalents. Exposer le JSON d'état complet au composant ou au navigateur violerait R7-A et rendrait les composants personnalisés trop puissants.

**Recommandation A.** Introduire un service de lecture applicatif autorisé qui projette uniquement les champs explicitement déclarés par le formulaire exact. Les champs indiquent leur contexte de lecture/écriture borné (initiateur, participant de la tâche, lecteur autorisé) ; l'état canonique brut n'est jamais envoyé au navigateur. Les composants personnalisés reçoivent la même projection filtrée.

**Alternative B.** Tout montrer à qui possède `instance.read`. Simple, mais confond visibilité de l'instance et accès aux données ; option déconseillée.

**Alternative C.** Introduire dès maintenant un DSL général de politiques par champ. Très puissant, mais disproportionné pour le MVP et difficile à auditer.

### L8-D4 — authentification et session du Host

**Contrainte.** Les fournisseurs L5 authentifient et le store gère déjà les sessions révocables, mais aucune intégration cookie Blazor n'existe. Laisser le navigateur choisir arbitrairement un fournisseur ferait circuler le même mot de passe vers plusieurs destinations possibles.

**Recommandation A.** Le Host sélectionne par configuration le fournisseur actif du parcours de connexion, émet un cookie opaque `Secure`, `HttpOnly`, `SameSite=Lax` adossé à `SecuritySessionManager`, applique antiforgery aux mutations et reconstruit l'identité serveur à chaque requête/circuit. Aucun token de session n'est accessible au JavaScript.

**Alternative B.** Cookie ASP.NET contenant durablement les claims. Intégration standard, mais révocation et fraîcheur du fournisseur seraient dupliquées ou affaiblies.

**Alternative C.** Bearer token stocké côté navigateur. Adapté à une SPA/API, mais inutile et plus exposé pour le Host Blazor intégré.

### L8-D5 — application des migrations de schéma SQL

**Contrainte.** Les stores exigent actuellement un `MigrateAsync` explicite. Une application automatique en production peut prendre des verrous ou modifier un schéma au mauvais moment ; un mode exclusivement manuel rend en revanche le sample et le développement inutilement pénibles. Cette question avait été volontairement reportée jusqu'au premier Host complet.

**Recommandation A.** Ajouter `Database:MigrationMode = Validate | Apply`, avec `Validate` par défaut. `Apply` est explicite pour développement/sample et une commande opérateur ponctuelle du Kernel applique les migrations avant démarrage. En mode `Validate`, un schéma en retard bloque la readiness avec un diagnostic sans le modifier.

**Alternative B.** Appliquer systématiquement au démarrage. Très simple, mais risqué pour Oracle et les déploiements multi-instance.

**Alternative C.** Ne jamais migrer depuis le Kernel. Sûr opérationnellement, mais nécessite dès L8 un outil externe que L9 n'a pas encore livré.

### L8-D6 — limites de mutation de l'administration

**Contrainte.** Le prototype permet visuellement de choisir `Identité désignée` ou `Pool éligible`, mais L6 inscrit ce mode dans la définition immuable. Le modifier sur une version publiée changerait rétroactivement la sémantique des instances. Les grants, eux, sont conçus pour être modifiés et relus immédiatement.

**Recommandation A.** Afficher le mode d'affectation et la séparation d'acteurs en lecture seule pour la version publiée ; permettre uniquement grant/revoke sur les actions exactes. Un changement de mode exige une nouvelle version de définition/module. Le prototype est conservé visuellement, mais les cartes de mode deviennent informatives dans la vue publiée.

**Alternative B.** Ajouter une surcouche administrative mutable. Elle contredirait les snapshots/versionnements L6 et demanderait une nouvelle politique de migration des instances ; option déconseillée.

### L8-D7 — réglages, thème et langues

**Contrainte.** Le prototype mélange réglages d'apparence modifiables et paramètres runtime/sécurité. Modifier à chaud les baux, migrations, fournisseurs ou secrets depuis l'UI contournerait la configuration validée au démarrage. Les textes doivent être externalisés et le prototype validé est en français.

**Recommandation A.** Persister seulement les réglages de présentation sûrs (nom affiché, logo référencé, couleur d'accent validée, fuseau et culture) avec audit. Afficher runtime, base et politiques de sécurité en lecture seule. Livrer les ressources françaises du prototype et une structure de localisation prête à recevoir d'autres cultures, sans retarder L8 par une traduction anglaise complète. Aucun CSS arbitraire ni secret n'est saisi dans l'UI.

**Alternative B.** Tout garder en configuration et rendre l'écran Réglages entièrement en lecture seule. Moins de schéma, mais ne satisfait pas la personnalisation intégrée attendue.

**Alternative C.** Rendre aussi les paramètres de sécurité/runtime persistants et modifiables. Cela exige coordination multi-instance, reprise et règles de priorité configuration/base non cadrées ; option déconseillée au MVP.

### L8-D8 — qualification navigateur

**Contrainte.** Les tests de services et de composants ne détectent pas les erreurs de navigation, antiforgery, focus, clavier, responsive ou chargement des assets. Installer et exécuter un navigateur sur les trois runners augmenterait fortement coût et instabilité sans qualifier Oracle ou LDAP sur ces OS.

**Recommandation A.** Tests unitaires/contrats/composants sur la matrice existante, plus Playwright .NET sur Linux pour les parcours navigateur SQLite : connexion, démarrage, désignation, inbox, claim, double soumission, administration, thème, clavier et mobile. Oracle rejoue le parcours applicatif serveur et un smoke Host séparé ; LDAP réel reste qualifié par le job Linux dédié.

**Alternative B.** Playwright sur les trois OS. Couverture navigateur maximale, mais temps, téléchargements et flakiness multipliés.

**Alternative C.** Tests de composants seulement. Plus rapides, mais insuffisants pour revendiquer T20.

## Découpage proposé après décision

1. **L8a — contrats et Host** : catalogue de présentation, projections autorisées, Kernel, session cookie, migration/validation de schéma et traduction du shell du prototype.
2. **L8b — parcours métier** : catalogue, formulaire de congé, désignation, démarrage, Today, inbox, détail/action dock, Mes demandes et composant personnalisé.
3. **L8c — administration** : accès, workflows/grants, exploitation, audit et réglages conformes aux capacités réelles.
4. **L8-Q — qualification** : SQLite, Oracle Enterprise 19.19, Playwright Linux, accessibilité/responsive, puis matrice GitHub Actions.
