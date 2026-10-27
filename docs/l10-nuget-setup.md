# Première publication NuGet et releases GitHub — guide du porteur

Vérifié le 27 octobre 2026 avec les documentations officielles liées ci-dessous.
Ce guide prépare les comptes et décrit la cible de publication. **Le workflow
actuel `release-candidate.yml` construit des candidats sur demande manuelle ; il
ne publie ni sur NuGet.org ni dans GitHub Releases.** Le workflow `publish-release.yml` est désormais livré : les tags `v*` déclenchent
la qualification puis la publication via l’environnement `nuget-release`. Ne
pousser un tag de publication qu’après configuration de la politique NuGet.

## 1. Ce que j’attends concrètement de toi

Décisions confirmées le 27 octobre 2026 : propriétaire et pseudo NuGet
**`mukadi.sheba`**, compte individuel ; recommandations acceptées : préversion
`1.0.0-rc.1`, IDs `EnterpriseWorkflow.*`, tags `v…`, Trusted Publishing,
Windows/Linux x64 et macOS ARM64, deux modes d’archives. Q06 est clos : pas de
profil de charge à fournir, pas de test de seuil/performance requis, aucune
promesse de capacité, disponibilité ou RPO/RTO. Les tests fonctionnels de
sauvegarde/restauration restent prévus.

La politique Trusted Publishing de la section 4 est confirmée configurée par le
porteur le 27 octobre 2026. Le workflow utilise directement ton pseudo public ; le
secret `NUGET_USER` n’est plus nécessaire. Aucun mot de passe, code 2FA ou clé
API à transmettre dans le chat.

## 2. Créer le compte NuGet.org

