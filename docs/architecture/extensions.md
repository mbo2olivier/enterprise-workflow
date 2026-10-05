# SDK, modules, formulaires et Studio

Statut : prototype L7a implémenté selon D1-A à D4-A ; loader définitif L7b et Studio différé. Les API de module restent expérimentales jusqu'à G3/L7b.

## Définition en C#

Le SDK construit un modèle canonique validé. Séparer auteur/builder et modèle de définition. Les évaluateurs de décisions sont enregistrés par identifiant et version ; ils retournent une issue déclarée. Pas de sérialisation de lambdas ni d’expression C# fournie par un utilisateur en production.

Un nœud reçoit les entrées validées, un contexte borné, l’identité de l’opération et un CancellationToken ; il utilise ses services par DI et retourne réussite/issue, attente durable ou échec qualifié. Description, éditeur de configuration et exécuteur sont distincts. Un nœud sans éditeur graphique reste utilisable en code.

## Artefact de module

```text
plugins/
  LeaveRequest/
    1.0.0/
      module.json
      LeaveRequest.Module.dll
      LeaveRequest.Module.deps.json
      dependencies/
      resources/
```

L’organisation exacte des dépendances sera alignée sur le mécanisme de publication .NET testé. Le manifeste proposé porte ID/version de module, assembly d’entrée, plage de compatibilité des contrats, workflows et formulaires exposés, empreintes, capacités requises et restrictions de plateforme. Secrets et configuration propre à une organisation sont externes à l’artefact.

## Chargement et versionnement

L7a utilise un contexte de chargement non collectible par module/version/empreinte, une résolution limitée aux dépendances privées déclarées et le partage des assemblies de contrats avec le Host. `AssemblyLoadContext` ne constitue pas une isolation de sécurité. [Guide Microsoft](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support).

Les modules sont du code approuvé exécuté avec les privilèges du processus. Aucun hot reload ni compilation distante au MVP. Les chemins du manifeste restent sous le répertoire autorisé ; aucune recherche opportuniste dans des emplacements non configurés.

Une instance référence définition/version, module/version et empreinte. Conserver les artefacts tant qu’ils servent des instances ou à la restauration requise. Si le chargement côte à côte des versions fonctionne, chaque instance résout son code exact. Sinon, bloquer les remplacements incompatibles et demander le drainage selon un plan explicite. Ne jamais continuer silencieusement avec le nouveau code.

Le prototype éprouve DI, dépendances privées, contrats partagés et ressources UI. Deux versions A/B coexistent ; deux handlers sous la même clé exacte n'écrasent jamais leurs registrations. Un module configuré invalide rend le démarrage non prêt avec diagnostic précis. La recommandation G3-A retient donc la coexistence comme modèle nominal, sous réserve de confirmation avant L7b.

## Réutilisation entre organisations

Copier l’artefact complet, pas seulement la DLL si elle a des dépendances. Vérifier TFM, RID/dépendances natives, contrats, configuration, services métier et mappings d’identité de l’organisation destinataire. Ne copier ni base, ni mots de passe, ni sessions, ni clés d’idempotence existantes. Un formulaire ne doit pas contenir un SID corporate codé en dur.

## UI et formulaires

MVP : texte court/long, date, nombre, booléen et choix ; labels, requis, messages et présentation simple. Validation serveur obligatoire. Formulaire et schéma de données versionnés avec l’instance. Une extension UI permet un composant personnalisé sans référence Razor dans le Core.

UI métier et administration en Blazor Web App, SSR statique par défaut et Interactive Server par composant lorsque nécessaire, conformément à l'[ADR 0021](../adr/0021-prototype-modules-ui-l7a.md). Les ressources d'un module sont déclarées et hachées, puis servies par une URL immuable versionnée ; le Host ne dépend pas du manifeste de Static Web Assets d'une RCL chargée dynamiquement. Pages L8 : accueil des processus autorisés, démarrage, inbox, détail de tâche, détail d'instance/historique et administration. Thème via tokens CSS, logo et titre configurables ; les thèmes ne modifient pas les permissions. Textes externalisés, UTC en stockage, fuseau d'affichage explicite.

## CLI

Commandes conceptuelles : créer module/nœud/workflow, valider, packager et ouvrir le Studio après sa livraison. Nom de commande et package à vérifier avant publication. Préférer un outil local épinglé ; `dotnet build` suffit pour compiler.

La validation réutilise le Core et renvoie code, sévérité, élément et correction suggérée ; code de sortie non nul en cas d’erreur. La compilation/validation d’un projet peut exécuter du code : uniquement projets de confiance. Le packaging vérifie dépendances, manifestes, compatibilité et absence de configuration secrète.

## Studio après MVP

Palette, canvas, propriétés, validation, sauvegarde d’un brouillon JSON et export C# stable. Le brouillon est la source visuelle ; après export et modification manuelle, le C# est maintenu séparément. Pas de round-trip automatique du C# arbitraire.

L’export doit compiler et produire un graphe normalisé équivalent, avec échappement correct et ordre déterministe. Pas d’écrasement d’un fichier existant sans option explicite. Studio lancé seulement en développement et absent du Host de production. Bibliothèque de graphe et besoin d’outillage JavaScript à la construction restent à arbitrer.
