# ADR 0015 — Worker, retries et outbox L4b

Date : 3 octobre 2026. **Statut : acceptée.** Source : validation explicite des recommandations 1A à 5A, puis 6A, par le porteur.

## Contexte

Le contrat L3 retournait un bail suffisant pour protéger un commit, mais pas la définition, l’état et la tentative nécessaires à l’exécution. L4b doit exécuter hors transaction longue, récupérer après expiration, appliquer une politique homogène et persister les effets externes sans fenêtre entre transition et intention.

## Décision

- Le claim retourne atomiquement un `ClaimedWork` : bail fenced, définition publiée revalidée, état courant et numéro de tentative.
- Toutes les valeurs opérationnelles sont configurables par entreprise. Les défauts sont : concurrence worker 1, bail 30 s, renouvellement 10 s, polling 250 ms, cinq tentatives métier, backoff exponentiel avec jitter de 1 s plafonné à 30 s et timeout de tentative de cinq minutes.
- Seul un résultat `RetryableFailure` déclenche un retry métier. Une exception non gérée est permanente. Un arrêt, une perte de bail ou une annulation d’hébergement n’est pas committé ; le travail redevient récupérable après expiration. Un timeout est retryable tant que le plafond n’est pas atteint.
- Un succès Service peut retourner des intentions `ExternalEffectIntent`. État, transition et lignes d’outbox sont persistés dans le même commit. La livraison est au moins une fois et emploie une clé stable dérivée de l’instance, de l’activation et de l’opération, jamais du numéro de tentative.
- Une livraison outbox possède son propre bail fenced. Dix tentatives sont autorisées par défaut, avec délais configurables. Une erreur permanente ou l’épuisement place le message en `Failed` sans réouvrir ni faire échouer une instance déjà terminale. Le diagnostic reste durable ; le rejeu administratif est futur.
- Un remplacement d’état par un handler conserve le `SchemaVersion` courant. Un changement de version exige un mécanisme de migration explicite futur.

## Conséquences

Les handlers ne doivent pas effectuer directement un effet de type commande lorsque l’outbox convient ; ils retournent une intention. Le destinataire doit dédupliquer par la clé fournie, car un crash après application mais avant acquittement provoque une redelivery. `Completed` décrit la fin métier et peut coexister avec une livraison `Ready`, `Leased` ou `Failed`.

L’outbox asynchrone ne résout pas une interaction distante requête/réponse dont le résultat conditionne immédiatement l’état. Le plan impose une décision ultérieure sur un modèle durable attente/callback. Il impose également une décision avant toute évolution du schéma d’état.

## Options écartées

Un chargement séparé après claim ajoute une seconde validation de concurrence. Le retry automatique de toute exception rejoue les bugs et les effets ambigus. L’appel externe direct laisse une fenêtre après effet et avant commit. Le retry outbox infini rend les échecs permanents invisibles ; faire échouer l’instance mélange état métier et état d’intégration.

## Validation

Parcours `Start → Service → End`, retry conservant l’activation et avançant la tentative, version d’état préservée, arrêt après application externe sans acquittement, redelivery avec la même clé, déduplication et rejet de l’ancien token. Les migrations et le fencing sont exercés sur SQLite et Oracle.
