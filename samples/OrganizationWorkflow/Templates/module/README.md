# WorkflowSample

Projet de module : `few-example dev`, puis ouvrir l’URL et choisir une identité.
Alice crée une demande ; Bob la prend et décide. L’auto-approbation reste interdite.
Le compte « Administration locale » configure les droits sans accès métier implicite.

`few-example validate .`, `few-example inspect .`, `few-example pack . --configuration Release`, puis `few-example verify <répertoire-artefact>`.
Modifier explicitement la release et, si nécessaire, la révision des contrats avant de réutiliser une base après modification du module. Aucune version existante n’est écrasée.

Local secrets can be placed in `.env` (see `.env.example`). `few-example dev` loads only that file automatically; use `--env-file` for another path or `--no-env-file` to disable loading. Do not commit credentials. The development runtime keeps identities from `ew.dev.json`; external providers and second authentication require a custom deployed host.

Ce module cible la distribution Example.Workflow 1.x. Configurer Example__PolicyUrl dans .env pour appeler l’API de démonstration. Les services sont fournis par la CLI en dev et par le Kernel en déploiement, sans inscription supplémentaire dans le module.
