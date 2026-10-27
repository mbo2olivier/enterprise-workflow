# ADR 0028 — État typé obligatoire et commentaires déclaratifs d’approbation

Date : 26 octobre 2026. Statut : accepté par le porteur ; implémenté localement dans l’ultime itération du L9b, qualification finale en cours.

## Contexte

Le state DTO, sa version explicite et le mapping indépendant des formulaires ont été adoptés par l’ADR 0026. Le handler standard d’approbation vérifie actuellement la présence d’un commentaire requis mais ne le conserve pas dans le state. Demander un handler métier pour cette seule copie alourdit la DX. Aucun framework n’a encore été publié : conserver les anciens parcours non typés ne répond à aucune dépendance utilisateur publiée.

## Décisions

1. Tout processus déclare son state par `WithState<TState>()`, avec version de schéma explicite. Le fluent propage ce type jusqu’aux actions. Le stockage interne reste en JSON canonique indépendant des types CLR.
2. Exposer `ReadState<T>()`, `ReadSubmission<T>()` et `NodeStateUpdate.Replace<T>()` : lecture d’objets détachés et remplacement complet explicite, validé avant commit. Aucun suivi automatique des mutations ni fusion implicite des formulaires.
3. `RequireCommentAt(s => s.Comments)` ajoute au state un commentaire obligatoire ; `CommentAt(...)` conserve un commentaire facultatif. La cible est une collection structurée de `WorkflowComment` contenant texte, étape, action, acteur et date serveur. Le chemin JSON est compilé depuis l’expression en respectant les noms sérialisés, sans persister de delegate.
4. Le handler d’approbation intégré effectue ce mapping et la préparation de l’état ; le runtime valide le résultat avant le commit transactionnel de la décision, du state et des autres effets. Droits, concurrence et idempotence restent applicables. Le replay ne duplique pas les entrées. Ne pas ajouter de handler métier uniquement pour stocker un commentaire d’approbation.
5. Aucune couche de rétrocompatibilité n’est requise pour les anciennes API de développement non publiées. Adapter les tests internes, fixtures, exemples, templates et hôtes embarqués aux états typés ; retirer les parcours et tests de compatibilité devenus inutiles, en conservant la couverture métier pertinente.
6. Cette dispense prépublication ne supprime ni les migrations de schéma, ni l’immutabilité des définitions publiées, ni la poursuite future des instances sur leur version d’origine. Elle ne demande aucune réinitialisation des données locales dans cette mise à jour documentaire.

## Effet sur les décisions précédentes

Amende les ADR 0026 et 0027 uniquement sur la conservation des contrats d’auteur historiques sans DTO avant publication. Les autres garanties de validation, de visibilité et de séparation des permissions demeurent applicables.

## Acceptations

Refus d’un processus sans DTO ; inférence du type dans le fluent ; refus d’une cible de commentaire incompatible ; métadonnées serveur non falsifiables ; commentaire facultatif vide sans entrée ; validation échouée sans mutation ; décision et commentaire atomiques ; replay sans doublon ; commentaires RH/manager conservés dans l’exemple ; tests et templates migrés sans couche de compatibilité non typée.

Voir la [spécification L9b](../l9b-spec.md) et le [plan](../implementation-plan.md).
