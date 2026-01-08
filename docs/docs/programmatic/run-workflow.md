---
title: Run Workflow
sidebar_position: 1
---

This section covers how to run workflows programmatically from your own code. 

## Create a new Run

You need a reference to the **IEngineService** interface from your service provider.
Call the **CreateWorkflowRun** method to create a new run instance and get back its run identifier.
At this point the run has not started execution, but it will perform some validation steps.

```csharp
  var engine = serviceProvider.GetRequiredService<IEngineService>();
  var runId = await engine.CreateWorkflowRun(workflowId);
```

You can get the workflow identifier from the details page of the workflow page.

<img src="/img/programmatic_workflowid.png" alt="Custom model setup" width="600" style={{ maxWidth: '100%', height: 'auto' }} />

If you prefer to use workflow names, use the helper method **GetWorkflowId**.
Note that workflow names are not guaranteed to be unique, so you can add multiple workflows and give them the same name.
When there is no match, or more than one match, it will throw an exception.

```csharp
  var engine = serviceProvider.GetRequiredService<IEngineService>();
  var workflowId = await engine.GetWorkflowId("Example Workflow");
  var runId = await engine.CreateWorkflowRun(workflowId);
```

## Start the Run

There are multiple engine methods for starting the workflow, to match different requirements.

### Await result

When inside an async method you can start the run and wait for the workflow to finish by using the **StartWorkflowRunAndWait** method.
The workflow will actually execute on background threads, but once it finishes your await will complete.
A **Run** object is always returned that contains a **RunStatus** property.
Examine this to discover the **RunStatus.Success** or **RunStatus.Failed** outcome.

On success the **OutputContext** property has a JSON serialized string of the finishing context.
To access the contents use the helper method **Deserialize** on the **ContextObject** object.
The example below shows how to perform that conversion.

```csharp
  var engine = serviceProvider.GetRequiredService<IEngineService>();
  var workflowId = await engine.GetWorkflowId("Example Workflow");
  var runId = await engine.CreateWorkflowRun(workflowId);

  var completed = await engine.StartWorkflowRunAndWait(runId);
  if (completed.RunStatus == RunStatus.Failed)
  {
    // Log or otherwise process a failure as needed
    Console.WriteLine($"Failed with error {completed.Error}");
  }
  else if (completed.RunStatus == RunStatus.Success)
  {
    // Deserialize needs access to the customized list of json converters
    var jsonConverters = serviceProvider.GetRequiredService<IJsonConverterService>();
    ContextObject context = ContextObject.Deserialize(completed.OutputContext, jsonConverters);

    // Perform success actions here
  }
```

The advantage of this approach is that it is easy to implement and can be exposed as the logic of a REST endpoint.
As long as your workflow is expected to finish before the timeout of the REST call, it provides a synchronous result to the caller.
If you cannot guarantee completion within the timeout period then this approach will not be reliable.
In that case consider using the next option.

### Notify result

Instead of waiting for the result, you can start the workflow executing and then return immediately.
In this scenario the completion will notify you by using the **IProgressService** that you must implement and register.
Here is the simple code to start the workflow running.

```csharp
  var engine = serviceProvider.GetRequiredService<IEngineService>();
  var workflowId = await engine.GetWorkflowId("Example Workflow");
  var runId = await engine.CreateWorkflowRun(workflowId);

  await engine.StartWorkflowRunAndNotify(runId);
```

The interface you need to implement only has 2 methods.

```csharp
  public interface IProgressService
  {
    Task RunProgress(Run run);
    Task TraceProgress(Trace trace);
  }
```

The **RunProgress** method is invoked each time a **Run** changes state.
**TraceProgress** is called whenever a new **Trace** record is created or changes value.
A trace record is used to track the state of an individual node as it is processed.
Here is a simple implementation of the interface to duplicate the previous logic.

```csharp
  public class ProgressService(IServiceProvider serviceProvider) : IProgressService
  {
    public async Task RunProgress(Run model)
    {
      if (model.RunStatus == RunStatus.Failed)
      {
        // Log or otherwise process a failure as needed
        Console.WriteLine($"Failed with error {model.Error}");
      }
      else if (model.RunStatus == RunStatus.Success)
      { 
        // Deserialize needs access to the customized list of json converters
        var jsonConverters = serviceProvider.GetRequiredService<IJsonConverterService>();
        ContextObject context = ContextObject.Deserialize(model.OutputContext, jsonConverters);

        // Perform success actions here
      }        
    }

    public async Task TraceProgress(Trace model)
    {
    }
  }
```

Note that you can add more than one **IProgressService** implementation if you need to split up functionality, in that case they are called in sequence.
There are multiple advantages to processing results using the interface based approach.
You can centralize processing for all your different workflows into a single location.
Long-running workflow runs do not time out the caller's HTTP request; they complete very quickly because they only need to start the workflow.
Once it completes in the future you process the result and then use whatever mechanism you choose to notify the client.

Some examples include:

- Use WebSockets (SignalR) for an immediate notification
- Record outcome in a database and wait for the user to request it
- Send an email message with result details
- Queue a processing message to an event queue

### Synchronous

This is not a recommended approach, but you can perform a synchronous call and wait for the workflow to finish.
The problem is that a long-running request is going to cause the thread to be suspended until the workflow finishes.
Potentially this can result in thread starvation if you have many parallel workflows.

```csharp
  var engine = serviceProvider.GetRequiredService<IEngineService>();
  var workflowId = await engine.GetWorkflowId("Example Workflow");
  var runId = await engine.CreateWorkflowRun(workflowId);

  // NOTICE: It does not use AWAIT on the call making it synchronous
  var completed = engine.StartWorkflowRunSynchronously(runId);
  if (completed.RunStatus == RunStatus.Failed)
  {
    // Log or otherwise process a failure as needed
    Console.WriteLine($"Failed with error {completed.Error}");
  }
  else if (completed.RunStatus == RunStatus.Success)
  {
    // Deserialize needs access to the customized list of json converters
    var jsonConverters = serviceProvider.GetRequiredService<IJsonConverterService>();
    ContextObject context = ContextObject.Deserialize(completed.OutputContext, jsonConverters);

    // Perform success actions here
  }
```

## Initializing the context

Most real-world workflows are going to have inputs that are used to parameterize the workflow operation.
All of the above methods for starting a workflow have a second parameter of type **ContextObject**.
This is the initial context instance that will be given to the start node.
If you do not provide the second parameter it will default to an empty instance.

How to provide a context:

```csharp
  var engine = serviceProvider.GetRequiredService<IEngineService>();
  var workflowId = await engine.GetWorkflowId("Example Workflow");
  var runId = await engine.CreateWorkflowRun(workflowId);

  var context = new ContextObject();
  context.Set<string>("input.prompt", "What is the capital of Brazil?");
  context.Set<int>("input.user_id", 42);
  context.Set<DateTimeOffset>("input.now", DateTimeOffset.Now);

  var completed = await engine.StartWorkflowRunAndWait(runId, context);
  ```

  For more detailed information about contexts, see the [Context](../core-concepts/context.md) section.
