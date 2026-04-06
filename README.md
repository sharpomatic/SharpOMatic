[![Development](https://github.com/sharpomatic/SharpOMatic/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/sharpomatic/SharpOMatic/actions/workflows/ci.yml)
[![Prerelease](https://img.shields.io/nuget/vpre/SharpOMatic.Engine?label=prerelease)](https://www.nuget.org/packages/SharpOMatic.Engine/)
[![Stable](https://img.shields.io/nuget/v/SharpOMatic.Engine?label=stable)](https://www.nuget.org/packages/SharpOMatic.Engine/)
![Target Frameworks](https://img.shields.io/badge/Target%20Frameworks-net10.0-512BD4?logo=dotnet)
![Languages](https://img.shields.io/badge/Languages-C%23%20(.NET)%20|%20TypeScript-512BD4?logo=dotnet)
![License](https://img.shields.io/github/license/sharpomatic/Sharp-O-Matic)
[![Downloads](https://img.shields.io/nuget/dt/SharpOMatic.Engine?label=downloads)](https://www.nuget.org/packages/SharpOMatic.Engine/)

# SharpOMatic

SharpOMatic is an open-source workflow builder for AI-heavy .NET applications.
It combines a browser-based editor with a host-it-yourself execution engine so you can design workflows visually, run them inside your own backend, and keep complete control over data, storage, and integrations.

## What SharpOMatic Does

- Build AI workflows with nodes for models, code, branching, batching, gosub calls, and suspend/resume control.
- Run workflows either as one-shot executions or as multi-turn conversations.
- Persist runs, traces, assets, and conversation history for debugging and auditing.
- Call directly into your backend from C# code nodes and expose backend types and methods for structured output and tool calling.
- Configure and run evaluations against datasets with grader workflows.
- Embed the editor into your own ASP.NET Core host instead of relying on a managed SaaS.

<img width="1048" height="481" alt="hero" src="https://github.com/user-attachments/assets/4494018b-90b8-4a8f-a815-93c7afd3e0ee" />

## Why Use It

### Configuration over code

AI systems usually need rapid iteration across prompts, models, tools, and orchestration.
SharpOMatic keeps most of that experimentation in workflow configuration instead of forcing a full code-edit-build-run cycle for every change.

### Host your own execution

The engine runs in your project.
That means workflow state, run history, assets, credentials, and provider access all stay under your control.

### Deep .NET integration

SharpOMatic is .NET-native.
Code nodes use C#, structured output can target your C# types, and tool calling can invoke your existing application services directly.

## Key Features Added Since 10.0.2

- Multi-turn conversation workflows with persisted conversation state.
- Suspend node support for waiting on a later turn and resuming safely.
- Resume options in the editor for continuing a conversation turn or merging JSON context before resume.
- Conversation run history, trace history, and conversation-scoped assets in the workflow UI.
- Programmatic APIs for starting or resuming conversations.
- Evaluation UI filtering so conversation-enabled workflows are not selectable for evaluation execution or graders.

## Project Layout

- Frontend UI: `src/SharpOMatic.FrontEnd`
- Editor host: `src/SharpOMatic.Editor`
- Workflow engine: `src/SharpOMatic.Engine`
- Sqlite repository package: `src/SharpOMatic.Engine.Sqlite`
- SQL Server repository package: `src/SharpOMatic.Engine.SqlServer`
- Demo host: `src/SharpOMatic.DemoServer`
- Documentation site: `docs`

## Documentation

Start here:

- [Getting Started](docs/docs/getting-started/start-with-github-code.md)
- [Core Concepts](docs/docs/core-concepts/workflows.md)
- [Conversations](docs/docs/core-concepts/conversations.md)
- [Evaluations](docs/docs/core-concepts/evaluations.md)
- [Programmatic workflow execution](docs/docs/programmatic/run-workflow.md)

## Affiliation

SharpOMatic is a personal project created and maintained by Phil Wright.
It is not affiliated with, sponsored by, or endorsed by my employer.
All names and trademarks remain the property of their respective owners.
