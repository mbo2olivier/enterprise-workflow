# L14 — retrait des anciens artefacts et consultation historique autonome

Date : 24 novembre 2026. Statut : orientation approuvée ; conception détaillée à compléter avant implémentation, code non démarré. Dépendances : L13, contrats de persistance et lecture existants. Décision : [ADR 0031](adr/0031-decouverte-modules-et-retrait-artefacts.md). Studio est repoussé à L15.

## Objectif

Permettre le retrait du chargement d’un ancien module sans supprimer les définitions, instances ou audits historiques. Une définition publiée en base ne suffit plus, à elle seule, à exiger son ancien artefact exécutable. Distinguer retrait du chargement, archivage externe des fichiers et suppression physique : aucune suppression automatique de fichiers ou purge de données n’est introduite.

## Artefacts encore nécessaires à l’exécution

Le calcul des artefacts obligatoires dépend des traitements effectivement encore exécutables, et non de toutes les définitions publiées. Conserver les références exactes `(module, release, hash)` nécessaires aux instances non terminales, tâches ouvertes, attentes/timers/callbacks, travaux ou retries, migrations encore applicables et effets externes dont la livraison reste possible.

Un statut terminal d’instance ne suffit pas si un traitement durable associé peut encore s’exécuter. La conception doit inventorier chaque état du modèle existant, notamment les éléments en échec/dead letter réexécutables. Les exigences incluent les artefacts indirectement nécessaires aux références exactes d’anciens handlers, formulaires, migrations et autres éléments liés. Les détails des requêtes et transitions de cycle de vie doivent être spécifiés et testés sur SQLite et Oracle.

Si un de ces artefacts est absent, incompatible ou remplacé par d’autres octets sous la même release, le Kernel reste non prêt avec un diagnostic expliquant les références bloquantes. Aucun remplacement automatique par une nouvelle version, migration implicite ou abandon silencieux d’un travail.

Une fois les derniers besoins exécutables éteints et la consultation historique autonome disponible, le retrait est permis. La définition concernée reste consultable mais ne permet pas de démarrer une nouvelle instance sans son artefact. Une reprise nécessitant le code ancien exige sa réinstallation exacte ou une migration explicitement prise en charge ; aucune nouvelle fonction de réouverture automatique n’est promise.

## Historique indépendant du code retiré

Assurer une lecture générique des dossiers terminés à partir des données persistées, sans instancier l’ancien module, ses composants, serializers personnalisés ou services métier. Conserver les références de définition et d’artefact, données utiles, étapes, décisions, commentaires, traces et accès autorisés aux pièces jointes. Les formulaires et projections personnalisés nécessitant le code retiré ne sont pas exécutés.

Définir des snapshots de consultation versionnés ou compléter les données déjà persistées : schémas, libellés/replis linguistiques, valeurs et politiques de visibilité nécessaires. L’affichage générique ne doit pas exposer un dump JSON brut ni des champs internes. Un snapshot ne fige pas les droits d’un utilisateur : habilitations, champs visibles et médias sont contrôlés côté serveur à chaque consultation, même lorsque les déclarations du module ne sont plus chargées. L’absence d’une politique exploitable entraîne un refus explicite, jamais une autorisation élargie.

Distinguer l’identité/version historique d’une définition et sa disponibilité pour de nouveaux démarrages. Le retrait ne republie pas une définition et ne modifie pas son JSON canonique, ses hashes ou les décisions d’audit historiques.

## Bases et historiques existants

Ne pas supposer les anciens snapshots suffisants. Avant le code, inventorier les dépendances réelles du parcours de lecture et préciser le format durable, les migrations SQLite/Oracle et le rattrapage nécessaire. Si un dossier ne peut pas être restitué de façon autonome, bloquer le retrait de l’artefact nécessaire avec un diagnostic et une procédure de préparation. Le rattrapage peut nécessiter la présence temporaire du module exact ; ne pas fabriquer des métadonnées manquantes.

Prévoir sauvegarde/restauration et opérations rejouables de préparation avant retrait. La bascule vers la nouvelle réconciliation doit être cohérente avec les workers et la possibilité de nouveaux traitements : pas de fenêtre autorisant du travail privé de son artefact. Les inscriptions historiques d’artefacts ne doivent pas être effacées si elles restent nécessaires à la traçabilité.

Les formats, signatures et mécanismes de migration détaillés restent à arrêter dans L14-0 après inventaire technique. Cette spécification autorise la conception de la solution, pas une purge manuelle des tables pour contourner la rétention actuelle.

## Critères de validation

- Définition historique seule, sans besoin exécutable ni besoin de lecture résiduel : ancien artefact retirable.
- Dernières activations closes mais effet externe/retry encore possible : retrait refusé.
- Dossiers clos et consultation autonome prête : démarrage du Kernel et lecture après retrait réussis sans charger l’ancienne assembly.
- Formulaires personnalisés remplacés par la restitution générique ; autorisations, masquage, médias et langues conservés.
- Ancien dossier insuffisamment préparé : refus explicite du retrait ; après préparation vérifiée, retrait possible.
- Aucun nouveau démarrage d’une définition dont le code est retiré ; code exact réinstallé et migration explicite ne sont pas confondus.
- Non-régression hashes, données, audits, réconciliation, reprise et restauration sur SQLite et Oracle ; matrice plateforme et navigateur ciblée.

Les tests unitaires légers seront exécutés pendant l’implémentation. Les campagnes lourdes seront remises au porteur dans une recette détaillée. Aucun résultat de qualification n’est acquis.
