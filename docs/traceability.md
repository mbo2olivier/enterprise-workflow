# Traçabilité

Les liens associent besoin, décision, lot et preuve attendue. Sauf mention explicite dans une preuve de lot, les tests ci-dessous sont **à exécuter** ; leur présence ne vaut pas preuve acquise. T01, T02 et T18 sont acquis au niveau L2. T03 à T05 et la création/migration initiale de T13 sont acquis en L3. L4a acquiert le binding exact ; L4b acquiert le worker séquentiel, les retries, le fencing après interruption et la redelivery outbox dédupliquée de T06/T07/T19. L5 acquiert les contrats, comptes locaux, profils, sessions, API protégée et stores SQLite/Oracle de T10/T11. L5a acquiert T22 sur OpenLDAP réel. L5b acquiert la partie habilitation de T23 : portée exacte, catalogue, profils/identités, candidats, révocation, révision, audit et migrations SQLite/Oracle. Les contrôles de tâche/instance active restent à acquérir avec L6. Voir les preuves de lots sous [`docs/evidence`](evidence/).

| Exigence | ADR / conception | Lots | Tests |
| --- | --- | --- | --- |
| EF-01 Bibliothèques et Kernel | 0001, 0006 | L1, L7b, L9 | T14, T18 |
| EF-02 C# et nœuds | 0007 ; extensions | L2, L4 | T01, T02, T12 |
| EF-03 Durabilité | 0008 | L3a/b, L4 | T03–T07 |
| EF-04 SQLite/Oracle | 0002 ; compatibilité | L3a/b, L10 | T03–T09, T13 |
| EF-05 Tâches/formulaires | 0004 ; runtime | L6, L8 | T08, T10, T20 |
| EF-06 Timers/retries/annulation | 0008 | L4, L6 | T05–T09 |
| EF-07 Modules | 0009 | L7a/b | T12, T14 |
| EF-08 Administration et sécurité extensible LDAP | 0003, 0018 ; sécurité | L5, L5a, L8 | T10, T11, T22 |
| EF-09 Permissions et auto-approbation | 0003, 0011, 0019 ; sécurité | L5, L5b, L6, L8 | T08, T10, T11, T23, T24 |
| EF-16 Stages = nœuds et actions habilitées | 0019 ; recadrage | L5b, L6, L8 | T23, T24 |
| EF-10 Installations indépendantes | 0005 | L7b, L10 | T17 |
| EF-11 UI/thèmes | 0004, 0012 ; extensions | L8 | T10, T20 |
| EF-12 CLI/templates | 0010 ; extensions | L9 | T14 |
| EF-13 Versions | 0009 | L7a/b | T12, T13 |
| EF-14 Audit/diagnostic | 0008 ; exploitation | L3 à L10 | T06, T10, T13, T21 |
| EF-15 Studio différé | 0004, 0007 | L11 | T15, T16 |
| ENF-01 Plateformes | 0001, 0010 | L1, L10 | T14 |
| ENF-02 Infrastructure minimale | 0006, 0008 | L4, L7b | T14, T17 |
| ENF-03 Concurrence | 0008 | L3a/b, L4, L6 | T04–T09, T19 |
| ENF-04 Extensibilité | 0006 | L1 à L9 | T11, T12, T18 |
| ENF-05 Données/droits | 0003 | L5, L8 | T10, T11, T16, T21 |
| ENF-06 Utilisabilité | Extensions/UI | L8 | T20 |
| ENF-07 Exploitabilité | 0009, 0010 ; exploitation | L10 | T13, T14 |
| ENF-08 Performance | Q06 ; tests | L10 | T21, seuils à fixer |

## Décisions utilisateur

S3 confirme .NET/plateformes, les deux providers, l’administration extensible, le Studio différé et une organisation par installation. S4 précise Oracle 19c minimum, local/distant selon extension, AD par identifiant/mot de passe et matrice distincte par provider. S5 confirme l’auto-approbation configurable et interdite par défaut (0011), et Razor/Blazor avec formulaires déclaratifs et composants personnalisés (0012). S6 accepte les ADR 0006 à 0008, confirme le retour au MVP séquentiel sans parallélisme et retient GitHub Actions. S7 valide D1 à D6 pour L2 (ADR 0013) et autorise Docker Compose. S8 valide 1A, 2A et 3A pour L4a (ADR 0014). S9 valide 1A à 5A puis 6A pour L4b : configuration intégrale, outbox bornée/dead letter, jalon attente/callback et jalon migration d’état (ADR 0015). Voir [cadrage](cadrage.md) pour les limites d’autorité de la source initiale.

S12 remplace la cible obligatoire AD par LDAP et introduit les stages ; S13 précise stage = nœud ; S14 confirme la réussite de la qualification LDAP Linux GitHub Actions. Voir ADR 0018/0019. R1 est clos ; les arbitrages techniques L5b/L6 restants sont proposés dans la spécification de recadrage.

## Préconditions encore ouvertes

Q06, Q07, Q08 et Q09b restent tracées au cadrage et aux portes du plan. Q11 est résolue par l’ADR 0017. Q10 est résolue pour L1 et sera revue à la qualification de distribution. Ces points n’annulent pas les décisions confirmées. Un lot dépendant ne doit pas choisir silencieusement à la place du porteur.
