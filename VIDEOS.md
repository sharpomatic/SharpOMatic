# SharpOMatic Video Plan

This document outlines the first 11 evergreen YouTube videos for promoting SharpOMatic to C# and .NET developers building AI features. Each section includes title options, a short description suitable for YouTube, and a screen-first talk track where each bullet is intended to cover roughly 10 to 30 seconds.

## 1. Why AI Features Feel Slow in .NET Projects

**Possible titles**

- Why AI Features Feel So Slow in .NET
- The Real Bottleneck in .NET AI Development
- Why Shipping AI in ASP.NET Core Takes Too Long

**YouTube description**

Building AI features in .NET often feels slower than it should. This video breaks down why prompt edits, model swaps, JSON parsing, and orchestration logic create friction, and how SharpOMatic changes the iteration cycle for ASP.NET Core teams.

**Video beats**

- Show: Fast montage of prompt text, C# service classes, JSON parsing, and failed runs. Talk: The pain is not calling the model once; the pain is changing the workflow every day without turning your codebase into a mess.
- Show: A typical `AiService.cs` file with prompt strings and response parsing. Talk: Point out how prompt logic, model settings, tool definitions, and glue code are often packed into one class.
- Show: Highlight a small prompt tweak and then a rebuild/restart loop. Talk: Explain how even tiny experiments create full code-test-redeploy friction.
- Show: Side-by-side of "change prompt in code" versus "change workflow configuration". Talk: Frame the central idea of configuration over code.
- Show: SharpOMatic editor home page and workflow canvas. Talk: Introduce SharpOMatic as a .NET-native workflow builder for AI-driven tasks.
- Show: A simple workflow with Start, ModelCall, Code, and End nodes. Talk: Explain that the workflow becomes the orchestration surface instead of custom plumbing code.
- Show: Run history and trace view in the editor. Talk: Emphasize debugging visibility and persisted run state as a practical production benefit.
- Show: A code node example calling backend code. Talk: Make clear this is not about replacing .NET code, but about moving orchestration and experimentation out of hardcoded classes.
- Show: Connectors and model configuration screens. Talk: Explain that swapping model settings becomes a configuration change instead of a code refactor.
- Show: Evaluation results screen or doc snippet. Talk: Briefly preview that serious AI systems need evaluations, not just manual spot checks.
- Show: Return to the workflow canvas with a clean visual zoom. Talk: Summarize the value proposition as faster iteration, clearer orchestration, and better operational control.
- Show: End card with next video teaser. Talk: Set up the next video around adding SharpOMatic to an existing ASP.NET Core app.

## 2. Build an AI Workflow in ASP.NET Core Without Leaving .NET

**Possible titles**

- Add AI Workflows to ASP.NET Core in 10 Minutes
- Build an AI Workflow in .NET Without a Separate Platform
- Embed a Visual AI Workflow Editor in Your ASP.NET Core App

**YouTube description**

This video shows how to add SharpOMatic to an existing ASP.NET Core project using NuGet packages, map the editor, and run your first AI workflow. It is a practical introduction for .NET developers who want AI orchestration without leaving their own stack.

**Video beats**

- Show: A blank or simple ASP.NET Core project in the IDE. Talk: Frame the goal as adding AI workflow capability to a normal .NET app, not starting from a separate platform.
- Show: The NuGet package names from the docs. Talk: Walk through `SharpOMatic.Engine`, `SharpOMatic.Engine.Sqlite`, and `SharpOMatic.Editor`.
- Show: Terminal running `dotnet add package` commands. Talk: Keep the setup fast and practical.
- Show: `Program.cs` before changes. Talk: Explain the baseline so viewers can see exactly what is being added.
- Show: Add service registration lines for asset storage, editor, transfer, and engine. Talk: Describe what each registration does in plain language.
- Show: Add the `MapSharpOMaticEditor("/editor")` call. Talk: Explain that one route exposes the browser-based workflow editor.
- Show: Run the app and open `/editor` in a browser. Talk: Highlight that the editor is hosted inside the developer's own app.
- Show: Create a minimal workflow on the canvas with Start, ModelCall, and End. Talk: Explain the basic flow and why it is a useful first example.
- Show: Configure a connector and model instance in the UI. Talk: Explain the separation between provider credentials, model definitions, and workflow logic.
- Show: Start a workflow run and inspect the output. Talk: Emphasize that this is already persisted and observable, not just console output.
- Show: Brief look at the generated SQLite file location from the docs. Talk: Explain local persistence and why it is useful for developer workflows.
- Show: End with the editor and IDE side by side. Talk: Reinforce that the whole experience stays inside the .NET ecosystem.

