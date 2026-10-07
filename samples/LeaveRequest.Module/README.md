# Module LeaveRequest

Ce module qualifie le parcours L8 avec une définition et un formulaire versionnés : saisie de la demande, désignation explicite d’un responsable habilité, approbation ou refus, puis notification via l’outbox.

Il est chargé comme tout module L7 depuis un artefact contenant `module.json`, l’assembly, le composant Razor déclaré et leurs empreintes SHA-256. Le packaging automatisé reste du ressort de la CLI L9 ; les tests L8 construisent cet artefact de qualification sans contourner le loader.
