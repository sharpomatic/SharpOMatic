---
title: Transfers
sidebar_position: 7
---

Transfers let you export and import workflows, evaluations, connectors, models, and library assets between environments.
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

<img src="/img/transfers_export.png" alt="Custom model setup" width="700" style={{ maxWidth: '100%', height: 'auto' }} />

## Import

Imports are performed by uploading a transfer package in the editor.
Items are upserted by ID, so existing entries are updated and missing entries are created.
Note that no data is deleted by this import process.
When secrets are excluded from the export, existing secret values are preserved during import.
This allows you to manually set a secret in the staging or production environment without worrying that a new import will override that environment-specific data.

## Assets

Transfers only include library assets.
Run-scoped assets are excluded.
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

## Controller

### Import

You can use a utility such as **POSTMAN** to invoke the import end point. <br/>
The demo server has a url of **http://localhost:9001/api/transfer/import**, you will need to update this for your own domain.

<img src="/img/transfer_import_postman.png" alt="POSTMAN import" width="700" style={{ maxWidth: '100%', height: 'auto' }} />

### Export

You can use a utility such as **POSTMAN** to invoke the export end point. <br/>
The body needs to be JSON that specifies the identifiers of the instances wanted, or set the **all** property to true.

<img src="/img/transfer_export_postman.png" alt="POSTMAN export" width="700" style={{ maxWidth: '100%', height: 'auto' }} />


