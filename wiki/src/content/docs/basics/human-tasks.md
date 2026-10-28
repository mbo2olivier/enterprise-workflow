---
title: "Human tasks and forms"
description: "Combine eligibility, assignment and server-side validation."
sidebar:
  order: 4
---

A human task combines an assignment policy, permission requirements, a form or integrated approval UI, actions and transitions. Eligibility alone does not mean ownership: a pool task may need to be claimed before completion.

The minimal template uses `Approval(...).ClaimedByEligibleParticipant()` with two actions. `approve` has an optional comment; `decline` requires one. The action chooses the next node in the process declaration.

For a custom input task, register a form with fields and a completion handler, then reference that form from the task. Read submitted values with `context.ReadSubmission<TInput>()`. Return `HumanTaskCompletionResult.Success()` to accept without a state change, or supply `stateUpdate: NodeStateUpdate.Replace(state)` for a full typed replacement.

The runtime derives the allowed outcome from the authorized action. A handler does not bypass assignment, permissions or state validation. Approval of one's own request is forbidden by default; test with separate requester and reviewer identities.

Use `ShowFields(...)` for an explicit task summary. A start-form field's audience and the instance state's visibility are distinct contracts. Keep private information out of a DTO projection unless the intended readers may see it.

See [forms and components](../../guides/forms/), [identity and permissions](../../guides/security/) and [the approval tutorial](../../examples/minimal-approval/).
