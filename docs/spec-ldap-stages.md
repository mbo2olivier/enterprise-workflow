# Recadrage LDAP et workflow stages

Date : 3 octobre 2026, mise à jour le 4 octobre 2026. Sources S12 : recadrage explicite du porteur ; S13 : précision que le nœud est l’équivalent du stage dans l’administration ; S14 : qualification OpenLDAP/LDAPS Linux GitHub Actions confirmée. Les besoins sont confirmés ; les recommandations L5b/L6 ci-dessous n’ont pas valeur d’arbitrage accepté. Cette spécification complète le PRD et prévaut sur les anciennes obligations AD.

## Exigences et séquencement

EF-08 cible une extension LDAP indépendante d’AD ; EF-16 introduit les stages et les droits par workflow, nœud et action. Un stage est le nœud vu dans l’administration, sans entité ou identifiant distinct. Les [ADR 0018](adr/0018-ldap-generique.md) et [0019](adr/0019-workflow-stages.md) définissent les conséquences et critères de qualification.

Priorité : **L5a LDAP générique terminé → L5b habilitations de stages → L6 tâches humaines → L8 administration graphique complète**. L5b livre ses services/contrats, migrations et endpoints administratifs protégés sans attendre l’UI L8. Les preuves acquises L1–L5 restent historiques ; seule la preuve propre à L5a qualifie LDAP.

## Constats sur le code validé

- Avant L5a, `ActiveDirectoryProvider` utilisait LDAPv3/LDAPS mais conservait `objectGUID`, une conversion binaire en `Guid` et une résolution de groupes par DN. L5a le remplace par un schéma et des codecs configurables, ainsi que des capacités explicites de recherche/groupes/statut.
- `AuthorizationRequest` possède `ResourceType/ResourceId`, mais `InternalProfileAuthorizationProvider` ne les évalue pas ; `SecurityProfile` porte un ensemble global de permissions. L5b ajoute un contexte serveur précis et des grants bornés ; la présence actuelle de champs ressource ne prouve pas cette granularité.
- Les profils directs, mappings de groupes facultatifs, sessions, bootstrap et audit restent réutilisables. Les habilitations de stages restent locales à l’installation et indépendantes de LDAP.
- L2/L4 ne livrent pas de tâches humaines ; préserver leurs définitions techniques et leurs hashes sans ajouter un objet stage est nécessaire. L6 peut intégrer les nouveaux contrats avant toute livraison métier.

## Arbitrages à remonter avant les lots concernés

| ID / échéance | Contrainte | Recommandation | Alternatives et conséquence |
| --- | --- | --- | --- |
| R1 — décidé pour L5a | Aucun usage du framework en cours ; conserver l’API AD créerait une dette inutile | **Décision :** remplacement direct par l’API LDAP générique ; image prête à l’emploi `vegardit/openldap:2.6.x` épinglée ; LDAPS par défaut, clair seulement par configuration explicite, sans downgrade automatique ; StartTLS différé ; qualification réelle Linux et contrats multi-OS | Façade AD écartée ; image construite dans le dépôt écartée ; une qualification réelle Windows/macOS pourra être ajoutée lorsqu’un service compatible est disponible |
| R2 — décidé : A | ADR 0017 choisit une personne ; un stage décrit plusieurs personnes éligibles | **Décision :** mode par nœud, personne désignée pour approbation ou pool avec claim exclusif pour tâches de poste ; L5b calcule l’éligibilité, L6 persiste affectation/claim | Désignation obligatoire partout écartée |
| R3 — décidé : A | L5 cumule des permissions globales ; anciennes instances et révocation doivent coexister | **Décision :** grants positifs exacts par workflow/version/nœud/action ; structure figée avec l’instance, habilitations courantes revalidées à chaque commande | Grants multi-versions, droits figés et refus explicites écartés |
| R4 — avant le sample bancaire L6 | ADR 0011 bloque par défaut l’auto-approbation du demandeur, sans définir toutes les séparations maker/checker | Contrainte configurable interdisant au validateur de valider sa propre action maker, fondée sur l’historique durable, en plus de 0011 | Réutiliser seulement 0011, insuffisant si le maker n’est pas le demandeur ; imposer partout deux personnes distinctes réduit la souplesse |
| R5 — décidé : A | Les permissions globales existantes sont des chaînes ; les définitions L2/L4 ne déclarent aucune action humaine | **Décision :** identifiants typés, catalogue framework et actions déclarées par les types interactifs ; toute mutation est validée contre le catalogue exact | Enumération fermée et chaînes administratives libres écartées |
| R6 — décidé : A | Il n’existe encore ni `HumanTask`, ni activation humaine, ni claim durable | **Décision :** L5b livre politiques, autorisation, administration/audit et candidats éligibles ; L6 livre tâche, affectation, claim, inbox et complétion atomique | Fusion L5b/L6 et report total des candidats écartés |
| R7 — décidé : A | Le droit d’agir, le droit de voir une instance et la visibilité des données ne sont pas équivalents | **Décision :** grants `instance.read` séparés des actions de stage, aucun accès intégral implicite ; visibilité tâche/champs en L6/L8 | Lecture complète implicite et règles par champ prématurées écartées |

R2/R3/R5/R6/R7 sont confirmés et appliqués par L5b. Le besoin LDAP, la granularité des stages et l’absence d’entité `Stage` distincte restent inchangés. R4 est concrétisé par la politique versionnée D6-A de L6 ; sa démonstration bancaire complète reste attendue avec l’UI L8.
