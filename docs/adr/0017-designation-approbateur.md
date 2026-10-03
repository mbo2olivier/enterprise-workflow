# ADR 0017 — Désignation explicite de l’approbateur

Date : 3 octobre 2026. **Statut : acceptée.** Source : décision explicite du porteur de désigner l’approbateur au démarrage du workflow ou lors de l’activité concernée, parmi les utilisateurs habilités par l’administration.

## Contexte

Une relation hiérarchique figée ou l’attribut AD `manager` ne suffisent pas aux entreprises dont les approbateurs disponibles varient, notamment pendant les congés. AD peut servir uniquement à authentifier et rechercher les identités.

## Décision

- L’administration habilite les personnes pouvant approuver par une permission métier portée par un profil interne. Un profil « Manager » peut regrouper cette permission ; le nom du profil n’est pas en lui-même une autorisation. Les associations directes et les mappings de groupes AD de l’ADR 0016 restent applicables.
- Pour un workflow ou une activité exigeant une approbation, l’utilisateur qui démarre le workflow ou réalise l’activité désigne explicitement la personne qui approuvera. La définition précise à quelle étape cette désignation est requise ; aucun approbateur n’est déduit automatiquement d’un attribut hiérarchique AD.
- Le sélecteur recherche par nom dans les identités habilitées à approuver dans le contexte concerné. La recherche ne donne pas accès à un annuaire complet sans droit. L’utilisateur choisit un résultat explicite ; le système conserve `(ProviderId, SubjectId)`, pas le texte saisi, l’email ou le nom affiché. Les homonymes doivent être distinguables.
- Le serveur vérifie la désignation, les permissions du candidat et les contraintes métier. Une sélection absente, ambiguë ou non autorisée empêche la validation du démarrage ou de l’activité qui exige cette sélection. Le client ne peut pas contourner le filtrage en soumettant une identité arbitraire.
- L’approbateur choisi et la référence de la politique d’affectation versionnée sont persistés durablement. La tâche reste affectée à cette identité après redémarrage. À l’activation et à la soumission, le système vérifie à nouveau les droits applicables ; la sélection antérieure ne confère pas une permission permanente.
- La règle d’auto-approbation de l’ADR 0011 reste applicable : interdite par défaut, exception explicite par workflow soumise aux autres droits et à l’affectation. Le filtrage et la validation serveur appliquent la même règle.
- Le choix flexible au démarrage ou à l’activité n’implique pas une réaffectation libre d’une tâche déjà ouverte. La délégation, la connaissance des absences et le remplacement après ouverture restent des fonctionnalités distinctes à cadrer ; aucun remplacement automatique silencieux.

## Portes et conséquences

Q11 est résolue et la porte d’arbitrage sur la source du responsable est levée. Le terme N+1 désigne ici l’approbateur explicitement choisi parmi les personnes habilitées ; il ne certifie pas une relation hiérarchique dans l’annuaire.

G2a conserve ses autres exigences avant L6 : détails de politique d’affectation versionnée, référence `FormId/FormVersion`, contrat de soumission et séparation validation métier/rendu UI. L5 livre les permissions, profils et capacités de recherche nécessaires ; L6 livre le contrat et la persistance de la désignation, sans dépendre de l’UI ; L8 livre le sélecteur graphique.

## Validation attendue

Identité AD habilitée par profil direct sans groupe AD ; recherche limitée aux candidats éligibles ; homonymes ; sélection manquante ; identité arbitraire soumise par API ; auto-approbation ; permission retirée entre sélection et activation ou complétion ; reprise conservant le destinataire choisi. Les refus doivent rester explicites, sans attribution implicite à une autre personne.
