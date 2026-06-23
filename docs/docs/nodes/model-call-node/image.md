---
title: Image
sidebar_position: 3
---

If the model supports image input or image output, this tab is available.

**Image Input Path** must point to an **AssetRef**, an absolute HTTP/HTTPS image URL string, or a list containing **AssetRef** values and/or image URL strings.
You can insert these **AssetRef** values using the editor for interactive development purposes.
Programmatically, you can add them when creating the initial context to insert into the workflow or inside a **Code** node.
Note that these assets and URLs must be image types; they cannot be text, PDF documents, or binary data.

When the path resolves to a URL string, SharpOMatic sends it to the model as `UriContent` instead of downloading and storing it as an asset.
The URL must be absolute, use `http` or `https`, and include a file extension that resolves to an `image/*` media type.

See the [Assets](/docs/core-concepts/assets) page in Core Concepts for programmatic examples.

<img src="/img/modelcall-image.png" alt="Asset Substitution" width="600" style={{ maxWidth: '100%', height: 'auto' }} />
