# ADR 0026 — État métier typé, visibilité et validation

> Amendement accepté le 26 octobre 2026 : voir [ADR 0028](0028-etat-type-obligatoire-et-commentaires-approbation.md). Le DTO devient obligatoire et la compatibilité avec les anciennes API d’auteur non publiées n’est pas requise. Les autres décisions restent applicables.
Statut : **acceptée le 25 octobre 2026** par le porteur ; implémentation à réaliser en L9b. Les signatures illustratives restent à figer. A1–A7 de L9b sont acceptés séparément le même jour dans l’[ADR 0027](0027-consultation-medias-audit-tat-l9b.md).

## Contexte

Les formulaires possèdent déjà des contrats de champs et des audiences ; les handlers peuvent mettre à jour l’état JSON. Le démarrage utilise actuellement la soumission comme état initial. La vue générale d’instance nécessite un contrat explicite des données métier, indépendant des entrées des formulaires. La version d’état explicite et les migrations existent (ADR 0022).

## Décision

1. Le processus déclare un DTO d’état métier par une façade de type `WithState<TState>()`, avec version explicite (`WithStateSchemaVersion(...)`) et migrations existantes. Aucun versionnement automatique ne remplace ce choix. Une empreinte éventuelle ne sert qu’au diagnostic.
2. Les métadonnées framework restent séparées. Le DTO est une façade SDK ; la persistance JSON, le modèle canonique indépendant de CLR et les contrats historiques sont préservés.
3. Les annotations du DTO définissent noms persistés stables, libellés/traductions, visibilité dans le détail et validation. Tous les champs déclarés sont affichables par défaut aux lecteurs autorisés de l’instance. Un champ caché reste exclu ; les restrictions par permissions métier distinguent any/all et s’appliquent aux sous-champs.
4. Autorisation d’instance puis filtrage serveur des champs, avant envoi au navigateur. La visibilité du détail ne remplace pas les autorisations propres aux tâches, formulaires et médias.
5. Contrats de formulaires et state indépendants ; mapping explicite dans les handlers, y compris au démarrage. Pas de copie/fusion automatique par égalité de noms. Le runtime valide et committe l’état produit ; les résultats existants restent le point d’intégration des handlers humains/services.
6. Validation permanente avant persistance et groupes contextuels associés explicitement aux nœuds/actions. Contexte calculé par le serveur ; validateurs personnalisés pour règles croisées. Échec : pas de commit d’état, transition ni effets transactionnels associés. Les migrations valident le contrat cible.
7. La vue de détail sélectionne les données de l’état courant à la lecture, sans seconde copie persistée à alimenter. Les projections TAT sont indépendantes.

## Conséquences

L9b doit ajouter compilation/inspection des contrats DTO, sérialisation stable, diagnostics, mapping initial et validation sur les chemins de mise à jour, ainsi que qualification des anciennes instances/migrations. Les métadonnées de présentation sont liées à la définition/artefact exact ; leur modification ne réécrit aucun contrat historique. Un changement de présentation seul ne requiert pas de migration de données compatible. L’ajout d’un champ étant visible par défaut, aperçu et documentation doivent le signaler.

La syntaxe des attributs, l’API du handler initial et les façades typées doivent être stabilisées à l’implémentation sans modifier ces principes. La spécification détaillée et les acceptations figurent aux [§1.1–1.3 de L9b](../l9b-spec.md).

## Mise en œuvre locale

Implémentation L9b ajoutée le 25 octobre 2026 ; contrats effectifs dans le [guide](../l9b-sdk.md), résultats et limites dans la [preuve](../evidence/l9b.md). Le statut accepté ne constitue pas une qualification globale du lot.
