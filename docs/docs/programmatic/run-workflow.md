---
title: Running Workflows
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

You can get the workflow identifier from the workflow details page.

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

When you are inside an async method, you can start the run and wait for the workflow to finish by using the **StartWorkflowRunAndWait** method.
The workflow will actually execute on background threads, but once it finishes your await will complete.
A **Run** object is always returned that contains a **RunStatus** property.
Examine this to discover the **RunStatus.Success** or **RunStatus.Failed** outcome.

On success the **OutputContext** property has a JSON-serialized string of the finishing context.
To access the contents, use the **Deserialize** helper on **ContextObject**.
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
If you cannot guarantee completion within the timeout period, then this approach will not be reliable.
In that case, consider using the next option.

### Notify result

Instead of waiting for the result, you can start the workflow executing and then return immediately.
In this scenario, completion is reported through the **IEngineNotification** interface that you must implement and register.
Here is the code to start the workflow running.

```csharp
  var engine = serviceProvider.GetRequiredService<IEngineService>();
  var workflowId = await engine.GetWorkflowId("Example Workflow");
  var runId = await engine.CreateWorkflowRun(workflowId);

  await engine.StartWorkflowRunAndNotify(runId);
```

The interface includes workflow completion, evaluation completion, and connection override hooks.

```csharp
  public interface IEngineNotification
  {
    public Task RunCompleted(
        Guid runId, 
        Guid workflowId, 
        RunStatus runStatus, 
        string? outputContext, 
        string? error);

    public Task EvalRunCompleted(
        Guid evalRunId,
        EvalRunStatus runStatus,
        string? error);

    public void ConnectionOverride(
        Guid runId, 
        Guid workflowId, 
        string connectorId, 
        AuthenticationModeConfig authenticationModel, 
        Dictionary<string, string?> parameters);
  }
```

The **RunCompleted** method is invoked each time a **Run** completes with success or failure.
**ConnectionOverride** is called whenever a connection is about to be used.
It allows you to override the connection properties such as the target endpoint and API key.
This makes it easy to implement different values per environment.
For example, your application could retrieve the values from Azure Key Vault.

Here is a simple implementation of the interface to duplicate the previous logic.

```csharp
  public class EngineNotification(IServiceProvider serviceProvider) : IEngineNotification
  {
    public async Task RunCompleted(
        Guid runId, 
        Guid workflowId, 
        RunStatus runStatus, 
        string? outputContext, 
        string? error)
    {
      if (runStatus == RunStatus.Failed)
      {
        // Log or otherwise process a failure as needed
        Console.WriteLine($"Failed with error {error ?? ""}");
      }
      else if (runStatus == RunStatus.Success)
      { 
        // Deserialize needs access to the customized list of json converters
        var jsonConverters = serviceProvider.GetRequiredService<IJsonConverterService>();
        ContextObject context = ContextObject.Deserialize(outputContext, jsonConverters);

        // Perform success actions here
      }        
    }

    public Task EvalRunCompleted(
      Guid evalRunId,
      EvalRunStatus runStatus,
      string? error)
    {
      // Handle evaluation completion if your host uses eval runs
      return Task.CompletedTask;
    }

    public void ConnectionOverride(
        Guid runId, 
        Guid workflowId, 
        string connectorId, 
        AuthenticationModeConfig authenticationModel, 
        Dictionary<string, string?> parameters)
    {
      if (connectorId == "azure_openAI")
      {
        if (authenticationModel.Id == "api_key")
        {
          // Override settings with values from environment or Azure Key Vault etc...
          parameters["endpoint"] = "myEndpoint";
          parameters["api_key"] = "mySecret";
        }
      }
    }
  }
```

### Progress Notifications

If you need to monitor workflow execution at a more granular level, then implement the **IProgressService** interface.

The interface includes workflow run progress, trace progress, and evaluation run progress.

```csharp
  public interface IProgressService
  {
    Task RunProgress(Run run);
    Task TraceProgress(Trace trace);
    Task EvalRunProgress(EvalRun evalRun);
  }
```


The **RunProgress** method is invoked each time a **Run** changes state.
**TraceProgress** is called whenever a new **Trace** record is created or changes its value.
A trace record is used to track the state of an individual node as it is processed.
Here is a simple implementation.

```csharp
  public class ProgressService(IServiceProvider serviceProvider) : IProgressService
  {
    public async Task RunProgress(Run model)
    {
      if (model.RunStatus == RunStatus.Failed)
      {
        // Log or otherwise process a failure
        Console.WriteLine($"Workflow {model.WorkflowId} failed with error {model.Error}");
      }
      else
      { 
        // Log all other state changes
        Console.WriteLine($"Workflow {model.WorkflowId} is now {model.RunStatus}");
      }
    }

    public async Task TraceProgress(Trace model)
    {
        Console.WriteLine($"Workflow {model.WorkflowId} Node {model.NodeEntityId} is now {model.Message}");
    }

    public async Task EvalRunProgress(EvalRun evalRun)
    {
        Console.WriteLine($"Eval run {evalRun.EvalRunId} is now {evalRun.Status}");
    }
  }
```

Note that you can add more than one **IProgressService** implementation if you need to split up functionality, in that case, they are called in sequence.

### Synchronous

This is not a recommended approach, but you can perform a synchronous call and wait for the workflow to finish.
The problem is that a long-running request is going to cause the thread to be suspended until the workflow finishes.
Potentially, this can result in thread starvation if you have many parallel workflows.

```csharp
  var engine = serviceProvider.GetRequiredService<IEngineService>();
  var workflowId = await engine.GetWorkflowId("Example Workflow");
  var runId = await engine.CreateWorkflowRun(workflowId);

  // NOTICE: It does not use await on the call, making it synchronous
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
