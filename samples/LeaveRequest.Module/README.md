# Module LeaveRequest — L8b

La déclaration complète est dans [LeaveRequestModule.cs](LeaveRequestModule.cs). Elle illustre les métadonnées uniques, le schéma d’état explicite, les champs fluents, les permissions métier, les processus séparés et les formulaires d’approbation intégrés.

Parcours : formulaire initial → confirmation du demandeur → pool RH → manager sélectionné depuis l’action RH → service de clôture → fin. Le refus RH ou manager exige un commentaire ; l’annulation de la confirmation est une branche métier distincte. Le stage `draft` ne crée aucun mécanisme de brouillon avant démarrage. Le handler de clôture est volontairement un service minimal à remplacer par le traitement métier de l’installation.

Permissions déclarées, attribuables dans l’administration des profils sous les noms de profils choisis par l’installation :

| Permission qualifiée | Usage |
| --- | --- |
| `sample.leave-request.request` | Créer puis confirmer sa demande |
| `sample.leave-request.read` | Lire les instances ; cette attribution est indépendante du traitement d’une tâche |
| `sample.leave-request.hr.review` | Prendre puis traiter une tâche RH |
| `sample.leave-request.manager.approve` | Être sélectionné et statuer comme responsable |

Prévoir au moins trois identités pour démontrer les séparations : demandeur, RH et manager. L’approbation de sa propre demande est interdite par défaut. Le choix du manager utilise les candidats habilités de la tâche cible ; aucun rôle ou nom de groupe LDAP n’est inscrit dans le module.

Le formulaire métier déclare son handler indépendamment de son enregistrement. Les tâches de confirmation et d’approbation utilisent les formulaires standard ; la confirmation montre une substitution explicite de handler. Les résumés exposent uniquement les champs déclarés pour les participants. `privateNote` n’apparaît pas dans les résumés. Les commentaires sont validés mais le handler standard ne les fusionne pas dans les données métier ; cette conservation relève d’un handler métier si elle est requise.

Le manifeste est produit depuis le module avec `WorkflowModuleManifestWriter.Create` ; le test `LeaveRequestModuleTests` construit l’artefact et le charge réellement. Les composants restent manifestés et hachés avec leur artefact. Le processus est qualifié sous `sample.leave-request.leave-request`, révision 1.

Les emails de pool sont activés uniquement si `Notifications:Smtp:Host` est configuré dans le Kernel. Sans SMTP, le parcours inbox est identique. Pour les comptes locaux, fournir la correspondance `Notifications:Smtp:Addresses` avec une clé `local|<subjectId>` ; les identités d’annuaire utilisent leur email courant lorsqu’il est disponible. Les secrets SMTP restent dans la configuration de l’installation.

Le [guide SDK](../../docs/sdk-fluent-design.md) décrit les contrats ; [l8b-validation.md](../../docs/l8b-validation.md) contient les tests externes restant à exécuter.
