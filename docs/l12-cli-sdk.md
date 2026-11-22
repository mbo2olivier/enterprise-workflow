# SDK de CLI et distributions organisationnelles L12

API du dépôt en cours de qualification ; aucune nouvelle publication NuGet n’est impliquée. Voir [spécification](l12-spec.md), [ADR 0030](adr/0030-distributions-cli-runtime-et-marque.md) et [recette](validation/l12-recette-porteur.md).

## Créer sa CLI

Référencer `EnterpriseWorkflow.Cli.Core` dans un exécutable .NET Web empaqueté comme outil. Le socle inclut l’hébergement dev ; un second package vide de développement n’est pas créé. Le projet doit conserver ses fichiers runtime et assets ASP.NET. L’exemple complet est `samples/OrganizationWorkflow/Cli`.

```csharp
using EnterpriseWorkflow.Cli;
var builder = EnterpriseWorkflowCli.CreateBuilder(args)
    .UseCommandName("few")
    .ConfigureModules(OrganizationServices.ConfigureModules)
    .ConfigureDevelopmentServices((context, services) =>
        services.AddOrganizationServices(context.Configuration))
    .AddTemplates("Organization.Workflow.Templates", "2.3.0", "few-module")
    .ConfigureCommandDefaults("dev", "--environment", "bank-dev");
return await builder.Build().RunAsync();
```

Les commandes standard sont incluses par défaut ; `AddStandardCommands()` est idempotent. `AddCommand("bank check", description, handler, options)` ajoute aussi des sous-commandes. Les options déclarées utilisent leur préfixe `--` et précisent si une valeur suit. Les arguments positionnels et validations métier appartiennent au handler. Le contexte fournit configuration figée à `Build`, provider DI, logger, sorties, arguments et token d’annulation. `ConfigureCommandDefaults` ajoute les options absentes ; une valeur explicite prévaut. `ReplaceCommand` exige une commande existante ; les commandes internes `__*` et les sous-commandes sous une racine standard sont réservées.

`Services` configure les services des commandes personnalisées. `ConfigureDevelopmentServices` configure ceux du DevHost, avec sa configuration effective et ses valeurs `.env` ; ne pas capturer un secret dans les arguments de lancement. Les services existants et les contrats de sécurité/authentification sont protégés. Les configurateurs d’options .NET additifs hors options framework/ASP.NET, nécessaires notamment à AddHttpClient, sont autorisés sans permettre de retirer ou remplacer une inscription existante. L’authentification dev reste celle de L11. Les enfants relancent l’assembly d’entrée de la distribution, afin de reconstruire les mêmes inscriptions compilées.

## Partager ses services et annoncer le runtime

Le module consomme ses interfaces par constructeur. Une bibliothèque de composition fournit la même implémentation au Kernel et à la CLI. L’hôte configure ses contrats partagés et son runtime :

```csharp
options.SharedAssemblies.Add(typeof(ICustomerDirectory).Assembly);
options.RuntimeId = "Organization.Workflow";
options.RuntimeVersion = "2.3.0";
options.RequireRuntimeDeclaration = true;
```

Le module ou sa classe de base organisationnelle expose :

```csharp
public override WorkflowRuntimeRequirement RuntimeRequirement =>
    new("Organization.Workflow", "[2.3.0,3.0.0)");
```

La propriété est sérialisée dans `module.json.runtime` (`id`, `versionRange`). Le loader valide la cible et la plage avec `NuGet.Versioning` avant activation. Diagnostics : EW7054 plage invalide, EW7055 runtime incompatible/absence exigée, EW7056 divergence manifeste/code, EW7057 collision d’assemblies partagées. L’absence de déclaration reste acceptée par défaut pour les artefacts historiques ; ce défaut peut être durci par l’hôte. Aucun inventaire de services n’est ajouté au module. Les erreurs DI surviennent à la résolution normale.

## Installer les templates

`few setup` installe la version exacte annoncée par la distribution, puis `few new few-module ...` génère sans installation implicite ni recherche de mise à jour. Le moteur standard `dotnet new` est réutilisé avec un hive isolé par package/version. `Templates__Root` permet de placer ces hives dans un emplacement de qualification ; sinon le répertoire applicatif local de l’utilisateur est utilisé. Les sources et credentials suivent NuGet ; un feed privé n’est pas codé dans le framework. La restauration du projet généré est une opération distincte.

## Fixer la marque du Kernel

```csharp
builder.ConfigureBranding(brand =>
{
    brand.Defaults = brand.Defaults with
    { DisplayName = "Bank Workflow", AccentColor = "#6D28D9", LogoResourcePath = "/brand/logo.svg" };
    brand.FaviconPath = "/brand/favicon.svg";
    brand.StylesheetPath = "/brand/instance.css";
    brand.Canvas = "#F5F3FF";
    brand.EditableFields = WorkflowBrandingFields.TimeZone;
});
```

Les fichiers sont servis par l’hôte. Les couleurs RGB sont validées ; les assets utilisent des chemins URL locaux absolus. `AccentStrong`, `AccentSoft`, `Canvas`, `Surface`, `Ink`, `Muted`, `Border` et `LogoAccessibleLabel` complètent les valeurs. Les champs persistés verrouillés sont remplacés à la lecture ; une modification interdite échoue aussi au store, même sans formulaire UI. Les réglages sans révision utilisateur prennent les défauts de l’hôte. La révision et les contrôles de concurrence du store persistent. Aucune migration SQL nouvelle n’est nécessaire.

Le Kernel standard active explicitement tous ses champs historiques. Un hôte personnalisé est verrouillé par défaut. Les assets et tokens étendus sont fournis uniquement par le code de l’hôte. La marque est unique pour l’instance ; elle ne change pas les préférences linguistiques L11.