1. Ouvrir [NuGet.org](https://www.nuget.org/) puis **Sign in**.
2. Choisir **Sign in with Microsoft**. Utiliser un compte Microsoft personnel
   durable, ou un compte professionnel dont tu maîtrises la pérennité. Si besoin,
   [créer un compte Microsoft](https://signup.live.com/).
3. Activer l’authentification à deux facteurs sur ce compte Microsoft ; conserver
   les moyens de récupération dans ton gestionnaire de mots de passe.
4. Accepter la connexion à NuGet.org et enregistrer le pseudo public souhaité.
   Le choisir soigneusement : il n’est pas renommable ensuite.
5. Vérifier l’adresse courriel et terminer toute confirmation demandée dans les
   paramètres du compte. Garder les notifications de publication actives.
6. Noter le pseudo de profil, distinct de l’adresse courriel.

Le compte NuGet individuel est nécessaire même si une organisation possédera
les packages. Source : [comptes individuels NuGet](https://learn.microsoft.com/en-us/nuget/nuget-org/individual-accounts).

Option organisation : menu du profil → **Manage Organizations** → **Add new
organization**. Choisir un nom disponible et une adresse courriel propre à cette
organisation, puis conserver ton compte individuel comme administrateur.
Cette organisation NuGet est distincte d’une organisation GitHub. Inutile de
transformer ton compte personnel en organisation.
Source : [organisations NuGet](https://learn.microsoft.com/en-us/nuget/nuget-org/organizations-on-nuget-org).

## 3. Préparer GitHub

Dans [le dépôt](https://github.com/mbo2olivier/enterprise-workflow), avec un compte
ayant les droits d’administration :

1. **Settings → Actions → General** : vérifier que GitHub Actions et les actions
   utilisées par le dépôt sont autorisés. Pour ce dépôt public, les runners GitHub standard Linux, Windows et macOS
   sont gratuits. Les runners de capacité supérieure (« larger runners ») restent
   payants ; le stockage des artefacts relève de quotas distincts.
2. **Settings → Environments → New environment** : créer `nuget-release`.
3. Dans cet environnement, restreindre les déploiements aux tags `v*`.
   Si le plan et la visibilité du dépôt le permettent, ajouter un approbateur
   requis. Pour un projet géré seul, ne pas activer une règle interdisant ta propre
   approbation si tu es l’unique approbateur.
4. Le pseudo `mukadi.sheba` est renseigné dans le workflow ; aucun secret
   `NUGET_USER` à créer.
5. Dans **Settings → Rules → Rulesets**, si disponible, préparer une règle sur
   les tags `v*` : limiter les personnes pouvant créer des tags de release et
   interdire leur modification/suppression hors procédure de maintenance.

La disponibilité des approbations dépend du plan et de la visibilité du dépôt.
Si elles ne sont pas disponibles, nous conserverons une publication manuelle
restreinte à un tag, plutôt que prétendre disposer d’une protection inactive.
Source : [environnements GitHub](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments).

### Héberger soi-même un runner (facultatif)

Le projet peut utiliser un runner sur ton Mac ou un serveur Linux. Dans le dépôt :
**Settings → Actions → Runners → New self-hosted runner**, choisir l’OS et
l’architecture, puis suivre les commandes de téléchargement, enregistrement et
lancement affichées par GitHub. Garder le jeton d’enregistrement confidentiel.
Installer le SDK épinglé et les dépendances des jobs ; Linux avec Docker est
nécessaire pour nos tests utilisant des conteneurs de services.

Un job cible par exemple `runs-on: [self-hosted, Linux, X64]`. La machine doit
rester disponible et pouvoir joindre GitHub. Un runner Linux peut compiler pour
Windows mais ne remplace pas une machine Windows pour y exécuter les tests.

Pour ce dépôt public, privilégier les runners GitHub standard gratuits. Un runner
personnel éventuel doit être isolé des données personnelles ou bancaires et ne
pas exécuter les PR non fiables ; ne pas exposer ton poste de travail ou un serveur
interne aux contributions externes. Les tags seuls ne constituent pas une isolation.

Sources : [runners standard et gratuité](https://docs.github.com/en/actions/how-tos/write-workflows/choose-where-workflows-run/choose-the-runner-for-a-job),
[runners auto-hébergés](https://docs.github.com/en/actions/reference/runners/self-hosted-runners),
[sécurité GitHub Actions](https://docs.github.com/en/actions/security-for-github-actions/security-guides/security-hardening-for-github-actions).

## 4. Autoriser GitHub sur NuGet sans clé permanente — recommandé

Le workflow `.github/workflows/publish-release.yml` est livré. Configurer la
politique avec les valeurs suivantes avant la première publication.

Dans NuGet.org : profil → **Trusted Publishing** → nouvelle politique GitHub.

| Champ | Valeur pour ce projet |
| --- | --- |
| Propriétaire NuGet de la politique | `mukadi.sheba` (compte individuel) |
| Repository Owner | `mbo2olivier` |
| Repository | `enterprise-workflow` |
| Workflow File | `publish-release.yml` — nom seul |
| Environment | `nuget-release` |
| Périmètre packages | `EnterpriseWorkflow.*`, avec création de nouveaux packages/versions autorisée |

Enregistrer la politique. Si NuGet signale une activation temporaire en attente,
vérifier sa validité au moment de la première publication et la réactiver si
nécessaire. Dans le workflow final, j’utiliserai `id-token: write` et
`NuGet/login@v1` avec `mukadi.sheba` pour obtenir une clé temporaire juste avant
l’envoi. L’environnement et les noms doivent correspondre exactement à la
configuration retenue.

Source : [Trusted Publishing NuGet](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

## 5. Solution de repli : clé API limitée

À utiliser seulement si Trusted Publishing ne peut pas être configuré.

NuGet.org → profil → **API Keys → Create**. Nommer la clé
`enterprise-workflow-release`, choisir le bon **Package Owner**, la permission de
publier de nouveaux packages et versions, le motif `EnterpriseWorkflow.*` et une
durée courte. Copier la clé une fois dans le secret d’environnement GitHub
`NUGET_API_KEY`, puis dans un gestionnaire de mots de passe si nécessaire.
Ne pas l’enregistrer dans un fichier du dépôt. Révoquer toute clé divulguée.

Avec notre SDK, la clé peut être fournie par la variable d’environnement
`NUGET_API_KEY` pour éviter sa saisie dans la commande :

```bash
# Exemple pour UN package validé ; la variable secrète est injectée par la CI.
dotnet nuget push artifacts/release/EnterpriseWorkflow.Abstractions.1.0.0.nupkg \
  --source https://api.nuget.org/v3/index.json
```

La publication finale traitera tout le catalogue, après contrôle du manifeste.
Ne pas publier un package de démonstration sous nos vrais IDs pour tester le
compte. Le site de test NuGet mentionné par Microsoft est une option de répétition
séparée ; il ne remplace pas la publication publique.
Source : [publier un package](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package).

## 6. Windows et Linux depuis le Mac ; rôle de GitHub

Oui, le SDK .NET peut cibler un RID différent de la machine de compilation.
Le script actuel permet, depuis la racine et avec accès à NuGet :

```bash
# Archives avec le runtime .NET inclus ; chaque dossier de sortie doit être neuf.
python3 scripts/archive-kernel.py win-x64 /tmp/ew-win-x64 --self-contained
python3 scripts/archive-kernel.py linux-x64 /tmp/ew-linux-x64 --self-contained
```

L’archive Windows contient notamment `EnterpriseWorkflow.Kernel.exe` ; Linux a
son exécutable `EnterpriseWorkflow.Kernel`. Distribuer le dossier complet de
l’archive, avec ses bibliothèques et ressources. « Autonome » signifie ici que le
runtime .NET est inclus ; les prérequis système natifs de l’OS restent applicables.
Ce n’est pas un exécutable unique. Nous conservons les modules dynamiques, sans
trimming ni Native AOT. Sans `--self-contained`, le serveur doit disposer du
runtime **ASP.NET Core 10** compatible. Une archive autonome devra être reconstruite
pour intégrer les correctifs du runtime.

Le Mac ne prouve pas qu’un binaire Windows/Linux fonctionne. Je recommande les
runners actuels `windows-2025`, `ubuntu-24.04` et `macos-15` pour construire et
tester les archives sur les OS correspondants. Le workflow candidat prépare déjà
ces trois cibles, actuellement en mode dépendant du runtime. Les essais des
archives et le mode autonome doivent encore être intégrés à la qualification.
La présence d’Oracle dans une archive ne vaut pas qualification Oracle sur cet OS.
Source : [déploiement .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/).

### Politique de tags proposée

- Commits et PR ordinaires : CI de développement, aucune publication publique.
- Tag `v1.0.0-rc.1` : candidat public explicitement identifié comme préversion.
- Tag `v1.0.0` : version stable après qualification.
- Le workflow final vérifie l’égalité entre tag et version du catalogue, que le
  commit appartient à la branche de release retenue, et les tests du même commit.
- Après succès des builds/tests et passage de l’environnement `nuget-release` :
  publication des packages, recette NuGet.org, puis GitHub Release avec archives,
  checksums et notes. Une panne après un envoi NuGet laisse une publication
  partielle à reprendre ; elle ne s’annule pas automatiquement.

Un tag Git, un artefact de workflow et une GitHub Release sont trois objets
distincts. Le déclenchement par tags est livré dans le workflow de publication ;
le workflow manuel candidat restera utilisable sans publier. Ne pas déplacer un
tag de version publiée : corriger avec une nouvelle version.
Source : [déclencheurs GitHub Actions](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow).

## 7. Arbitrages arrêtés et prérequis restants

Les recommandations sont acceptées. Aucun arbitrage Q06 n’est attendu.

| Sujet | Décision / état |
| --- | --- |
| Licence | Apache-2.0 |
| Propriétaire/auteur public | `mukadi.sheba`, compte individuel |
| Identifiants | 20 packages `EnterpriseWorkflow.*`, CLI `ew` ; disponibilité à vérifier avant envoi |
| Première publication | `1.0.0-rc.1` ; passage à la stable dans une release ultérieure |
| Déclenchement | Tags `v…` uniquement ; relance manuelle uniquement sur un tag |
| Authentification | Trusted Publishing, `publish-release.yml`, environnement `nuget-release` |
| Distribution | Windows x64, Linux x64, macOS ARM64 ; modes autonome et dépendant du runtime |
| Q06 | Clos sans exigences de charge/performance/SLA/RPO/RTO ni test correspondant |
| Configuration externe | Compte NuGet, politique Trusted Publishing, environnement et règles GitHub à terminer/vérifier par le porteur |
| Qualification technique | CI, archives, restauration, sécurité et inventaire des dépendances restent à la charge de l’implémentation L10 |

Les dépendances tierces conservent leurs conditions propres. La licence Apache
du framework ne les remplace pas. Le dimensionnement et les objectifs
opérationnels relèvent de chaque installation ; nous ne revendiquons pas de
niveau garanti. L’absence de tests Q06 ne retire pas les tests de cohérence,
concurrence ou sauvegarde/restauration.

## 8. Déroulé de la première release après configuration

1. Tu confirmes la configuration de la politique Trusted Publishing.
2. Je termine les vérifications locales ; la CI qualifie les plateformes lors
   de l’exécution distante. Les versions et le workflow sont livrés.
3. Nous vérifions le catalogue et les propriétaires avant le premier envoi. Les
   IDs, versions et métadonnées publiés doivent être définitifs pour cette version.
4. Après feu vert de qualification, créer et pousser uniquement le tag choisi
   sur le commit validé. Exemple futur pour la version stable :

   ```bash
   git tag -a v1.0.0 <SHA_DU_COMMIT_VALIDE> -m "Enterprise Workflow 1.0.0"
   git push github refs/tags/v1.0.0
   ```

5. Suivre le run GitHub et approuver l’environnement si cette protection a été
   configurée. En cas d’échec, conserver les logs et l’inventaire ; ne pas modifier
   le contenu d’une version déjà publiée pour relancer.
6. Après indexation NuGet, exécuter la recette publique dans un environnement
   neuf. La version du script est lue dans le catalogue (actuellement
   `1.0.0-rc.1`). Clôturer L10 seulement après la recette et les autres preuves G4.

### Configuration externe confirmée

```text
Politique Trusted Publishing : confirmée configurée par le porteur le 27 octobre 2026.
Environnement GitHub et règles : leur application sera vérifiée lors du run de release.
```
