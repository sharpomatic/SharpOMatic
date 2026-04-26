---
title: Transfers
sidebar_position: 8
---

Transfers let you export and import workflows, evaluations, connectors, models, and library assets between environments.
Library asset folder structure is included as part of the transfer data.
A typical usage is to export new or updated entities from your development environment.
These can then be imported into downstream environments, such as staging or production, as part of a release process.
Another use is to back up your entities for safekeeping.
It is recommended to store important data in a source control system for change and version tracking.

## Export

Exports are generated from the editor Transfer page.
You can select all items or choose specific workflows, evaluations, connectors, models, and library assets.
The export is a zip package that includes a manifest and the selected data.
By default, secrets are not exported to prevent accidental exposure of sensitive data.
You can use the checkbox to override this and export secrets as well.
If selected assets belong to folders, those folders are included automatically.

<img src="/img/transfers_export.png" alt="Custom model setup" width="700" style={{ maxWidth: '100%', height: 'auto' }} />

## Import

Imports are performed by uploading a transfer package in the editor.
Workflows, connectors, models, library asset folders, and library assets are imported with their stored metadata so existing content can be matched and updated where possible.
Evaluations are imported as new instances with new identifiers to avoid collisions when importing into the same system.
This means existing evaluation runs remain intact and linked to the original evaluation configuration.
When secrets are excluded from the export, existing secret values are preserved during import.
This allows you to manually set a secret in the staging or production environment without worrying that a new import will override that environment-specific data.

## Evaluations

Evaluation transfer includes the full configuration:

- EvalConfig
- EvalGraders
- EvalColumns
- EvalRows
- EvalData

Evaluation run result data is not transferred:

- EvalRun
- EvalRunRow
- EvalRunRowGrader
- EvalRunGraderSummary

## Assets

Transfers only include library assets.
Run-scoped and conversation-scoped assets are excluded.
Asset binaries are included in the package.

## Program Setup

You must add the **AddSharpOMaticTransfer** call into your program setup for the transfer operations to work.
If you forget this line then the editor will show the transfer option but all attempts to import or export will fail.
In production you can add this transfer capability without the editor.
You can then invoke the controller endpoints directly. 
For example, to import the latest versions of your workflows, models, assets etc.

```csharp
builder.Services.AddSharpOMaticTransfer();
```

When the package is registered, the transfer endpoints are exposed under `/sharpomatic/api/transfer/...`.
If you override the SharpOMatic base path, pass the same base path into `AddSharpOMaticTransfer(...)` that you use for the editor and AG-UI registration.

## Controller

### Import

You can use a utility such as **POSTMAN** to invoke the import endpoint. <br/>
The demo server has a url of **https://localhost:9001/sharpomatic/api/transfer/import** and also exposes HTTP on **http://localhost:9000/sharpomatic/api/transfer/import**.

<img src="/img/transfer_import_postman.png" alt="POSTMAN import" width="700" style={{ maxWidth: '100%', height: 'auto' }} />

### Export

You can use a utility such as **POSTMAN** to invoke the export endpoint. <br/>
The body needs to be JSON that specifies the identifiers of the instances wanted, or set the **all** property to true.
The demo server export endpoint is **https://localhost:9001/sharpomatic/api/transfer/export** and also **http://localhost:9000/sharpomatic/api/transfer/export**.

<img src="/img/transfer_export_postman.png" alt="POSTMAN export" width="700" style={{ maxWidth: '100%', height: 'auto' }} />
