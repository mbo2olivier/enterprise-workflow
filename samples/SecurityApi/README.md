# Security API — L5

Exemple minimal d’authentification locale, sessions serveur et administration protégée. SQLite est utilisé pour la démonstration ; le store Oracle expose le même contrat.

Initialiser une seule fois depuis un terminal local :

```bash
dotnet run --project samples/SecurityApi -- bootstrap admin "mot-de-passe-fourni-localement"
```

Le mot de passe n’est jamais fourni par configuration ni par un endpoint de bootstrap. En production, utiliser un mécanisme local évitant son inscription dans l’historique du shell. `recover <providerId> <subjectId>` ne fonctionne que lorsqu’aucun accès administratif connu ne subsiste.

Lancer ensuite l’API avec `dotnet run --project samples/SecurityApi`. `POST /session` délivre un jeton opaque ; les routes `/admin/*` exigent ce jeton et la permission correspondante. Les routes `PUT`, `DELETE` et `GET /admin/workflow-grants` administrent les grants exacts L5b. Une action doit d’abord être déclarée dans `Security:WorkflowActions` : l’exemple enregistre `leave-request`, version 1, nœud `manager-approval`, action `task.approve`. Un destinataire est soit une identité stable (`RecipientKind: identity`, `ProviderId`, `SubjectId`), soit un profil interne (`RecipientKind: profile`, `ProfileId`).

L’exemple n’implémente volontairement ni inbox ni claim : L5b calcule l’éligibilité, tandis que L6 ajoutera l’état durable de `HumanTask`. Cet exemple ne remplace pas les protections Host finales (limitation de débit, cookies/CSRF si une session cookie est choisie, coffre de secrets et TLS de terminaison).
