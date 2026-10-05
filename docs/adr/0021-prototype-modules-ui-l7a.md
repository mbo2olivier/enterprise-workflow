# ADR 0021 — Prototype de modules versionnés et ressources UI L7a

Date : 4 octobre 2026. **Statut : acceptée pour D1-A à D4-A ; décision G3 encore à confirmer.**

## Contexte

L7a doit éprouver le chargement de code métier approuvé, la coexistence de versions, les dépendances privées, l'alimentation du registre L4a et les composants Razor avant de figer le loader L7b. Un `AssemblyLoadContext` n'est pas une frontière de sécurité : un module dispose des privilèges du processus.

## Décisions acceptées

- **D1-A** : Blazor Web App avec rendu SSR statique par défaut et Interactive Server seulement pour les composants qui en ont besoin. Le prototype qualifie le rendu statique dynamique ; le Host L8 portera les render modes.
- **D2-A** : composants compilés dans la DLL et ressources déclarées/hachées dans `module.json`, servies sous une URL comprenant module, version et empreinte d'artefact. Aucun répertoire statique n'est exposé.
- **D3-A** : un contexte non collectible par `(ModuleId, ModuleVersion, ArtifactHash)`, conservé jusqu'au redémarrage ; contrats partagés depuis le contexte principal, dépendances privées déclarées et isolées ; aucun hot reload au MVP.
- **D4-A** : contrats minimaux et loader réutilisable mais explicitement expérimental jusqu'à L7b, avec fixtures A/B et tests CI.

Le manifeste v1 est strict : propriétés inconnues, chemins non normalisés, reparse points, fichiers absents, types MIME non autorisés, limites dépassées, versions de contrat incompatibles et hashes invalides bloquent la readiness avant le chargement. Les scripts globaux automatiques sont exclus ; un module JavaScript déclaré reste une ressource explicite d'un composant approuvé.

## Résultat du prototype

Deux versions de `EnterpriseWorkflow.Fixtures.ApprovalModule` et deux versions incompatibles de `EnterpriseWorkflow.Fixtures.PrivateDependency` sont chargées simultanément. Chaque composant Razor restitue la valeur de sa propre dépendance. Les handlers versions 1 et 2 alimentent le registre L4a sans fallback ; une collision exacte est refusée par `EW4001_DUPLICATE_HANDLER`. Une ressource CSS altérée est refusée avant activation par `EW7006_MODULE_FILE_HASH_MISMATCH`.

## Recommandation G3 à confirmer

**G3-A — conserver la coexistence côte à côte** : le prototype ne révèle pas de contrainte imposant le drainage. L7b devra conserver l'artefact exact tant qu'une définition ou une restauration le référence, activer les changements uniquement au redémarrage et interdire le retrait d'une version encore requise. Le drainage reste une opération explicite possible, pas le modèle nominal.

Alternative **G3-B** : imposer le drainage avant tout remplacement incompatible. Elle simplifierait la rétention et le diagnostic, mais supprimerait le bénéfice démontré et empêcherait les instances longues de poursuivre leur code exact pendant le déploiement d'une nouvelle version.

## Limites

Le prototype ne promet ni déchargement, ni isolation hostile, ni signature cryptographique, ni dépendance native/RID. Signature et packaging appartiennent à L9/L10. G3a reste ouverte avant toute évolution du schéma d'état.
