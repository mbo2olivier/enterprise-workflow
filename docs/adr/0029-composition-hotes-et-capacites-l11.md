# ADR 0029 — composition d’hôtes et capacités optionnelles L11

Date : 18 novembre 2026. Statut : orientations acceptées par le porteur ; détails de mécanismes dans la spécification L11, arbitrages complémentaires tracés séparément.

## Décision

Fournir un builder d’hébergement séparé des bibliothèques d’auteur. Composer le kernel standard à partir de ce socle et préserver sa configuration .NET et ses providers actuels. Déclarer les extensions dans le code compilé de l’hôte.

Fournir des contrats optionnels distincts pour les capacités d’identité, le provisionnement maker/checker et le second facteur. Aucun kernel n’est tenu d’implémenter ces capacités. La configuration ne pilote que l’activation du second facteur ; activation sans handler = erreur de composition, désactivation sans handler = fonctionnement nominal.

Le provider personnalisé assure le provisionnement local et son approbation atomique ; les droits restent dans EW. Le moteur de workflows ne gère aucun challenge d’authentification. `ew dev` garde les identités de son JSON et ne charge pas les providers des hôtes personnalisés.

## Conséquences

Ajouts compatibles aux contrats existants. Les dépendances d’hébergement ne sont pas ajoutées au SDK des modules. Un exemple personnalisé et des tests de conformité doivent montrer les extensions sans représenter une intégration bancaire réelle. Les noms d’API ne sont pas chargés depuis la configuration. Voir [spécification L11](../l11-spec.md).
