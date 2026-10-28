---
title: "Minimal approval"
description: "Use the generated module as a small complete tutorial."
sidebar:
  order: 1
---

This tutorial uses the full module generated in [your first workflow](../../getting-started/first-workflow/). It is intentionally smaller than LeaveRequest: a start form, an approval and a closing service.

## Trace one request through the code

Open the generated module source alongside `ew inspect .`. `Module.ConfigurePermissions` declares request, review and read permissions. `RequestProcess` requires request permission at start, declares read access and references `request-form`. The initializer reads `RequestForm` and creates `RequestState` through `IWorkflowSerializer`.

The approval is a pool task. Its `approve` action stores an optional comment and transitions to `close`; its `decline` action requires a comment and transitions to `rejected`. The closing service replaces the full DTO with `Closed = true` before the accepted end node is reached.

## Verify the behavior

1. Start as Alice with a valid reason. Expect a waiting approval.
2. Try to approve as Alice. Expect denial under the default separation rule.
3. Claim as Bob, then approve. Expect an accepted terminal outcome and closed state.
4. Start another instance and decline without a comment. Expect validation failure.
5. Add a comment and decline. Expect a declined outcome and the recorded comment.

Stop and restart the development host with a request still waiting; Bob should still be able to finish that instance. The task is stored, not kept alive by the browser.

## Make a controlled change

Change only a display label and save the source while `ew dev` watches. Expect a new local generation. Old instances retain their original artifact; a new request uses the new generation. For a production change, follow [the evolution tutorial](../evolution/) instead of distributing `.ew` generations.
