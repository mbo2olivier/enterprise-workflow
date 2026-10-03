# ADR 0014 — Identité, résultats et validation des handlers L4a

Date : 1er octobre 2026. **Statut : acceptée.** Source : validation explicite des recommandations 1A, 2A et 3A par le porteur.

## Contexte

Une définition L2 contient déjà une référence durable de handler composée d’un identifiant technique et d’une version positive. L4a doit relier cette référence à une classe concrète sans sérialiser de type CLR, ouvrir un scope DI par tentative et refuser les configurations impossibles avant qu’un worker L4b ne les exécute.

## Décision

- La clé d’un handler est globale dans une installation : `(HandlerId, HandlerVersion)`. Les modules emploient des identifiants qualifiés par leur domaine et les collisions sont refusées ; le registre ne choisit jamais selon l’ordre d’enregistrement.
- Les contrats distinguent service et décision. Un succès peut remplacer l’état ; une décision réussie fournit exactement une issue nommée. Les deux rôles peuvent retourner un échec retryable ou permanent avec un code technique.
- Un handler ne choisit ni prochain nœud, ni délai de retry, ni transaction. Le runtime interprète le graphe et la politique L4b.
- Toutes les registrations directes sont collectées au démarrage puis figées dans un registre immuable. Une clé dupliquée échoue pendant la composition.
- Chaque définition est validée contre ce registre avant publication : clé absente ou rôle incompatible interdit la publication. La résolution exige ensuite la clé et la version exactes, sans fallback.
- Chaque tentative obtient un scope DI court et distinct. Le contexte contient uniquement instance, activation stable, nœud, tentative, état et configuration canoniques ; aucun secret, service provider ou contrôle de transition.

## Options écartées

Qualifier chaque référence par l’identité du module aurait changé le schéma canonique L2 alors que la convention globale et le refus de collision suffisent au registre initial. Autoriser les handlers à retourner une destination ou un délai aurait déplacé les invariants du moteur dans le code métier. Autoriser les références non résolues jusqu’à la première exécution aurait transformé une erreur de configuration en panne tardive.

## Conséquences

Les applications nomment les handlers dans un espace global et doivent construire le registre avant de publier des définitions exécutables. L7 pourra alimenter le même registre depuis des manifestes, mais ne pourra pas écraser une clé. La coexistence physique des DLL et leur drainage restent Q07 ; cette décision ne les anticipe pas.

## Validation

Tests de doublon, ordre ordinal et versions, clé absente, rôle incompatible, définition complètement liée, absence de fallback, création/disposition d’un scope par tentative et invariants des résultats/contextes.
