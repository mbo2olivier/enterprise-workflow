# Preuves L7a — loader versionné et ressources UI

Date : 4 octobre 2026. Décisions D1-A à D4-A consignées dans l'[ADR 0021](../adr/0021-prototype-modules-ui-l7a.md).

## Livrables

- contrat expérimental `IWorkflowModule` séparé du Core ;
- manifeste JSON v1 strict avec compatibilité de contrat, DLL d'entrée, dépendances privées, composants et ressources hachés ;
- `AssemblyLoadContext` non collectible par version d'artefact et partage contrôlé des contrats du Host ;
- composition DI et alimentation du registre immuable L4a sans écrasement silencieux ;
- découverte de composants Blazor compilés et rendu SSR dynamique ;
- endpoint de ressources immuables `/modules/{id}/{version}/{artifactHash}/assets/{path}` ;
- limites configurables, chemins normalisés, refus des liens/reparse points, allowlist MIME et contrôle SHA-256 avant chargement ;
- modules fixtures A/B possédant le même nom d'assembly et des dépendances privées de même nom.

## Validation locale

- deux versions simultanées chargées dans deux contextes distincts et non collectibility confirmée ;
- composants Razor A/B rendus avec leurs dépendances respectives `private-a` et `private-b` ;
- handlers `fixture.approval` versions 1 et 2 présents ensemble dans le registre ;
- collision exacte Host/module refusée indépendamment de l'ordre ;
- ressource versionnée lisible uniquement avec l'empreinte exacte ; altération refusée avant activation ;
- restore verrouillé réussi, build Release sans avertissement et sample durable L6 toujours exécutable ; la suite portée à **92/92 tests** avec L7b reste verte.

## Conclusion

La coexistence est techniquement démontrée par le prototype local et le porteur a ensuite confirmé G3-A pour L7b. Le 5 octobre 2026, le porteur confirme la réussite de la matrice GitHub Actions sur Linux, macOS et Windows ; les fixtures L7a sont donc qualifiées à distance sur les trois OS.
