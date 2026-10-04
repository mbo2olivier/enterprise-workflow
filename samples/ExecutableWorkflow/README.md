# ExecutableWorkflow

Le sample exerce désormais le schéma canonique v1 complet du lot L6 : service, création d'une `HumanTask` désignée, formulaire/handler versionnés, complétion autorisée et idempotente, puis livraison outbox. Il est compilé et exécuté par la CI sur Linux, macOS et Windows afin que toute évolution pré-release du schéma reste synchronisée avec les samples.

Cet exemple L4b crée une base SQLite temporaire, applique les migrations, valide le binding d’un handler concret, puis exécute durablement `Start → Service → End`. Le handler remplace l’état sans changer sa version de schéma et retourne une intention d’effet externe, persistée atomiquement puis livrée par l’outbox avec une clé d’idempotence stable.

```bash
dotnet run --project samples/ExecutableWorkflow/ExecutableWorkflow.csproj
```

La base temporaire est supprimée à la fin. Pour observer une reprise réelle, remplacez le chemin temporaire par un chemin fixe et interrompez le processus entre deux appels de pump.