## 3. Download SharpOMatic and Run It Right Away

**Possible titles**

- Run SharpOMatic From GitHub in 10 Minutes
- Fastest Way to Try SharpOMatic on Your Machine
- Clone, Run, and Test SharpOMatic With a Sample Workflow

**YouTube description**

If you want to try SharpOMatic without integrating it into your own app first, this video shows the fastest path. Clone the repo, run the demo server, open the editor, load a sample workflow, and confirm everything is working end to end.

**Video beats**

- Show: GitHub repo homepage and the docs navigation. Talk: Frame this as the quickest way for curious .NET developers to see the product working before making architecture decisions.
- Show: The getting started docs for using the GitHub code. Talk: Explain that this path is for evaluation and fast local experimentation.
- Show: Clone or download the repository in the terminal. Talk: Keep the first step very concrete and low friction.
- Show: Open the solution and point out the DemoServer project. Talk: Explain that the demo host wires the engine and editor together for immediate local use.
- Show: Restore and run the demo server from the IDE or terminal. Talk: Mention that this is the fastest way to get the editor running without embedding it into an existing app.
- Show: Browser opening the local editor URL. Talk: Confirm that the UI loads and explain that this is the first proof the environment is working.
- Show: Workflow page and the Samples menu. Talk: Explain that sample workflows are built in specifically to help users explore features quickly.
- Show: Create a workflow from a sample. Talk: Pick one that is visually clear and easy to explain, ideally one with a small number of nodes.
- Show: Connector and model setup if the sample needs a model. Talk: Explain the minimum config required to make an AI sample run.
- Show: Run the sample workflow and wait for completion. Talk: Explain that the goal here is not deep understanding yet, just proving the full stack works on the developer's machine.
- Show: Output context, run history, and traces. Talk: Explain what success looks like and where to inspect failures if something goes wrong.
- Show: A quick troubleshooting checklist on screen. Talk: Mention common issues like missing model configuration, wrong startup project, or using the wrong local URL.
- Show: End with the sample workflow on screen and the docs sidebar visible. Talk: Point viewers to the next step, either embedding SharpOMatic into their own ASP.NET Core app or exploring more sample workflows.

## 4. Stop Hardcoding Prompts in C#

**Possible titles**

- Stop Hardcoding AI Prompts in Your C# Services
- A Better Pattern Than Prompt Strings in C#
- The .NET AI Architecture Mistake Most Teams Make

**YouTube description**

Many .NET teams start AI work by dropping prompt strings into service classes. This video shows why that becomes hard to maintain and how a workflow-based approach makes prompt changes, branching, and model swaps much easier to manage.

**Video beats**

- Show: A C# class with multiple prompt constants and `switch` statements. Talk: Explain how AI code often starts simple and then grows into a tangled service.
- Show: Zoom in on prompt text mixed with retry logic and JSON parsing. Talk: Point out the architectural smell of mixing instructions, orchestration, and infrastructure in one place.
- Show: Git diff with tiny prompt edits causing code changes. Talk: Explain how non-engineering content becomes engineering churn.
- Show: Workflow canvas version of the same process. Talk: Introduce the idea that prompts and routing belong in a workflow model.
- Show: A ModelCall node prompt configuration. Talk: Show how prompt edits become targeted configuration changes.
- Show: A Switch node after a model output. Talk: Explain that decision routing can be visual and explicit instead of hidden in nested C# conditions.
- Show: An Edit node updating context fields. Talk: Show how small transformations can be done without writing another helper method.
- Show: A Code node only where actual backend logic is needed. Talk: Clarify that C# still matters, but only for the parts that truly need code.
- Show: Duplicate the workflow and swap a model instance. Talk: Show how experimentation becomes safer and faster.
- Show: Run history comparing two workflow versions. Talk: Explain that this is much more reviewable than scanning diffs across multiple classes.
- Show: Return to the original service class and compare complexity. Talk: Make the before-and-after contrast explicit.
- Show: End card with a clean workflow screenshot. Talk: Tease the next video on calling existing backend services from a workflow.

## 5. How to Call Your Existing C# Services From an AI Workflow

