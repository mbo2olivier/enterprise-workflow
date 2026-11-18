# WorkflowSample

Projet de module : `ew dev`, puis ouvrir l’URL et choisir une identité.
Alice crée une demande ; Bob la prend et décide. L’auto-approbation reste interdite.
Le compte « Administration locale » configure les droits sans accès métier implicite.

`ew validate .`, `ew inspect .`, `ew pack . --configuration Release`, puis `ew verify <répertoire-artefact>`.
Modifier explicitement la release et, si nécessaire, la révision des contrats avant de réutiliser une base après modification du module. Aucune version existante n’est écrasée.

Local secrets can be placed in `.env` (see `.env.example`). `ew dev` loads only that file automatically; use `--env-file` for another path or `--no-env-file` to disable loading. Do not commit credentials. The development runtime keeps identities from `ew.dev.json`; external providers and second authentication require a custom deployed host.
