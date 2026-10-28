---
title: "LeaveRequest walkthrough"
description: "Follow the complete requester \u2192 manager \u2192 HR business flow."
sidebar:
  order: 2
---

LeaveRequest is the running example throughout this wiki. Its business module release is **1.8.0**, process revision **9**, state schema **4**, using framework **1.0.0-rc.1**. Business labels in the shipped sample are French; the steps below name the corresponding roles and actions in English.

## Obtain a standalone NuGet example

Use the [documentation source](https://github.com/mbo2olivier/enterprise-workflow/tree/main/wiki/examples/LeaveRequest) added with this wiki. The example pins framework packages to **1.0.0-rc.1**, independently of the documentation branch. Clone the repository and copy that example outside it:

```bash
git clone https://github.com/mbo2olivier/enterprise-workflow.git
cd enterprise-workflow
```

```bash
cp -R wiki/examples/LeaveRequest ../LeaveRequest.Tutorial
cd ../LeaveRequest.Tutorial
dotnet restore
ew dev . --environment tutorial
```

Run the copy commands from the documentation checkout root. The folder includes a Razor project, module source, summary view, explicit NuGet references and `ew.dev.json`. There is no need to compile the framework solution. On Windows copy the folder with your shell or file manager.

## Actor walkthrough

| Actor | Seeded permissions | Action |
| --- | --- | --- |
| Alice | request + read | Create a request, select her manager and confirm |
| Carol | manager approval + read | Approve or reject the designated manager task first |
| Bob | HR review + read | Claim and validate the HR task after manager approval |

1. Select Alice. Create a request with valid dates, leave type and a reason. Optionally upload a supporting document allowed by the module policy.
2. Alice receives the confirmation task. Search for and select Carol as the responsible manager, then choose **Confirmer**. Manager selection is required to submit. Choose **Annuler** to cancel without selecting a manager. The node ID `draft` is a post-start business confirmation; it is not an unsaved-draft subsystem.
3. Switch to Carol. Open the assigned **Validation du responsable** task and choose **Approuver**. HR has no task for this request until this approval succeeds.
4. Switch to Bob. Claim the **Validation RH** task, then choose **Valider**. HR is the final approval stage and does not select the manager.
5. The closing service marks the request closed and generates a text document through the context media API. The instance ends with an accepted business outcome.

Manager designation is explicit in this sample: Alice selects an eligible person. The sample does not resolve reporting lines automatically from an organization chart or LDAP. In a real installation, ensure the selected eligible actor represents the requester's actual line manager.

To test rejection, start a new instance and choose **Refuser** at manager or HR review. A comment is required. Manager rejection ends the process without creating an HR task; HR rejection ends it without running the closing service. Read the detail's state, comments, progression and media library with an authorized identity.

## Follow the implementation

`LeaveRequestInitializer` explicitly maps form input into `LeaveRequestState` and collects uploaded files. `LeaveDatesValidator` rejects an end date before the start date. `PrivateNote` is hidden in the instance state detail. The summary component receives server values and filtered state.

The requester confirmation action uses `SelectAssigneeFor("validation-manager")`; the manager task uses `AssignedFromPreviousTask()`. Manager approval transitions to `validation-rh`, an eligible pool task. HR validation transitions to `close-request`. Both approval stages map optional approval comments and required rejection comments into the typed `Comments` collection. No browser-supplied author or timestamp is trusted.

`CloseRequestHandler` obtains `media.For(context)`, writes with the stable operation key `closure-summary`, then returns the full updated state. File attachment becomes visible only with the successful commit.

## Package and replay

```bash
ew pack . --configuration Release --output ./artifacts
ew verify ./artifacts/sample.leave-request/1.8.0
ew dev --artifact ./artifacts/sample.leave-request/1.8.0 --environment packaged --config ./ew.dev.json
```

The packaged environment is independent of `tutorial`. Do not reuse `.ew` development artifacts in production. Continue with [media](../../guides/media/), [audit/TAT](../../guides/audit-tat/) and [module deployment](../../operations/modules/).

## Updating from the previous sample

Module **1.8.0**, revision **9**, changes the approval order; the state schema remains **4** because its DTO is unchanged. Retain module **1.7.0** for existing instances: their immutable definition still runs HR before manager. This update does not silently reorder running requests. The framework packages remain **1.0.0-rc.1**.
