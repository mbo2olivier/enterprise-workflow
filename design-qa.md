# Design QA — fidélité de l’intégration L8

Date : 19 octobre 2026.

## Source de vérité

- `prototype/src/App.jsx` et `prototype/src/styles.css` — composition, contenu, interactions et responsive approuvés ;
- `qa-reference-access-viewport.png`, `qa-reference-operations.png`, `qa-reference-settings.png` — captures du prototype aux états Profils, Instances et Réglages ;
- les précédentes captures du shell, de Today, de l’accueil d’administration et de la grille restent applicables.

## Normalisation et preuves

- Viewport desktop : `1280 × 720` CSS px, densité 1, sans chrome navigateur.
- Viewport responsive : `390 × 844` CSS px, densité 1.
- Captures intégrées : `qa-l8-access-fidelity.png`, `qa-l8-operations-fidelity.png`, `qa-l8-settings-fidelity.png`, `qa-l8-settings-mobile.png`.
- Comparaisons côte à côte : `qa-compare-access.png`, `qa-compare-operations.png`, `qa-compare-settings.png` ; prototype à gauche, Razor à droite.
- Les régions tabulation/table et panneaux/champs sont suffisamment lisibles dans les comparaisons complètes ; aucune découpe complémentaire n’est nécessaire.

## Surfaces contrôlées

- Polices et typographie : Inter, niveaux de titres, labels, textes secondaires et densité correspondent au prototype.
- Espacement et rythme : intro, action en haut à droite, contrôles segmentés, tables, bandeau d’exploitation et panneaux de réglages reprennent les mêmes largeurs, marges, bordures et rayons.
- Couleurs et tokens : grille, surfaces, accent, états et contraste utilisent les tokens approuvés.
- Icônes et actifs : chemins officiels Phosphor correspondant aux sémantiques du prototype ; aucun glyphe substitutif ni actif factice.
- Copie et contenu : libellés du prototype conservés ; seules les valeurs issues du Kernel et les états vides honnêtes remplacent les fixtures simulées.

## Interactions contrôlées

- Accès : navigation Profils / Identités / Fournisseurs, recherche/filtre, création et modification d’un profil en modale, et création d’une identité locale en modale.
- Exploitation : navigation Instances / Outbox / Modules et une seule table par vue.
- Réglages : champs éditables de présentation, politique de session en lecture seule, configuration runtime en lecture seule et action d’enregistrement séparée.
- Responsive : aucune largeur de document excédentaire sur Accès, Exploitation et Réglages à `390 px` ; les tables gardent leur défilement local.
- Console navigateur : aucune erreur ni aucun avertissement applicatif.

## Comparaison et historique de correction

1. Première intégration : Accès empilait métriques, cinq panneaux et formulaires ; Exploitation utilisait un tableau de bord inventé ; Réglages mélangeait formulaire et runtime dans une composition étrangère au prototype. Ces écarts étaient P1.
2. Correction : Accès a repris les trois onglets, la liste unique et l’action en haut à droite ; Exploitation a repris le bandeau quatre états, les trois onglets et la table unique ; Réglages a repris les trois panneaux et leurs espacements.
3. Première vérification fonctionnelle : les liens contenant deux paramètres encodaient littéralement `&amp;`, empêchant l’ouverture de l’inspecteur. Le routage a été corrigé et revalidé.
4. Vérification finale : les différences restantes sont des données réelles ou des limites explicites de contrat. Le Kernel ne publie pas encore de projection administrative globale des instances ou de l’outbox ; les tables prévues affichent donc un état vide au lieu d’inventer des lignes.
5. Évolution du 19 octobre : l’inspecteur latéral de profil a été remplacé par une modale sans modifier la table sous-jacente. Une seconde modale reprend la même anatomie pour créer un compte local ; les annuaires externes restent explicitement en lecture seule.
6. Comparaison navigateur directe : les modales du prototype et de l’intégration Razor ont été ouvertes dans le même viewport desktop. En-tête, icône, hiérarchie, champs, voile, actions et rayons concordent. La modale Razor de modification est plus haute uniquement parce qu’elle affiche les permissions réelles inconnues du prototype.
7. Vérification fonctionnelle sur SQLite jetable : création de `modal-qa-user`, création de `modal-qa-profile`, retour sur le bon onglet avec état de succès et apparition immédiate dans la liste. Aucun débordement global ni erreur de rendu observé.

## Résultat

`final result: passed`