**Possible titles**

- Call Your Existing C# Services From an AI Workflow
- Use SharpOMatic Without Rewriting Your Backend
- AI Workflows That Plug Into Real .NET Code

**YouTube description**

SharpOMatic is not a separate no-code island. In this video, you will see how code nodes can call your existing services, repositories, and domain logic so AI workflows become part of your real ASP.NET Core backend.

**Video beats**

- Show: Architecture slide with ASP.NET Core app, SharpOMatic, and existing services. Talk: Explain the concern many developers have about workflow tools living outside the real codebase.
- Show: Open the code node docs or a code node example. Talk: Introduce the `Context` global and the ability to access backend services.
- Show: A sample backend service interface in C#. Talk: Pick a realistic example such as customer lookup, ticket retrieval, or order history.
- Show: A code node script using `ServiceProvider.GetRequiredService<T>()`. Talk: Explain how workflow steps can call normal DI-registered services.
- Show: Read values from `Context` like `input.userId`. Talk: Explain how workflow input parameters flow into backend logic.
- Show: Write the service result back into `Context`. Talk: Show how downstream nodes can use enriched data without custom orchestration code.
- Show: Add `AddScriptOptions` in `Program.cs`. Talk: Explain why extra assemblies and namespaces may need to be exposed to scripts.
- Show: Run the workflow and inspect the output context. Talk: Prove that backend data and model output can live together in one run state.
- Show: Trace view for the code node. Talk: Emphasize debugging value when a backend call fails or returns unexpected data.
- Show: Compare this with a hand-built orchestration service in C#. Talk: Explain how SharpOMatic removes repetitive plumbing while keeping real code where it belongs.
- Show: Another short example, maybe calling a pricing or search service. Talk: Reinforce that this works for practical business scenarios, not just demos.
- Show: End with the workflow and service code side by side. Talk: Summarize the message as reuse, not rewrite.

## 6. Structured Output for LLMs Using Real C# Types

**Possible titles**

- Structured Output for LLMs With C# Types
- Stop Fighting JSON From AI Responses in .NET
- Make LLM Output Fit Your C# Models

**YouTube description**

One of the hardest parts of building AI features in .NET is turning model output into something reliable. This video shows how SharpOMatic can use structured output and C# types to make AI responses easier to consume safely in backend code.

**Video beats**

- Show: A messy AI text response and a failing JSON parse. Talk: Introduce the classic reliability problem with free-form LLM output.
- Show: A C# DTO or record type that the app really wants. Talk: Explain the goal of mapping AI output into familiar .NET shapes.
- Show: Structured output tab in the ModelCall docs or editor. Talk: Introduce the available output modes: text, JSON, schema, and configured type.
- Show: The C# type registration code using `AddSchemaTypes`. Talk: Explain how approved types are made available to workflows.
- Show: Select a configured type in the workflow UI. Talk: Show that the workflow can drive model output toward the desired shape.
- Show: A before-and-after comparison of manual parsing versus configured schema output. Talk: Explain why this reduces fragile parsing code.
- Show: Run the workflow and inspect the structured result in context. Talk: Highlight how downstream nodes get cleaner data.
- Show: A code node or host code reading strongly shaped values. Talk: Connect the workflow output back to practical application code.
- Show: Briefly mention schema constraints and model capability requirements. Talk: Set realistic expectations about provider support.
- Show: Example use cases like classification, extraction, routing, and enrichment. Talk: Tie the feature to real-world .NET backend needs.
- Show: Return to the failing free-form example. Talk: Explain that the value is not just convenience; it is predictability and maintainability.
- Show: End card with a type definition and workflow output side by side. Talk: Tease the next video on tool calling with C# methods.

## 7. Tool Calling for .NET Developers

**Possible titles**

- Tool Calling in .NET With Real C# Methods
- Let the Model Call Your C# Functions
- How Tool Calling Actually Fits Into ASP.NET Core

**YouTube description**

Tool calling gets much easier to understand when you see it using real C# methods and dependency injection. This video shows how SharpOMatic exposes backend functions to a model and where that pattern fits into production-oriented .NET applications.

**Video beats**

