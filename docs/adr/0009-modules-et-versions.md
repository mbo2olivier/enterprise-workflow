# ADR 0009 — Artefacts immuables et protection des anciennes instances

Date : 30 septembre 2026.

**Statut : Acceptée — G3-A, coexistence nominale.**

## Contexte

Les workflows peuvent attendre longtemps ; remplacer une DLL peut changer le comportement d’une instance active.

## Décision

Lier instance, définition et artefact exacts. Conserver côte à côte les versions requises par les définitions, les instances et la restauration. Activer les changements uniquement au redémarrage et bloquer le retrait d'une version encore référencée. Le drainage reste une opération explicite possible, pas le modèle nominal. Tout module configuré incompatible bloque la readiness.

## Options considérées

Remplacer la DLL en place sans contrôle expose une instance à un code différent. Hot reload et migration automatique augmentent le périmètre. Garder seulement le graphe ne conserve pas les exécuteurs.

## Conséquences

Rétention des artefacts, résolution de DI par version et assets UI à tester. Modules approuvés uniquement ; un contexte de chargement n’est pas une sandbox. Sauvegarder base et artefacts ensemble.

## Validation attendue

Versions A/B avec dépendance privée, ancienne instance en attente, retrait interdit, hash modifié, restauration et ressources Razor. Le prototype L7a a validé la coexistence ; L7b doit maintenant qualifier la rétention et le retrait bloqué.
