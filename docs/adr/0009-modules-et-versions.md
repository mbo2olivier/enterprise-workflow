# ADR 0009 — Artefacts immuables et protection des anciennes instances

Date : 30 septembre 2026.

**Statut : Proposée — choix coexistence/drainage encore ouvert.**

## Contexte

Les workflows peuvent attendre longtemps ; remplacer une DLL peut changer le comportement d’une instance active.

## Décision ou proposition

Lier instance, définition et artefact exacts. Tester tôt la coexistence par module/version ; si elle échoue, bloquer les remplacements incompatibles et préparer un drainage explicite. Ne pas considérer ce repli comme une coexistence réalisée. Tout module configuré incompatible bloque la readiness.

## Options considérées

Remplacer la DLL en place sans contrôle expose une instance à un code différent. Hot reload et migration automatique augmentent le périmètre. Garder seulement le graphe ne conserve pas les exécuteurs.

## Conséquences

Rétention des artefacts, résolution de DI par version et assets UI à tester. Modules approuvés uniquement ; un contexte de chargement n’est pas une sandbox. Sauvegarder base et artefacts ensemble.

## Validation attendue

Versions A/B avec dépendance privée, ancienne instance en attente, retrait interdit, hash modifié, restauration et ressources Razor. Le résultat du prototype doit clore Q07 avant loader définitif.