- Show: High-level diagram of user input, model, tools, and backend services. Talk: Explain tool calling in plain language before diving into code.
- Show: A static C# tool method with a `Description` attribute. Talk: Explain how the model needs a clear tool name and purpose.
- Show: The `AddToolMethods` registration in `Program.cs`. Talk: Show how approved functions are explicitly exposed.
- Show: Tool method receiving `IServiceProvider`. Talk: Explain how the method can access context and normal backend services.
- Show: The tool writing values into the `ContextObject`. Talk: Emphasize that tool execution can both return data and update workflow state.
- Show: A ModelCall node with the tool-calling tab and selected tools. Talk: Explain per-call control over which tools are available.
- Show: The tool choice setting in the UI. Talk: Clarify the difference between planning with tools visible and actually allowing execution.
- Show: A workflow run where the model triggers a tool. Talk: Step through the trace or output to show the round trip.
- Show: A practical example like checking account data or looking up a tenant plan. Talk: Tie tool calling to backend business logic that .NET teams already own.
- Show: Parallel tool calling option. Talk: Briefly explain why this can reduce latency when supported by the provider.
- Show: A caution screen listing safe boundaries. Talk: Explain that tool exposure should be deliberate and minimal, especially around sensitive operations.
- Show: End with the model node and tool method side by side. Talk: Summarize tool calling as a bridge between LLM reasoning and real application capabilities.

## 8. How to Evaluate AI Workflows Before They Break Production

**Possible titles**

- How to Evaluate AI Workflows in .NET
- Stop Shipping Prompt Changes Without Eval Runs
- Catch AI Regressions Before Production

**YouTube description**

If your team changes prompts, models, or tools without evaluations, you are guessing. This video shows how SharpOMatic evaluations, grader workflows, and score tracking help .NET teams make AI changes with more confidence.

**Video beats**

- Show: A dramatic compare of two outputs where one looks better and one secretly regresses. Talk: Explain why eyeballing AI results is not enough.
- Show: The evaluations docs or evaluations page. Talk: Define evaluations as a repeatable test harness for workflows.
- Show: Columns and rows for an evaluation dataset. Talk: Explain how test cases are modeled and why structured inputs matter.
- Show: A workflow under test and a separate grader workflow. Talk: Explain the main idea of using workflows to score workflows.
- Show: The `score` output contract in the docs. Talk: Make the grader contract concrete and easy to remember.
- Show: Start an evaluation run with sample count options. Talk: Explain fast sample runs versus full dataset runs.
- Show: Results screen with pass rates or average scores. Talk: Emphasize trend visibility and regression detection.
- Show: Row-level details for weak cases. Talk: Explain how evaluations are also a debugging tool, not just a dashboard.
- Show: A prompt edit followed by another evaluation run. Talk: Demonstrate the feedback loop that makes configuration over code even more valuable.
- Show: A model swap and compare results. Talk: Reinforce that evaluations make provider and prompt experimentation safer.
- Show: A release flow diagram with dev, staging, and production. Talk: Explain where evals fit into real promotion decisions.
- Show: End card with "No evals, no confidence." Talk: Set up the next orchestration-focused video.

## 9. Build Multi-Step AI Pipelines With Parallel Execution

**Possible titles**

- Build Parallel AI Pipelines in .NET
- Fan-Out, Batch, and Merge AI Workflows in C#
- Process More AI Work With Less Glue Code

**YouTube description**

Many AI features are really multi-step pipelines: chunking data, running parallel work, and merging results. This video shows how SharpOMatic handles fan-out, fan-in, and batch processing for .NET developers building more advanced AI workflows.

**Video beats**

- Show: Example problem like summarizing 100 support tickets or processing many documents. Talk: Introduce the need for orchestration beyond a single model call.
- Show: A manual C# approach with loops, tasks, and merge logic. Talk: Explain how orchestration code becomes noisy and hard to debug.
- Show: A workflow with Batch, ModelCall, and End nodes. Talk: Introduce the Batch node as a cleaner execution pattern for list-based work.
- Show: Batch settings like input path, output path, batch size, and parallel batches. Talk: Explain what each setting controls.
- Show: FanOut and FanIn node docs or a sample workflow. Talk: Explain parallel branches for multiple strategies or tasks.
- Show: Context diagrams for cloned branch state. Talk: Explain how branch isolation works and why only `output` is merged at FanIn.
- Show: Run traces with thread IDs. Talk: Highlight the debugging visibility that is usually missing from hand-built parallel pipelines.
- Show: A concrete workflow example like classify, summarize, and extract in parallel. Talk: Make the pattern practical and memorable.
- Show: Final merged context after FanIn. Talk: Show how outputs come back together without custom merge code.
- Show: A quick warning about when not to parallelize. Talk: Mention provider limits, cost, and cases where sequence matters.
- Show: Compare the workflow canvas with the manual orchestration code again. Talk: Stress readability and maintainability.
- Show: End card with a child workflow icon. Talk: Tease reuse through gosub workflows in the next content or later in the series.

