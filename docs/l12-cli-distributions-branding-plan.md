# L12 — SDK de CLI extensible, distributions organisationnelles et identité visuelle

Date : 18 novembre 2026. Statut : briefing et arbitrages approuvés par le porteur, intégrés au plan ; implémentation non démarrée. L12 suit L11 et repousse `ew studio` à L13. La série L10 reste close. Cette autorisation concerne la planification ; les spécifications et ADR précèdent l’implémentation. Les noms d’API et de packages ci-dessous sont indicatifs, aucune capacité future n’est annoncée comme livrée.

## Objectif et répartition public/privé

Permettre à une organisation de construire, nommer, empaqueter et distribuer sa propre CLI .NET, ses templates et son Kernel, en consommant le socle public par dépendances versionnées. Exemple : commande privée `few` pour FirstBank Enterprise Workflow, publiée sur un feed Azure Artifacts. Le développeur crée un module préconfiguré, injecte un service interne, le teste en développement puis exécute le même artefact dans un Kernel compatible.

Le dépôt organisationnel demeure indépendant et conserve son historique initial ; aucun fork complet ni cherry-pick récurrent n’est requis par ce modèle. Le framework livre les mécanismes génériques et une distribution fictive de référence. Les contrats, services, outils, accès, secrets et qualifications bancaires réels appartiennent au projet privé ; aucun package public ne dépend de composants privés.

## Décisions approuvées

- Builder CLI avec configuration, construction et exécution séparées : `CreateBuilder(args)` → `Build()` → `RunAsync()`. Les signatures exactes restent à spécifier.
- Extensions déclarées dans du code compilé ; aucune découverte de plugins CLI arbitraires par configuration dans cette version. Collisions de noms de commandes et d’options refusées explicitement.
- CLI standard `ew` construite sur le même socle réutilisable que les CLI personnalisées, sans copie de son point d’entrée ni recours à des commandes internes non publiques.
- Mêmes implémentations versionnées des services métier dans l’hôte de développement et le Kernel, via une bibliothèque de composition partagée ; configuration d’environnement différente.
- `ew dev` et ses variantes organisationnelles gardent exclusivement les identités de développement. Aucun mode d’authentification réelle, maker/checker ou seconde authentification dans la commande dev ; ces mécanismes demeurent dans le Kernel.
- Templates préconfigurés ; installation orchestrable par la CLI ; nom de commande et feed choisis par l’organisation.
- Une identité visuelle par installation ; l’hôte définit les paramètres administrables.
- Nouveau L12 après L11, Studio en L13, sans réouverture des lots clos.

## Builder et expérience d’extension

Le builder réutilise les commandes génériques de compilation, validation, inspection, développement, packaging et vérification ; il permet la génération depuis des templates organisationnels, les commandes/sous-commandes supplémentaires, les options, valeurs par défaut et validations explicites. Les mécanismes déjà livrés ne deviennent pas implicitement de nouvelles commandes : leur surface publique est à spécifier.

Exemple illustratif, non contractuel :

```csharp
var builder = EnterpriseWorkflowCli.CreateBuilder(args);
builder.UseCommandName("few");
builder.AddStandardCommands();
builder.AddTemplates<FirstBankTemplates>();
builder.ConfigureDevelopmentServices((context, services) =>
{
    services.AddFirstBankServices(context.Configuration);
});
return await builder.Build().RunAsync();
```

Les commandes personnalisées disposent d’un contexte documenté : configuration, DI, console, journalisation et annulation. Elles bénéficient des conventions communes d’aide, localisation L11, diagnostics et codes de sortie. Le contrat distingue ajout, configuration et remplacement explicitement autorisé ; aucun masquage silencieux d’une commande standard. Définir l’ordre des registrations, les services réservés et les validations de composition.

## Parité des services métier et frontière de sécurité

Les modules référencent les contrats et le SDK organisationnels ; les hôtes fournissent les implémentations par DI. Un projet/package partagé contient les intégrations internes ; une composition commune enregistre ces services pour le Kernel et l’hôte dev. Leurs règles d’appel, modèles, sérialisation et traitement d’erreurs sont communs.

En développement, le développeur fournit endpoints, identifiants techniques et paramètres test/dev. En déploiement, ces valeurs proviennent de l’instance. Réutiliser les règles de configuration et de secrets définies en L11, dont `.env` pour le développement, sans chargement automatique nouveau en production ni exposition dans logs, arguments, fichiers intermédiaires ou artefacts. Les doubles sont utiles aux tests automatisés, pas l’implémentation par défaut du développement organisationnel.

Cette parité porte sur les services métier, pas sur les providers de connexion du Kernel. Le partage d’intégrations ne doit pas enregistrer implicitement l’authentification bancaire dans le DevHost. La configuration dev conserve les identités prévues par L11 (`ew.dev.json`) ; l’extension refuse les remplacements des mécanismes d’identité réservés. Aucun lancement du Kernel personnalisé ni mode d’authentification réelle n’est ajouté à dev. La qualification de sécurité est effectuée dans le Kernel, hors de ce parcours.

## Packages et responsabilités

Découpage indicatif à confirmer en ADR, sans création anticipée de projets vides :

