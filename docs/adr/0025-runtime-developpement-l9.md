# ADR 0025 — Runtime de développement local `ew dev` (L9)

Date : 20 octobre 2026. **Statut : acceptée pour le périmètre et les orientations ci-dessous à la demande du porteur ; implémentation locale livrée, qualification finale restante.** Les détails restants figurent dans la [spécification L9](../l9-spec.md).

## Contexte

Un auteur doit tester le rendu et le comportement de son module avant packaging sans créer un Kernel ni écrire une application embedded. Le porteur demande `ew dev`, SQLite par défaut et une configuration du runtime local, puis l’intégration des propositions discutées dans les spécifications.

## Décisions

- Inclure `ew dev` dans L9 : hôte de développement fourni par l’outil, réutilisant moteur, application, sécurité, loader et UI existants.
- Compiler, valider et préparer un artefact temporaire conforme aux mêmes règles que `ew pack`, puis exposer une URL après readiness.
- SQLite persistant dans un espace local `.ew/dev/`, migrations explicitement orchestrées par l’hôte de développement ; aucune migration ajoutée implicitement au loader.
- Configuration facultative `ew.dev.json`, options CLI prioritaires, défauts utilisables sans fichier. JSON en premier incrément ; YAML et autres providers différés.
- Identités de démonstration, seed explicite des habilitations et sélecteur propre au développement, sans contourner l’autorisation métier. Écoute sur boucle locale ; mécanismes de test absents de la production et du package métier.
- Surveillance avec compilation et redémarrage contrôlé, désactivable. Respect des artefacts immuables, de leur rétention et des révisions ; aucun remplacement silencieux de hash sous une même release ni hot reload.
- Réutiliser les écrans L8 et les conventions L8b ; maintenir l’exemple embedded comme livrable distinct.

## Conséquences et alternatives

L9 doit qualifier la boucle navigateur, la persistance, les identités, les reconstructions et le passage du développement au package. Un aperçu de formulaire seul ne valide pas les affectations et parcours réels. Exiger un projet Kernel par module ajoute une composition inutile à la boucle auteur. Un moteur simulé ou un loader permissif créerait des écarts avec le déploiement.

Cette décision complète les ADR 0006, 0010, 0022 et 0023 sans accepter implicitement tous les choix de distribution de l’ADR 0010. Le redémarrage de développement respecte l’exclusion du hot reload. Aucun statut de qualification L8b ou de livraison L9 ne change.

## Validation attendue

Depuis copie propre : génération → `ew dev` → parcours LeaveRequest multi-acteurs → arrêt/reprise → modification et diagnostic de version → package → vérification → chargement Kernel. Tester aussi configuration invalide, droits refusés, coexistence des versions, isolement des routes de développement et absence de données locales dans le package. Voir les critères détaillés de la spécification L9 et T14/T16.

## Application L9

CLI et templates locaux, configuration JSON v1, réinitialisation par archivage, validation des artefacts combinés et stack SMTP de capture sont implémentés. Les choix et limites sont documentés dans le [guide CLI](../l9-cli.md) ; la [preuve locale](../evidence/l9.md) ne constitue pas une qualification CI multiplateforme.