## 10. Reuse AI Logic Instead of Copy/Pasting It Everywhere

**Possible titles**

- Reuse AI Workflows Instead of Copy/Pasting Prompts
- Build Reusable AI Logic in .NET
- How to Share AI Workflow Logic Across Features

**YouTube description**

As AI features expand, teams often duplicate prompt logic, tool setup, and routing across services. This video shows how SharpOMatic uses child workflows and transfer packages to help .NET teams reuse and promote AI logic more cleanly.

**Video beats**

- Show: Two or three similar service classes with duplicated prompt logic. Talk: Explain how copy/paste spreads quickly once multiple features need similar AI behavior.
- Show: A repeated workflow fragment on the canvas. Talk: Translate the same duplication problem into visual workflow terms.
- Show: Gosub node docs or example workflow. Talk: Introduce child workflows as a reuse mechanism.
- Show: Parent workflow calling a child workflow. Talk: Explain how shared logic can be extracted once and called from multiple places.
- Show: Input mapping settings on Gosub. Talk: Explain controlled data flow into the child workflow.
- Show: Output mapping or merge behavior. Talk: Show how results come back into the parent workflow without manual wiring code.
- Show: Child traces linked to the parent run. Talk: Highlight that reuse does not mean losing debugging visibility.
- Show: A real example like a shared "answer with citations" or "classify intent" child workflow. Talk: Suggest a pattern viewers can imagine in their own systems.
- Show: The transfer page for export/import. Talk: Expand the reuse story from within one app to across environments.
- Show: Export package contents and import behavior. Talk: Explain how workflows, models, connectors, and assets can move through release stages.
- Show: A dev-to-staging-to-production diagram. Talk: Explain the operational value of promotion instead of rebuilding logic by hand.
- Show: End with before-and-after architecture. Talk: Summarize the value as less duplication, cleaner upgrades, and more consistent AI behavior.

## 11. Self-Host AI Workflows and Keep Control in Your .NET App

**Possible titles**

- Self-Host AI Workflows in ASP.NET Core
- Keep AI Execution Inside Your .NET Backend
- The Case for Self-Hosted AI Orchestration in .NET

**YouTube description**

Many teams want AI features without giving away operational control. This video explains SharpOMatic's self-hosted model, where execution runs in your own ASP.NET Core app, data stays in your environment, and you still get modern workflow orchestration for AI use cases.

**Video beats**

- Show: Architecture slide comparing hosted external workflow platform versus self-hosted engine in your app. Talk: Frame the tradeoff around control, compliance, and integration.
- Show: SharpOMatic docs highlighting hosted execution in your project. Talk: Explain that the engine lives inside the developer's own backend.
- Show: `Program.cs` with engine and editor registration. Talk: Show that self-hosting is not a giant infrastructure project; it is a normal .NET integration.
- Show: SQLite and SQL Server repository options from the docs. Talk: Explain the persistence choices and why they matter for different environments.
- Show: Run history and traces in the editor. Talk: Emphasize that visibility comes with self-hosting; you do not lose operational insight.
- Show: Connector setup for OpenAI, Azure OpenAI, and Google AI. Talk: Explain that provider choice stays flexible while execution stays local to your app.
- Show: The `ConnectionOverride` interface from the docs. Talk: Explain how secrets and environment-specific connector values can be managed in production.
- Show: Code node calling backend logic and tool methods using DI. Talk: Reinforce how self-hosting and deep integration fit together.
- Show: A slide with concerns like data locality, scaling, uptime, and access control. Talk: Explain why many .NET teams prefer this model over outsourcing orchestration.
- Show: A realistic enterprise-ish app diagram with existing APIs and databases. Talk: Help viewers imagine SharpOMatic as part of a normal internal architecture.
- Show: Return to the editor and a completed workflow run. Talk: Make the experience feel tangible, not abstract.
- Show: Final series summary screen with all 11 topics. Talk: Position the channel as a resource for practical, production-minded .NET AI development.
