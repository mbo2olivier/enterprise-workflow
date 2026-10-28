# LeaveRequest — responsable hiérarchique avant RH

Date : 28 octobre 2026. Demande du porteur : faire approuver le congé par le responsable hiérarchique avant la validation RH, dans le sample et le fil rouge public.

## Comportement livré

Parcours : formulaire initial → confirmation du demandeur avec désignation du responsable → approbation du responsable → prise en charge/validation RH → service de clôture → fin acceptée.

La désignation du manager est déplacée de l’action RH à l’action `submit` de la confirmation. Le runtime valide le destinataire selon l’habilitation de `validation-manager`. Le sample ne résout pas automatiquement les liens hiérarchiques depuis un annuaire. L’annulation reste possible sans désignation. Les refus du responsable et des RH exigent toujours un commentaire et terminent la demande. Les RH n’ont plus de sélecteur de manager et leur action finale est `Valider`.

La release métier passe de 1.7.0 / révision 8 à **1.8.0 / révision 9**. Schéma d’état **4** et framework **1.0.0-rc.1** inchangés. Les anciens artefacts 1.7.0 doivent être conservés pour les instances existantes, qui gardent leur ordre RH → responsable ; aucune migration automatique ni réécriture de définition n’est effectuée.

## Alignements

- Déclaration et vue récapitulative du sample ; README du sample.
- Copie autonome consommant NuGet dans `wiki/examples/LeaveRequest`.
- Tutoriel public, concepts de versionnement, prochaines étapes, guide formulaires et README racine anglais.
- Tests du graphe, scénarios navigateur et moment du test de notification SMTP : panne avant l’approbation manager qui ouvre le pool RH.
- Chemins d’artefacts des workflows documentation/release-candidate et de la recette navigateur L9 alignés sur 1.8.0.
- L10b clos séparément sur confirmation du porteur que le wiki est désormais publié.

## Vérifications exécutées

- Build Release des tests Extensions et Web.Playwright : succès sans avertissement ni erreur.
- Suite Extensions : **9 tests réussis**, aucun échec/ignoré. Assertions du graphe chargé depuis l’artefact : désignation sur confirmation, manager avant RH, RH avant clôture, refus et annulation.
- CLI publique `1.0.0-rc.1` : packaging et vérification du sample autonome **1.8.0** réussis, avec les références NuGet publiques.
- Runtime `ew dev --artifact` sur SQLite et environnement neuf ; suite navigateur filtrée `*LeaveRequest*` : **4 scénarios réussis**, aucun échec/ignoré. Couverture : parcours accepté avec upload/document généré et recherche/audit/TAT ; annulation sans manager ; refus manager ; refus RH après accord manager ; commentaire obligatoire ; désignation obligatoire à la confirmation ; aucune tâche RH avant approbation manager ; accès négatifs.
- Parité source/vue/seed de l’exemple public : succès.
- Build wiki : **53 pages HTML**, **3 834 liens/ressources locaux vérifiés**, y compris fragments et liens du README.
- Scripts shell, YAML des deux workflows et `git diff --check` : succès.

Les tests navigateur ont été exécutés sans service SMTP : le scénario de panne/retry du relais reste conditionnel et n’est pas revendiqué comme réexécuté. Pas de nouvelle campagne Oracle/LDAP ni de nouvelle publication NuGet. Les mises à jour du sample/wiki sont locales ; leur publication suivra le circuit habituel du dépôt.
