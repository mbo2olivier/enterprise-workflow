---
title: "SMTP notifications"
description: "Enable pool email delivery with explicit installation settings."
sidebar:
  order: 8
---

Inbox tasks work without SMTP. Configure `Notifications:Smtp` in the Kernel or `smtp` in `ew.dev.json` to enable pool notification delivery.

For a local mail capture service listening on port 1025, this development fragment can be merged into `ew.dev.json`:

```json
{
  "smtp": {
    "host": "localhost",
    "port": 1025,
    "enableSsl": false,
    "from": "workflow@example.test",
    "baseUrl": "http://localhost:5080",
    "addresses": {
      "local|ew-dev-bob": "bob@example.test"
    }
  }
}
```

This requires a running capture server; `enableSsl: false` is for that local setup. Production settings need the actual relay, protected credentials, TLS requirements and externally reachable application URL. Local accounts use `provider|subject` address mappings; directory identities use their current email when available.

The transport fans out durable notification work and rechecks eligibility before sending. A recipient who lost access must not receive a newly delivered eligible-task notification merely because it was queued earlier. SMTP errors follow outbox retry behavior; the task remains available in the inbox.

Test with a capture inbox and separate actors. Stop the SMTP service, create a pool task, observe failure/retry, restore SMTP and verify delivery. Receiving one local test message does not establish Internet deliverability or production relay configuration.

Defaults: port 587, SSL enabled, page size 50 and send timeout 15 seconds. `From` is required when a host is configured. See [runtime and outbox](../../reference/runtime/).
