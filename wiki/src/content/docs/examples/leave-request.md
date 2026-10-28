---
title: "LeaveRequest walkthrough"
description: "Follow the complete requester \u2192 manager \u2192 HR business flow."
sidebar:
  order: 2
---

LeaveRequest is the running example throughout this wiki. Its business module release is **1.8.0**, process revision **9**, state schema **4**, using framework **1.0.0-rc.1**. Business labels in the shipped sample are French; the steps below name the corresponding roles and actions in English.

## 1. Start the empty Windows runtime

Use the supplied **win-x64 self-contained Kernel archive**, extracted into `C:\EnterpriseWorkflow\Runtime`. Follow [the first workflow tutorial](../../getting-started/first-workflow/#1-start-the-empty-runtime) to migrate a new local SQLite installation, bootstrap administration and run `EnterpriseWorkflow.Kernel.exe` at `http://localhost:5100` with `Modules:Directories` set to `[]`.

Sign in and show that no business module is configured. Keep this runtime running while preparing the SDK project in a second PowerShell window. No framework source checkout or rebuild is needed.

## 2. Build the LeaveRequest SDK example

Install the [CLI and SDK templates](../../getting-started/install/). Obtain the [standalone LeaveRequest source package](/enterprise-workflow/downloads/LeaveRequest.zip) from this documentation, or the same package through your internal software distribution channel. Save it as `C:\EnterpriseWorkflow\Downloads\LeaveRequest.zip`. This contains only the example module sources and its NuGet project, not the framework source code.

```powershell
New-Item -ItemType Directory -Force C:\EnterpriseWorkflow\Projects | Out-Null
Expand-Archive C:\EnterpriseWorkflow\Downloads\LeaveRequest.zip -DestinationPath C:\EnterpriseWorkflow\Projects
Set-Location C:\EnterpriseWorkflow\Projects\LeaveRequest
dotnet restore
ew validate .
ew inspect .
ew dev . --environment tutorial --url http://localhost:5101
```

The destination project must not already exist. Open `LeaveRequestModule.cs` to review or adapt the typed state, forms, manager assignment, approval rules and closing service. The project references framework **1.0.0-rc.1** packages through NuGet. Use the bank's approved package feed configuration where required.

For a new module from scratch, `dotnet new ew-module --name Acme.Leave --module-id acme.leave` generates the simpler two-actor starter described in [your first workflow](../../getting-started/first-workflow/). It does not generate this full manager-and-HR sample automatically.

## 3. Design and exercise the flow with `ew dev`

Open `http://localhost:5101`, the isolated development host. The Windows Kernel remains running separately at `http://localhost:5100` with an empty module catalogue. `ew dev` supplies its own SQLite data and the demo identities from `ew.dev.json`:

1. Select **Alice**. Enter valid leave dates, a leave type and a reason; confirm the request and select Carol as manager.
2. Select **Carol**. Approve the manager task.
3. Select **Bob**. Claim and validate the HR task. Inspect the closed state and generated document.
4. Repeat with rejection or cancellation to check the alternative paths. Rejection requires a comment.

Adapt the C# module while the development host watches source changes. Existing instances retain their generation; new instances use the rebuilt generation. See [the development cycle](../../basics/development-cycle/).

Stop `ew dev` with Ctrl+C when the flow is ready. Its local generations, database and identity selector remain development-only. Package the declared business release for the separate runtime.

## 4. Package and deploy to the same runtime

```powershell
ew pack . --configuration Release --output .\artifacts
ew verify .\artifacts\sample.leave-request\1.8.0
```

Stop the running Kernel with Ctrl+C in its original window. From the module project window, copy the complete verified artifact into a new version directory:

```powershell
New-Item -ItemType Directory -Force C:\EnterpriseWorkflow\Modules\sample.leave-request | Out-Null
Copy-Item -Recurse .\artifacts\sample.leave-request\1.8.0 C:\EnterpriseWorkflow\Modules\sample.leave-request\1.8.0
```

Do not overwrite an existing artifact. Update the `Modules` section of `C:\EnterpriseWorkflow\Runtime\appsettings.json`, preserving all other settings:

```json
"Modules": {
  "Directories": ["C:\\EnterpriseWorkflow\\Modules\\sample.leave-request\\1.8.0"]
}
```

Use each exact artifact directory, rather than a parent folder. Restart the same executable from the runtime directory:

```powershell
Set-Location C:\EnterpriseWorkflow\Runtime
$env:ASPNETCORE_URLS = 'http://localhost:5100'
.\EnterpriseWorkflow.Kernel.exe
```

The Kernel loads the module at startup. Copying files alone does not activate a process. For a runtime already hosting other modules, retain their configured paths and all artifacts still needed by running instances; see [module deployment](../../operations/modules/).

## 5. Provision actors in the Kernel

As administrator, use **Administration > Access** to create three local accounts, create profiles with the following permissions and assign the profiles to those accounts:

| Actor | Module permissions | Action |
| --- | --- | --- |
| Alice | `sample.leave-request.request`, `sample.leave-request.read` | Create, select manager and confirm |
| Carol | `sample.leave-request.manager.approve`, `sample.leave-request.read` | Approve or reject the manager task |
| Bob | `sample.leave-request.hr.review`, `sample.leave-request.read` | Claim and validate the final HR task |

Use individual passwords and separate sign-ins or browser sessions. The Windows Kernel does **not** load demo identities from `ew.dev.json` and has no development identity selector. The administrator has no implicit business approval rights.

## 6. Test the deployed process

1. Sign in as Alice. Create a request with valid dates, leave type and a reason. Optionally upload a supporting document allowed by the module policy.
2. Alice receives the confirmation task. Search for and select Carol as the responsible manager, then choose **Confirmer**. Manager selection is required to submit. Choose **Annuler** to cancel without selecting a manager. The node ID `draft` is a post-start business confirmation; it is not an unsaved-draft subsystem.
3. Sign in as Carol. Open the assigned **Validation du responsable** task and choose **Approuver**. HR has no task until this approval succeeds.
4. Sign in as Bob. Claim the **Validation RH** task, then choose **Valider**. HR is the final approval stage and does not select the manager.
5. The closing service marks the request closed and generates a text document through the context media API. The instance ends with an accepted business outcome.

Alice explicitly selects an eligible manager. The sample does not resolve reporting lines automatically from an organization chart or directory. In a real installation, ensure that the selected eligible actor represents the requester's actual line manager.

To test rejection, start a new instance and choose **Refuser** at manager or HR review. A comment is required. Manager rejection ends the process without creating an HR task; HR rejection ends it without running the closing service. Inspect state, comments, progression and media as an authorized reader.

To demonstrate durability, leave a request waiting for approval, stop and restart the same Kernel with its database and exact artifact retained, then complete the pending task. The complete demonstration now covers both modes: design and iterate with `ew dev`, then deploy and test the packaged module on the compiled Windows runtime. The two hosts keep separate databases, identities and configuration; only the packaged business artifact is transferred.

## Follow the implementation

`LeaveRequestInitializer` explicitly maps form input into `LeaveRequestState` and collects uploaded files. `LeaveDatesValidator` rejects an end date before the start date. `PrivateNote` is hidden in the instance state detail. The summary component receives server values and filtered state.

The requester confirmation action uses `SelectAssigneeFor("validation-manager")`; the manager task uses `AssignedFromPreviousTask()`. Manager approval transitions to `validation-rh`, an eligible pool task. HR validation transitions to `close-request`. Both approval stages map optional approval comments and required rejection comments into the typed `Comments` collection. No browser-supplied author or timestamp is trusted.

`CloseRequestHandler` obtains `media.For(context)`, writes with the stable operation key `closure-summary`, then returns the full updated state. File attachment becomes visible only with the successful commit.

## Updating from the previous sample

Module **1.8.0**, revision **9**, changes the approval order; the state schema remains **4** because its DTO is unchanged. Retain module **1.7.0** for existing instances: their immutable definition still runs HR before manager. This update does not silently reorder running requests. The framework packages remain **1.0.0-rc.1**.
