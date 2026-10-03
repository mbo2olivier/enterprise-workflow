# ADR 0003 — Sécurité par extensions et administration intégrée

Date : 30 septembre 2026.

**Statut : Acceptée pour le principe et le mode AD ; contrats détaillés proposés.**

## Contexte

Une installation peut utiliser AD, une API tierce ou des comptes locaux ; ce choix ne doit pas modifier le cœur.

## Décision ou proposition

Authentification et autorisation extensibles, administration intégrée et capacités de gestion fournies par l’extension. AD vérifie un identifiant/mot de passe. Les comptes locaux appartiennent à une extension facultative ; aucun stockage de mots de passe imposé au moteur.

## Options considérées

AD codé dans le Core, comptes locaux obligatoires et SSO Windows obligatoire sont écartés. Une seule interface fusionnant authentification, gestion de comptes et affectation limiterait les extensions distantes.

## Conséquences

Identité stable par fournisseur et sujet. UI administrative adaptée aux capacités, mêmes contrôles serveur. Contrats de sessions, bootstrap, révocation et agrégation d’autorisation à préciser avant implémentation. LDAP sécurisé et ASP.NET Core Identity local sont des propositions, pas des bibliothèques validées.

## Validation attendue

Une même définition fonctionne avec extension locale et AD sans changement du moteur. Tests négatifs, panne du fournisseur, collisions d’identité, réinitialisation non supportée. Voir [sécurité](../architecture/security.md).

## Complément accepté pour L5

L’[ADR 0016](0016-securite-profils-l5.md) précise et remplace les propositions relatives à la composition, au bootstrap, aux sessions et au choix ASP.NET Core Identity. Il introduit les profils internes et leurs associations directes aux identités ou par groupes AD facultatifs. Les signatures techniques restent à définir.
