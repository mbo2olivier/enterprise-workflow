# Capture SMTP de qualification L9

Stack facultative de test ; `ew dev` ordinaire fonctionne sans Docker.

```bash
docker compose -f tests/smtp/docker-compose.yml up -d --wait
# Messages : http://localhost:8025 ; SMTP : localhost:1025
# Panne/reprise pendant un parcours :
docker compose -f tests/smtp/docker-compose.yml stop smtp
docker compose -f tests/smtp/docker-compose.yml start smtp
# Arrêt et suppression de la capture jetable :
docker compose -f tests/smtp/docker-compose.yml down --volumes
```

Dans `ew.dev.json`, ajouter `smtp` :

```json
{
  "host": "localhost",
  "port": 1025,
  "enableSsl": false,
  "from": "workflow@example.test",
  "baseUrl": "http://localhost:5080",
  "pageSize": 1,
  "addresses": {
    "local|ew-dev-bob": "bob@example.test",
    "local|ew-dev-carol": "carol@example.test"
  }
}
```

Cette configuration s’ajoute au seed du module ; les noms d’identités doivent correspondre.
Vérifier les destinataires éligibles, le lien et le contenu minimal, la pagination, les retries après panne/reprise et l’inbox utilisable même SMTP arrêté. Consigner les résultats selon `docs/l8b-validation.md`.

Le transport SMTP est réel vers Mailpit ; cette capture ne qualifie ni délivrabilité Internet, ni TLS/authentification de production. L’image fixée a démarré localement avec contrôle de disponibilité réussi ; la capture et la panne/reprise sont acquises dans la [preuve L9](../../docs/evidence/l9.md), avec la portée qui y est décrite. Aucun SMTP n’est activé par défaut.

Référence : [documentation officielle des images Mailpit](https://mailpit.axllent.org/docs/install/docker/).
