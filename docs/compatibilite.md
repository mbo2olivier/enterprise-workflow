# Compatibilité et distribution

Date de vérification documentaire : 1er octobre 2026. La combinaison de développement macOS ARM64, client .NET 10/ODP.NET et serveur Oracle Free Linux ARM64 en conteneur est validée pour les scénarios de persistance L3b-I. Oracle Enterprise 19.19 et les distributions complètes du produit ne sont pas encore qualifiés. Les décisions produit et les capacités annoncées par les éditeurs restent distinctes des résultats de tests.

## Base vérifiée

.NET 10 est une version LTS. Les distributions Microsoft comprennent notamment Windows x86/x64, Linux et macOS x64/ARM64 ; le support dépend des versions d’OS. [Politique .NET](https://dotnet.microsoft.com/en-us/platform/support/policy), [téléchargements .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [OS supportés](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

Oracle documente ODP.NET Core en 64 bits, Oracle Database 19c minimum et macOS ARM64. Le support .NET 10 commence à ODP.NET Core 23.26.0 ; EF Core 10 nécessite un provider Oracle compatible. Cela ne qualifie ni Win x86 ni macOS Intel pour cet adaptateur. [Prérequis ODP.NET](https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/InstallSystemRequirements.html), [prérequis EF Oracle](https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/InstallEFCoreRequirements.html).

Le porteur accepte ces restrictions : support distinct par adaptateur. Oracle 19c est le minimum produit confirmé, pas une revendication de compatibilité déjà testée avec toute version supérieure.

## Matrice cible

| RID candidat | Intention de distribution | SQLite | Oracle | Condition avant annonce de support |
| --- | --- | --- | --- | --- |
| win-x86 | Demandée, faisabilité à vérifier | À qualifier | Exclu du périmètre accepté | OS Windows pris en charge, dépendances natives et tests en processus x86 |
| win-x64 | Cible principale Windows | À qualifier | À qualifier | OS commun aux dépendances ; serveur Oracle 19c au minimum testé |
| linux-x64 | Première cible Linux proposée | À qualifier | À qualifier | Distribution/version explicitement choisies dans l’intersection des supports |
| linux-arm64 | Extension de matrice à évaluer | Non promis | Non promis | Runners, dépendances et support du provider vérifiés |
| linux-musl-x64 | Variante Alpine différée | Non promis | Non promis | libc et dépendances natives qualifiées séparément |
| osx-arm64 | Première cible macOS proposée | À qualifier | À qualifier | Version macOS compatible avec .NET et provider, accès à Oracle réel |
| osx-x64 | Cible macOS Intel proposée | À qualifier | Non revendiqué | SQLite et modules testés sur machine Intel |

La matrice du connecteur AD sera ajoutée après prototype de sa bibliothèque LDAP. Un RID ne décrit pas à lui seul le support d’Oracle, d’AD ou d’un module tiers. Les distributions Linux non listées dans une release restent non qualifiées, même si une compilation réussit.

## Artefacts proposés

- Packages `EnterpriseWorkflow.*` : bibliothèques ciblant net10.0 ; identifiants publics à réserver.
- Modèles de projet et outil CLI local épinglé : noms à choisir.
- Archives du Host par RID, avec configurations d’exemple sans secret.
- Modules métier séparés : manifeste, assemblies, dépendances et assets.

Proposer un profil SQLite autonome pour la découverte, et un profil Oracle sur plateformes compatibles. Le mécanisme exact de sélection des providers reste à fixer ; aucune dépendance Oracle chargée obligatoirement dans le profil Win x86.

Une publication autonome inclut le runtime et nécessite sa mise à jour lors des correctifs. Une publication dépendante du runtime nécessite le runtime ASP.NET Core compatible sur l’hôte. L’option de publication sera toujours explicite. Ni conteneur, ni service système, ni reverse proxy imposé au Core.

## Critères de release

Pour chaque artefact : version, commit source, SDK, dépendances verrouillées, RID/OS testés, providers/connecteurs qualifiés, empreinte et instructions d’installation. Exécuter le binaire produit, charger un module externe, réaliser une attente/reprise et valider le provider annoncé. Cross-compilation seule insuffisante.

Commencer sans trimming ni Native AOT afin d’éprouver le chargement dynamique ; réévaluer uniquement sur preuves. Signature/notarisation et hébergement des releases restent à décider. Aucune licence ni réservation NuGet n’a été effectuée.

## Environnement observé

Les lots L1 à L3b ont été compilés avec le SDK .NET `10.0.401` sur macOS 26.3 ARM64. SQLite a été testé sur fichiers locaux et Oracle via Oracle Entity Framework Core `10.23.26301` contre Oracle Free `23.26.3` en conteneur. Cette preuve valide l’adaptateur en développement ; elle ne qualifie ni Oracle 19.19, ni un artefact de distribution, ni les autres lignes de la matrice, ni AD.
