# ADR 0030 — distributions, CLI compilée et identité visuelle

Date : 19 novembre 2026. Statut : accepté pour implémentation, sur décisions du porteur.

## Contexte

L11 fournit la composition d’un Kernel personnalisé. L12 permet à une organisation de distribuer sa CLI et sa composition métier en développement sans fork du framework. Studio reste L13.

## Décision

Extraire le socle CLI réutilisable et conserver un exécutable `ew` minimal. Séparer builder, application et exécution. Réexécuter le point d’entrée organisationnel dans les enfants pour conserver les extensions compilées. Fournir un contexte public pour les commandes et la composition dev ; préserver les services réservés et les identités de test.

Partager explicitement les assemblies de contrats côté hôte. Le module déclare uniquement la compatibilité du runtime complet, avec identifiant et plage traitée par NuGet.Versioning. Aucun registre de compatibilité par service, aucune déclaration de services requis, aucune validation exhaustive de DI anticipée. Les artefacts antérieurs restent lisibles ; l’hôte peut imposer une déclaration aux nouveaux modules.

Une distribution publie un ensemble exact de versions. Les templates sont préparés explicitement, jamais installés implicitement pendant la génération. Les credentials du feed restent hors des packages.

La marque est verrouillée par défaut dans les hôtes personnalisés ; la composition autorise les champs administrables. Les réglages historiques du Kernel standard sont explicitement autorisés. La politique est appliquée à la lecture et à la mutation serveur.

## Conséquences

Le socle CLI emporte les dépendances nécessaires à l’hébergement dev ; aucun projet abstrait vide n’est créé pour simuler un découpage. Les noms et signatures effectifs seront documentés à la livraison. L’auteur assume la qualification de ses plages runtime. Les tests lourds sont fournis au porteur sous forme de recette exécutable, sans les lancer pendant cette implémentation.

## Évolution de plan du 24 novembre 2026

La mention historique du Studio L13 ci-dessus est remplacée dans la feuille de route par L15 : L13 couvre désormais la découverte des modules, L14 le retrait des anciens artefacts et la consultation historique autonome. Voir [ADR 0031](0031-decouverte-modules-et-retrait-artefacts.md). Les décisions de composition L12 restent inchangées.
