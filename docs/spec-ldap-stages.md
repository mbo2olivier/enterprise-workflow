# Recadrage LDAP et workflow stages

Date : 3 octobre 2026. Sources S12 : recadrage explicite du porteur ; S13 : précision que le nœud est l’équivalent du stage dans l’administration. Les deux besoins sont confirmés ; les recommandations ci-dessous n’ont pas valeur d’arbitrage accepté. Cette spécification complète le PRD et prévaut sur les anciennes obligations AD. Aucun changement de code n’est réalisé dans ce recadrage documentaire.

## Exigences et séquencement

EF-08 cible une extension LDAP indépendante d’AD ; EF-16 introduit les stages et les droits par workflow, nœud et action. Un stage est le nœud vu dans l’administration, sans entité ou identifiant distinct. Les [ADR 0018](adr/0018-ldap-generique.md) et [0019](adr/0019-workflow-stages.md) définissent les conséquences et critères de qualification.

Priorité : **L5a LDAP générique → L5b habilitations de stages → L6 tâches humaines → L8 administration graphique complète**. L5a et L5b livrent services/contrats, migrations et endpoints administratifs protégés sans attendre l’UI L8. Les preuves acquises L1–L5 restent historiques ; elles ne qualifient pas les nouvelles capacités.

## Constats sur le code validé

- Avant L5a, `ActiveDirectoryProvider` utilisait LDAPv3/LDAPS mais conservait `objectGUID`, une conversion binaire en `Guid` et une résolution de groupes par DN. L5a le remplace par un schéma et des codecs configurables, ainsi que des capacités explicites de recherche/groupes/statut.
- `AuthorizationRequest` possède `ResourceType/ResourceId`, mais `InternalProfileAuthorizationProvider` ne les évalue pas ; `SecurityProfile` porte un ensemble global de permissions. L5b ajoute un contexte serveur précis et des grants bornés ; la présence actuelle de champs ressource ne prouve pas cette granularité.
- Les profils directs, mappings de groupes facultatifs, sessions, bootstrap et audit restent réutilisables. Les habilitations de stages restent locales à l’installation et indépendantes de LDAP.
- L2/L4 ne livrent pas de tâches humaines ; préserver leurs définitions techniques et leurs hashes sans ajouter un objet stage est nécessaire. L6 peut intégrer les nouveaux contrats avant toute livraison métier.

## Arbitrages à remonter avant les lots concernés

| ID / échéance | Contrainte | Recommandation | Alternatives et conséquence |
| --- | --- | --- | --- |
| R1 — décidé pour L5a | Aucun usage du framework en cours ; conserver l’API AD créerait une dette inutile | **Décision :** remplacement direct par l’API LDAP générique ; image prête à l’emploi `vegardit/openldap:2.6.x` épinglée ; LDAPS par défaut, clair seulement par configuration explicite, sans downgrade automatique ; StartTLS différé ; qualification réelle Linux et contrats multi-OS | Façade AD écartée ; image construite dans le dépôt écartée ; une qualification réelle Windows/macOS pourra être ajoutée lorsqu’un service compatible est disponible |
| R2 — avant L5b/L6 | ADR 0017 choisit une personne ; un stage décrit plusieurs personnes éligibles | Mode par nœud : personne désignée pour approbation, pool avec claim exclusif pour tâches de poste ; stage = nœud conformément à S13 | Désignation obligatoire partout, plus simple mais sans inbox de poste collective |
| R3 — avant L5b | L5 cumule des permissions globales ; anciennes instances et révocation doivent coexister | Grants positifs exacts par workflow/version/nœud/action ; structure figée avec l’instance, habilitations courantes revalidées à chaque commande | Grants couvrant plusieurs versions avec portée explicite et revue à publication ; droits figés à la création nécessitent un mécanisme prioritaire de révocation ; refus explicites nécessitent une règle de priorité |
| R4 — avant le sample bancaire L6 | ADR 0011 bloque par défaut l’auto-approbation du demandeur, sans définir toutes les séparations maker/checker | Contrainte configurable interdisant au validateur de valider sa propre action maker, fondée sur l’historique durable, en plus de 0011 | Réutiliser seulement 0011, insuffisant si le maker n’est pas le demandeur ; imposer partout deux personnes distinctes réduit la souplesse |

Ces arbitrages bloquent uniquement l’implémentation des mécanismes concernés. Le besoin LDAP et la granularité des stages sont déjà acceptés ; ne pas les remettre en question à travers ces choix techniques. Les détails de visibilité des données et de catalogue d’actions sont à formaliser dans les contrats L5b, sans déduire un accès intégral de l’appartenance à un stage.
