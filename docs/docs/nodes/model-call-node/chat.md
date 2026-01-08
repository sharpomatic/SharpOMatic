---
title: Chat
sidebar_position: 4
---

All models can take advantage of the chat tab.

This tab enables you to create and pass in a list of chat messages that are directly passed into the model call.
It can be used on its own or in conjunction with the text and image tabs.
Using this ability gives you greater flexibility and control over the exact content sent to the model call.

## ChatMessage

Model calls pass in a list of **ChatMessage** instances.
By default, a new empty list of **ChatMessage** is created.
If you specify content in the text tab, then the instructions and prompt are turned into chat messages and added to the list.
If you specify image context via the image tab, then those images are turned into chat messages and appended to the list.



## Input messages

Use the **Chat Input Path** to specify a **ChatMessage** instance or a list of **ChatMessage** entries to initialize the content.
After this, it adds instructions, prompts, and images if they are defined in the text and/or image tabs.
You can leave the text and image tabs blank so that only the path-defined content is used.

For example, the following **Code** node creates system instructions, user prompt and an image.

```csharp
  var list = new ContextList();

  // Add system instructions
  list.Add(new ChatMessage(ChatRole.System, 
    [new TextContent("Use a greeting to respond and append the current time.")]));

  // Add user prompt
  list.Add(new ChatMessage(ChatRole.User, 
    [new TextContent("Describe the content of the attached image.")]));

  // Get services from workflow-scoped service provider
  var repositoryService = ServiceProvider.GetRequiredService<IRepositoryService>();
  var assetStoreService = ServiceProvider.GetRequiredService<IAssetStore>();

  // Get AssetRef for a named library asset
  var assetRef = await repositoryService.GetLibraryAssetByName("headshot.png");

  // Get a byte stream for the asset
  using(var stream = await assetStoreService.OpenReadAsync(assetRef.StorageKey))
  {
    // Load the bytes into a memory buffer
    using var buffer = new MemoryStream();
    await stream.CopyToAsync(buffer);

    // Images are specified as DataContent with the correct media type
    var content = new DataContent(buffer.ToArray(), assetRef.MediaType) { Name = assetRef.Name };
    list.Add(new ChatMessage(ChatRole.User, [content]));

    // Put all the messages into the context so it can be used by the model call
    Context.Set<ContextList>("input.request", list);
}
```

This is then referenced by the **Chat Input Path**.

<img src="/img/modelcall-chat-paths.png" alt="Chat Paths" width="600" style={{ maxWidth: '100%', height: 'auto' }} />

## Output messages

If defined, the **Chat Output Path** specifies the context location to output the full message history.
This includes all original entries sent to the model along with all the replies from the model.
For example, the output from the above example is six message entries.

The first three are the ones we sent to the model.

<img src="/img/modelcall-chat-request.png" alt="Request Messages" width="900" style={{ maxWidth: '100%', height: 'auto' }} />

**message[0]** is the system instructions.<br/>
**message[1]** is the user prompt.<br/>
**message[2]** encodes the image as a base64 string, (only 2 data lines shown for brevity).

The next 3 are replies from the model.

<img src="/img/modelcall-chat-output.png" alt="Output Messages" width="900" style={{ maxWidth: '100%', height: 'auto' }} />

**message[3]** requests that two function calls be executed.<br/>
**message[4]** contains the outcome of those calls sent to the model.<br/>
**message[5]** is the final text reply from the model with the requested description.

## Chat Bot

To create a chat bot, build a workflow that takes a list of **ChatMessage** entries as input.
The workflow outputs the updated list, with the model replies appended.
After the workflow completes, present the most recent model response to the user in the format that fits your application.
If the user continues the conversation, add their text as a new message at the end of the list and run the workflow again.
This repeated execution uses the accumulated chat messages to maintain the conversation.
