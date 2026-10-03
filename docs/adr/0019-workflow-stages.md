# ADR 0019 — Stages et habilitations par nœud et action

Date : 3 octobre 2026. **Statut : granularité requise et stage = nœud acceptés (S12/S13) ; modèle technique et modes d’affectation proposés, à arbitrer avant L5b.**

## Besoin confirmé

Un workflow traverse plusieurs postes. À chaque étape métier, seuls les individus ou profils habilités peuvent effectuer les actions du nœud actif. L’administration doit gérer ces droits sans donner implicitement accès aux autres étapes du même workflow.

Précision du porteur (S13) : un stage est le nœud du workflow vu par l’administration. Il relie cette étape à ses participants éligibles ; aucune entité Stage distincte ni mapping stage/nœud n’est introduit. Il ne constitue ni un groupe LDAP ni un profil de sécurité : les mêmes profils peuvent servir plusieurs stages et les individus peuvent être habilités directement. Il ne change pas le MVP séquentiel à branches exclusives.

## Modèle proposé

La définition versionnée expose les `NodeId` stables existants et les actions de chaque nœud interactif. « Stage » est le terme métier utilisé dans l’administration pour ce nœud ; aucun `StageId` ni regroupement supplémentaire n’est nécessaire. Les nœuds techniques ne requièrent pas de participant humain ; une notification ne donne aucun droit à son destinataire. Le stage actif se déduit du nœud/activation durable, jamais d’un paramètre client.

La définition contient les nœuds et actions possibles ; l’administration de l’installation contient les habilitations des identités/profils, sans embarquer les personnes ou groupes LDAP dans un module. Le compilateur vérifie unicité et références des nœuds ainsi que leurs actions ; la publication vérifie la cohérence des politiques locales. Une étape sans candidat est diagnostiquée, sans choisir automatiquement une autre personne.

Une habilitation cible `(WorkflowId, DefinitionVersion, NodeId, ActionId)` et un profil interne ou une identité stable. Les groupes LDAP facultatifs alimentent les profils ; leur présence n’est pas nécessaire. Recommandation : grants positifs explicites, refus par défaut, pas de wildcard implicite ni de priorité Allow/Deny à inventer. Droits globaux d’administration séparés ; les anciennes permissions globales ne deviennent pas des grants pour tous les stages.

La lecture d’une instance, de l’historique ou d’un formulaire est autorisée séparément de sa réalisation. Les règles de visibilité doivent borner les données consultables ; être participant à un stage ne donne pas automatiquement accès à toutes les données des autres stages. Le catalogue des actions distingue au minimum lecture de tâche, saisie/soumission, approbation, refus et affectation selon le nœud ; démarrage et annulation restent des actions distinctes au niveau workflow/instance.

Pour réaliser une action, le serveur exige : identité/session valide, habilitation dans le contexte exact, nœud/tâche actif, action déclarée, affectation/claim valide, révision valide et contraintes métier (dont auto-approbation). Allow du fournisseur ne contourne aucun de ces contrôles. Filtrage de l’inbox, recherche des candidats et commandes API utilisent la même règle, revalidée lors de la soumission. Habilitation retirée après sélection ou claim ⇒ action refusée.

La complétion, la clôture de l’attente et la progression sont atomiques sur chaque store. Un double clic, une complétion concurrente ou une ancienne activation ne produit aucune double progression. L’audit identifie acteur, workflow/version, instance, nœud (stage)/activation, action et révision de politique, sans secret.

## Affectation et ADR 0017

L’ADR 0017 reste valide pour les approbations à destinataire explicite : le choix se fait désormais parmi les candidats habilités pour le stage, nœud et action précis. Un stage définit l’éligibilité ; l’affectation désigne qui traite une tâche particulière.

Un pool de makers doit pouvoir être distingué d’un destinataire choisi. Recommandation R2 : mode explicite par nœud, `DesignatedIdentity` pour l’approbateur de 0017 et `EligiblePool` avec claim exclusif atomique pour les tâches de poste. Ce second mode n’est pas déjà accepté par 0017. Aucune réaffectation, délégation ou escalade silencieuse ; ces opérations nécessitent un contrat et un droit dédiés.

## Versions et compatibilité

La version de définition/les références de nœud et les actions d’une instance restent figées. Recommandation R3 : droits locaux relus à chaque commande, révocation immédiate des grants dans le store, modification administrative auditée et versionnée. Les anciens workflows techniques L2/L4 continuent à fonctionner ; aucun accès humain générique ajouté pour les rendre compatibles. Un nœud interactif sans contrat d’actions et habilitations explicites ne peut être publié comme traitable.

Les policies d’affectation et les formulaires gardent leurs versions propres. Ne pas remapper un nœud ancien sur une nouvelle définition par son seul libellé. Les stores de sécurité et le contexte d’autorisation L5 évoluent avec migrations SQLite et Oracle ; toute évolution du contrat des actions de nœud exige de préserver lecture, sérialisation et hash des définitions historiques. Aucun ajout de StageId au modèle canonique n’est requis.

## Exemple et validation

Clientèle (`soumettre`) → superviseur (`approuver`/`refuser`) → back-office maker (`saisir`/`soumettre`) → back-office validateur (`valider`/`refuser`) → notification technique. Les participants d’un poste ne peuvent pas agir sur un autre poste, même dans la même instance. Le parcours est séquentiel ; une notification ne vaut pas attente d’une réponse distante.

T23/T24 doivent couvrir les droits croisés stage/nœud/action/version, appel API direct avec faux contexte, retrait des droits, profil administratif sans droit métier, visibilité, deux claims concurrents, double complétion, reprise et conservation des versions sur SQLite et Oracle. Une éventuelle séparation maker/checker entre stages est R4 ; l’interdiction actuelle d’auto-approbation ne prouve pas à elle seule cette séparation.
