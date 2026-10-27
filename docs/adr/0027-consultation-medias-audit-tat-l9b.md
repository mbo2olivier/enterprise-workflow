# ADR 0027 — Consultation, recherche, audit, médias et TAT L9b

> Amendement accepté le 26 octobre 2026 : voir [ADR 0028](0028-etat-type-obligatoire-et-commentaires-approbation.md). Le DTO devient obligatoire et la compatibilité avec les anciennes API d’auteur non publiées n’est pas requise. Les autres décisions restent applicables.
Statut : **acceptée le 25 octobre 2026**. Le porteur accepte les recommandations A1 à A7 de la [spécification L9b](../l9b-spec.md). Implémentation et qualification à réaliser ; cette décision complète l’ADR 0026 sur le state DTO.

## Décisions

- **A1 — Lecture** : conserver `instance.read`, sans droit métier automatique pour l’initiateur ou l’administrateur. La recherche couvre toutes les instances déjà lisibles par l’acteur. Filtrer les champs selon le DTO pour les nouveaux contrats, préserver les contrats historiques. Le filtre initiateur exige une capacité administrative explicite et n’élargit pas les droits de lecture. Contrôles serveur sur recherche, détail et téléchargements.
- **A2 — Audit** : rechercher/exporter l’audit de sécurité existant. Ajouter explicitement les liens de corrélation nécessaires ; pas d’agrégation universelle des logs techniques/métier dans ce lot.
- **A3 — Exports** : CSV UTF-8 synchrone en flux, bornes de période/taille/durée configurables, refus explicite en cas de dépassement, sans troncature silencieuse. Même socle pour audit et TAT ; droits distincts, contrôle serveur, audit de l’extraction et protection contre les formules tableur. Jobs asynchrones et XLSX ne sont pas retenus.
- **A4 — Médias** : API commune extensible et adaptateur disque privé livré pour Kernel, embedded et `ew dev`. Identifiant logique stable, révisions binaires immuables, suppression logique et purge selon rétention/références. Volume partagé requis pour plusieurs hôtes utilisant ce provider ; sauvegarde/restauration cohérente des métadonnées et binaires. Staging et finalisation/nettoyage idempotents gèrent l’absence de transaction commune SQL/fichiers. BLOB SQL et adaptateur objet ne sont pas des livrables retenus.
- **A5 — Politique fichier** : hook antivirus et états de validation, limites/types configurables, activation selon l’installation. Aucun scanner réel livré ou requis comme gate de L9b. Limites, quotas et rétentions doivent être fixés/configurés avant production, sans effacement silencieux par défaut.
- **A6 — TAT** : durée calendaire UTC ; distinguer attente, affectation et complétion. Ne pas assimiler ces mesures au travail actif. Suivi prospectif avec import des seuls faits historiques prouvables, couverture partielle marquée et exclue des moyennes par défaut. Calendriers ouvrés et pauses actives ne sont pas retenus.
- **A7 — Cohérence TAT** : mutation source, événements TAT et projections normalisées dans la même transaction SQL, sur tous les chemins concernés et les deux providers. Unicité, révisions/fencing et reconstruction versionnée assurent reprise et absence de double comptage. Échec de projection : rollback de la mutation. Mesurer le coût ; projection asynchrone et best effort ne sont pas retenus.

## Conséquences et limites

L9b-0 matérialise ces décisions dans les contrats, schémas et configurations ; L9b-1 à L9b-5 implémentent et qualifient les parcours. Les signatures publiques, valeurs opérationnelles de limites/rétention et objectifs de performance Q06 restent à stabiliser, sans rouvrir ces décisions. Les critères de sortie, scénarios négatifs et limites sont décrits dans la spécification L9b ; aucune preuve L9 n’est automatiquement étendue à L9b. L10 reste conditionné à la qualification de L9b et au gel de ses contrats.

## Mise en œuvre locale

Implémentation L9b ajoutée le 25 octobre 2026 ; contrats effectifs dans le [guide](../l9b-sdk.md), résultats et limites dans la [preuve](../evidence/l9b.md). Le statut accepté ne constitue pas une qualification globale du lot.
