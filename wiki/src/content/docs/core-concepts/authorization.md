---
title: "Human tasks and authorization"
description: "Separate authentication, reading, eligibility and task ownership."
sidebar:
  order: 6
---

An identity is a provider plus a stable subject ID. Authentication proves that identity; authorization determines the operations it may perform. A display name, email or LDAP group label is not a durable subject identifier.

Modules declare business permissions. Installations assign those permissions through their chosen security profiles. For example, LeaveRequest declares request, read, HR-review and manager-approval permissions, while the installation decides which profiles and identities receive them.

Reading an instance, being eligible for a task, claiming a pool task and completing an assigned task are separate checks. An administrative capability does not grant blanket business reading or task completion. Permissions are re-evaluated for protected operations; a previously displayed action is not an authorization token.

`AssignedToInitiator` models requester work. `ClaimedByEligibleParticipant` models a pool. `SelectAssigneeFor` with `AssignedFromPreviousTask` models designation of the next participant. The server validates the selected candidate for the target task rather than accepting an arbitrary subject from a browser.

Self-approval is forbidden by default. Test with at least requester, reviewer and manager identities when following [LeaveRequest](../../examples/leave-request/).

See [identity and permissions](../../guides/security/) for local and LDAP installation settings.
