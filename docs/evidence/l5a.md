# Preuves L5a — LDAP générique

Date : 3 octobre 2026. R1 est arbitré dans l’ADR 0018 : rupture de l’ancienne API AD, image OpenLDAP prête à l’emploi, LDAPS par défaut, clair explicitement sélectionnable et qualification réelle portée par Linux.

## Livrables

- `LdapDirectoryProvider`, `LdapDirectoryOptions` et `ILdapServiceCredentialProvider`, sans hypothèse Active Directory ;
- schéma configurable, codecs d’identifiants chaîne/GUID little-endian/hex/base64, identifiant `entryUUID` par défaut ;
- stratégies de groupes absents, attribut utilisateur ou recherche de groupes par membre ;
- capacités explicites recherche/groupes/statut et statut `Unknown/Enabled/Disabled` ;
- revalidation des sessions distantes uniquement lorsque la capacité de statut est réellement annoncée ;
- LDAPS par défaut, LDAP clair uniquement avec `LdapTransportMode.PlainText`, referrals désactivés et aucun downgrade automatique ;
- stack OpenLDAP 2.6 multiarchitecture épinglée par digest, données LDIF et certificats éphémères ;
- job GitHub Actions OpenLDAP réel limité à Linux ; Windows et macOS conservent les tests unitaires/contractuels non bloquants.

## Résultats locaux

- build Release du projet sécurité et de ses tests : zéro avertissement, zéro erreur ;
- huit tests sécurité réussis sur macOS ARM64 ;
- qualification OpenLDAP réelle en mode clair explicitement sélectionné : login, recherche, identité stable après renommage, groupes directs par recherche, statut actif/inactif, panne et bind utilisateur ;
- négociation et chaîne LDAPS de la stack vérifiées avec OpenSSL ; la pile LDAP native macOS ne permet pas d’injecter une CA par callback de connexion, la preuve LDAPS applicative reste donc celle du job Linux ;
- qualification Linux LDAPS, certificat au mauvais nom et matrice complète à confirmer sur GitHub Actions.

Le lot ne revendique ni Active Directory, ni StartTLS, ni groupes imbriqués.
