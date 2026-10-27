# ADR 0010 — Distribution par RID et qualification explicite

Date : 30 septembre 2026.

**Statut : Proposée pour le packaging ; périmètre plateformes accepté.**

## Contexte

Les utilisateurs doivent pouvoir compiler ou utiliser des versions préconstruites sans ajout gratuit d’infrastructure.

## Décision ou proposition

Proposer packages bibliothèques, modèles/CLI et archives Host par RID qualifié. Commencer par publications non trimmées, sans Native AOT, pour préserver chargement dynamique. Évaluer archives autonomes et dépendantes du runtime avec coûts de mise à jour explicites.

## Options considérées

Un seul exécutable universel masque les dépendances natives. Native AOT/trimming sans qualification des extensions serait risqué. Docker uniquement contredirait les objectifs d’hébergement.

## Conséquences

Versions des dépendances, checksums, restrictions, procédure de build et licences tierces accompagnent les releases. Pas de publication NuGet ou GitHub Release durant le cadrage. Licence du projet et noms publics à choisir.

## Validation attendue

Smoke tests des archives sur leur plateforme, chargement d’un module externe, providers revendiqués testés. Voir [compatibilité](../compatibilite.md).

## Complément confirmé le 20 octobre 2026 — publication NuGet.org en L10

Le porteur demande explicitement la publication des packages publics sur NuGet.org dans L10 : bibliothèques et adaptateurs destinés à la consommation publique, CLI .NET et templates selon le catalogue de release arrêté. La production locale des packages appartient à la préparation ; L10 exige leur disponibilité publique et leur consommation vérifiée depuis un environnement propre. Voir le [plan L10](../implementation-plan.md#lot-l10--qualification-et-publication-publique).

Cette confirmation accepte le canal NuGet.org et son inclusion dans le lot ; les autres modalités de packaging encore proposées ne sont pas acceptées implicitement. Licence, IDs, propriétaires et versions doivent être arrêtés avant publication. La restriction de publication pendant le cadrage reste applicable ; aucune publication n’est effectuée par cette mise à jour documentaire.

## Démarrage L10 — 27 octobre 2026

Sur demande du porteur de choisir une licence assez permissive pour ses forks
internes en banque et ses autres projets, Apache-2.0 est retenue et son texte
officiel ajouté à la racine. Cette décision ne modifie pas les licences tierces.
Le catalogue et les trois RID de préparation restent candidats ; aucune
réservation de nom, qualification d’archive ou publication n’est présumée.
Voir le [runbook L10](../l10-release.md) et la [preuve](../evidence/l10.md).

## Décisions de release confirmées le 27 octobre 2026

Propriétaire individuel NuGet : `mukadi.sheba`. Les recommandations sont acceptées :
IDs `EnterpriseWorkflow.*`, CLI `ew`, préversion `1.0.0-rc.1`, publication sur tags
`v…` par Trusted Publishing et archives autonomes/dépendantes du runtime pour
Windows x64, Linux x64 et macOS ARM64. La politique NuGet est confirmée configurée
par le porteur. Q06 est clos sans seuils de charge/performance, SLA ou RPO/RTO
ni tests correspondants ; aucune garantie chiffrée n’est annoncée. Les exigences
fonctionnelles de sécurité, cohérence, migrations et restauration restent requises.
