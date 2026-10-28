---
title: "Enterprise Workflow"
description: "Durable workflows for C# applications, from the first approval to production operations."
sidebar:
  order: 0
---

Enterprise Workflow is a .NET 10 framework for durable business processes. Define a process in C#, persist its execution, coordinate human approvals and service work, and keep each running instance tied to the code that owns it.

This documentation targets **1.0.0-rc.1**, the first public release candidate. Obtain the compiled Windows Kernel archive through your software distribution channel, then install the CLI and SDK templates from the approved NuGet package source. The quick start runs that executable first with no processes, develops and tests a business module with `ew dev`, then packages and deploys it to the same executable. No framework source checkout is required.

## Choose your starting point

| You want to… | Start here |
| --- | --- |
| Build a business module with a ready-made UI | [Run your first workflow](getting-started/first-workflow/) |
| Host the engine inside your own application | [Use embedded hosting](getting-started/embedded/) |
| Deploy the modular host | [Install the Kernel](operations/install/) |
| Learn through a complete business example | [Follow LeaveRequest](examples/leave-request/) |

## What you can build

Use typed business state, server-side validation, human task assignment, comments, forms, private attachments, notifications, authorized instance search, audit exports and turnaround-time reports. Persist with SQLite or Oracle through the corresponding adapters. Extend the public handler, identity, storage and presentation contracts.

A typical process moves from a submission to a human task, through a decision or service, and into a terminal business outcome. Durable waiting does not require an HTTP request or an in-memory object to remain alive.

## Read in order, or solve a specific problem

**Getting Started** gets you to a working application. **Basics** covers daily development. **Core Concepts** explains the execution model. **Guides** solves individual implementation problems. **Tutorials & Examples** follows complete cases. **Deployment & Operations** covers installation and maintenance. **Advanced Topics** explains compatibility and recovery. **Reference** lists commands and contracts.

The delivered graph model is sequential with exclusive decisions; loops, parallel branches and a visual Studio are outside this release. Read [compatibility and limits](reference/compatibility/) before choosing a deployment target. The project uses [Apache-2.0](https://github.com/mbo2olivier/enterprise-workflow/blob/main/LICENSE); third-party dependencies retain their own terms.
