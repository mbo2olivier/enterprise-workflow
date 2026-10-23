# Exemple embedded

Cette application charge le même artefact et la même définition que le Kernel et `ew dev`, compose SQLite, le runtime et les services applicatifs avec une autorisation réelle, puis démarre une instance. Le JSON initial et les permissions doivent correspondre au module. Exemple avec le template module packagé :

```bash
dotnet run -- ../Acme.Leave/artifacts/acme.leave/1.0.0 acme.leave.request 1 '{"reason":"Demande de démonstration"}' acme.leave.request acme.leave.read
```

La base `embedded-workflow.db` reste dans le répertoire courant. L’identité de démonstration reçoit seulement les permissions explicitement demandées. Les tâches humaines conservent les contrôles d’affectation et de séparation ; l’application intégratrice doit ajouter son identité authentifiée et ses interactions de tâches. Aucun transport externe n’est simulé comme réussi.