| Composant | Responsabilité |
| --- | --- |
| EnterpriseWorkflow.Cli.Core | Builder, commandes et conventions CLI réutilisables |
| EnterpriseWorkflow.Development.Hosting | Composition et cycle de vie du développement |
| Organisation.Workflow.Abstractions | Contrats et modèles consommés par les modules |
| Organisation.Workflow.Sdk | Extensions et dépendances usuelles de développement métier |
| Organisation.Workflow.Integrations | Implémentations partagées des services internes |
| Organisation.Workflow.Hosting | Composition et validation communes de ces services |
| Organisation.Workflow.Kernel | Hôte déployé construit sur L11, avec sa sécurité |
| Organisation.Workflow.Cli | Outil nommé par l’organisation et composition dev |
| Organisation.Workflow.Templates | Projets et configuration initiale |

Ne pas imposer les dépendances du Kernel, ses providers de connexion ou ses secrets aux modules métier.

## Contrats partagés, artefacts et compatibilité

Permettre à l’hôte de déclarer explicitement les assemblies de contrats organisationnels partagées avec les modules ; même identité CLR, règles de résolution/version et diagnostics définis. Distinguer les dépendances fournies par l’hôte de celles privées du module et préserver leur isolation/coexistence. Aucun fallback silencieux sur une version de contrat incompatible.

Définir les exigences des modules envers les contrats/services de la distribution et signaler les services absents ou incompatibles avant l’exécution du handler. Le mécanisme exact de déclaration, la validation des dépendances DI et les formats restent à spécifier.

Une distribution publie un ensemble cohérent de versions CLI/SDK/contrats/intégrations/Kernel. Sa version est distincte des releases métier, versions de définition et schémas d’état. Le même artefact métier fonctionne dans les deux hôtes compatibles sans recompilation par environnement, sans endpoints/secrets embarqués et sans altération des hashes historiques. Toute évolution de manifeste ou de format exige sa politique explicite de compatibilité.

## Templates et distribution

Après installation initiale, la génération fournit références SDK/contrats, handler consommant un service partagé, configuration dev, `.env.example` sans secret, ressources de localisation L11, exigences de compatibilité et commandes documentées. Le développeur configure ses accès test ; il n’installe pas les intégrations une à une pour chaque module.

Définir le packaging d’un outil .NET personnalisable, son nom de commande, les métadonnées, l’installation/mise à jour via un feed privé et l’installation des templates orchestrable. Azure Artifacts est un exemple de cible, pas une dépendance imposée au framework. L’authentification au feed utilise les mécanismes du registre sans embarquer ses secrets dans l’outil ou les templates.

## Identité visuelle et localisation

Compléter les réglages actuels : nom de l’installation, logos, favicon, libellés accessibles, palette principale/accent et variantes, surfaces, textes, bordures et styles additionnels d’hôte. Appliquer le thème au portail, à la connexion et à l’administration ; traiter les couleurs encore fixes. Ne pas confondre capacité déjà stockée et rendu réellement fonctionnel.

Une marque par installation. Définir la priorité entre défauts framework, valeurs de l’hôte et réglages persistés ; l’hôte fixe la marque et autorise explicitement les paramètres administrables. Respecter les langues et replis L11, sans traduire les identifiants. Les thèmes par utilisateur/entité et une refonte générale des écrans sont hors périmètre.

## Séquence et critères de sortie

| Sous-lot | Livrables | Validation attendue |
| --- | --- | --- |
| L12-0 — spécifications et ADR | Builder, commandes, composition dev, frontières L11, contrats, compatibilité, configuration et thème | Décisions produit reprises ; signatures, formats, responsabilités et migrations nécessaires spécifiés avant code |
| L12-1 — socle CLI | Builder et API d’extension ; adaptation de ew | CLI standard compatible ; CLI personnalisée compilable ; aide, annulation, localisation et diagnostics communs ; collisions refusées |
| L12-2 — services métier dev | Composition compilée et configuration | Même implémentation versionnée en dev/Kernel ; endpoints différents ; identité dev conservée ; remplacement des services réservés refusé ; aucun secret exposé |
| L12-3 — contrats et artefacts | Assemblies partagées et exigences des modules | Identité CLR cohérente, versions compatibles, isolation des dépendances privées et diagnostics avant handler |
| L12-4 — templates/distribution | Génération préconfigurée et outil .NET privé | Installation depuis feed de test, nom personnalisé, création/dev/validation/pack/vérification sans ajout manuel des dépendances usuelles |
| L12-5 — identité visuelle | Marque, palette, styles et réglages contrôlés | Portail/connexion/administration cohérents, deux langues et contrôles visuels responsive/accessibilité pertinents |
| L12-6 — qualification | Distribution fictive, documentation et preuves | Même artefact dans dev et Kernel compatible ; non-régression CLI ; matrice plateforme/navigateur concernée ; SQLite/Oracle si persistance modifiée |

## Limites et suites documentaires

Aucune implémentation bancaire réelle, plateforme de gestion de distributions, découverte arbitraire de plugins, nouvelle sémantique moteur ou qualification bancaire inférée de l’exemple. La sécurité et la localisation restent définies par L11 ; elles ne sont pas réimplémentées ici.

Après spécification : mettre à jour PRD/traçabilité, architecture, guides SDK/CLI, compatibilité des packages et artefacts, configuration, templates, distribution et stratégie de tests. Produire les ADR et preuves dédiés, puis mettre à jour le wiki à la livraison. Préserver les preuves des lots clos et distinguer planification, implémentation et qualification.
