# Repository Project

SharpOMatic is an open-source project on GitHub that allows a user to build and execute workflows with an emphasis on AI-related tasks. It has deep integration with .NET by allowing users to add C# code snippets and call directly into backend code.

The frontend is an Angular and TypeScript browser-based application.
The backend is .NET/C#-based and consists of Engine, Editor, AGUI, DemoServer, and provider packages for SQLite and SQL Server.
The 'docs' directory defines the static website and uses Docusaurus.

## Project Structure & Module Organization
- `src/SharpOMatic.sln` is the .NET solution used by Visual Studio and CLI builds to load the backend projects (Engine, Editor, AGUI, DemoServer, SQLite/SQL Server providers, tests) plus solution items like `DEV.md` and `TODO.md`. The Angular frontend is not included as a project in the solution; instead, the Editor project runs the frontend build and embeds the output during its own build.

- `src/SharpOMatic.FrontEnd/` is the Angular + TypeScript SPA for the workflow designer/editor UI, including pages, components, and services that call the backend APIs and SignalR endpoints. Its production build output (`dist/SharpOMatic-Editor/browser`) is consumed by the Editor project and embedded into the .NET package, so UI changes here affect the hosted editor experience.

- `src/SharpOMatic.Editor/` is a .NET 10 class library that hosts the editor UI and supporting endpoints; it embeds the built Angular assets as resources and exposes ASP.NET controllers and SignalR hubs for the editor and transfer flows. It depends on the Engine project and provides extension methods to plug the editor into an ASP.NET Core app, so changes here affect server-side hosting, routing, and API contracts for the UI.

- `src/SharpOMatic.Engine/` is the .NET 10 core workflow engine that defines the runtime model (nodes, contexts, metadata), persistence (EF Core models/migrations), and services used to execute workflows and manage assets, runs, and repository state. It is the main backend library consumed by both the Editor package and the Server host, so changes here typically impact execution behavior and API DTOs.

- `src/SharpOMatic.AGUI/` is a .NET 10 library that exposes AG-UI compatible endpoints and services for stateful agent/chat integrations. It depends on the Engine project and shares workflow run, conversation, stream event, and history-building behavior with the core runtime, so changes here should be kept aligned with AG-UI docs and tests.

- `src/SharpOMatic.Engine.Sqlite/` and `src/SharpOMatic.Engine.SqlServer/` are provider packages for EF Core database configuration and migrations. Schema-affecting repository/entity changes usually need matching migrations and model snapshots in both provider projects.

- `src/SharpOMatic.DemoServer/` is a .NET 10 ASP.NET Core host application that wires Engine and Editor together for local running, testing, and sample configuration (routing, database setup, asset storage). It serves as the integration harness where you can validate end-to-end editor + engine behavior before packaging or deploying elsewhere. This is used during development to test changes, but end users are expected to create their own server or integrate the Editor and Engine into their own existing project.

- `src/SharpOMatic.Tests/` contains xUnit tests for engine workflow behavior, editor/AG-UI behavior, and service-level behavior such as transfer import/export. It is the primary regression suite for backend changes.

### Project Structure Engine

- `SharpOMatic.Engine/bin`
    Build outputs produced by local or CI builds (compiled assemblies, temporary artifacts). These files are generated and should not be edited; delete the folder if you need a clean rebuild.

- `SharpOMatic.Engine/Contexts`
    Runtime context data structures (ContextObject/ContextList) plus RunContext/ThreadContext that carry state through workflow execution. Values must be JSON-serializable to persist runs, and custom converters are registered through the JSON conversion services.

- `SharpOMatic.Engine/DTO`
    Request/response payload models used by editor and transfer APIs (typically Request/Result naming). These DTOs are used for API-specific contracts that are not represented by core entities or metadata.

- `SharpOMatic.Engine/Entities`
    Domain entities for workflows, nodes, and supporting data, including versioned configuration that is persisted to the repository. Editing these models usually affects serialization, migrations, and runtime behavior.

- `SharpOMatic.Engine/Enumerations`
    Shared enums used across multiple engine areas (runs, nodes, metadata, services), keeping cross-cutting state flags centralized.

- `SharpOMatic.Engine/Exceptions`
    Engine-specific exception types that provide domain context for validation, parsing, and execution failures; these surface to API responses and logs.

- `SharpOMatic.Engine/FastSerializer`
    Custom JSON tokenizer/deserializer used for fast, location-aware parsing of context data and other engine JSON, which is critical for precise error reporting in user-authored inputs.

- `SharpOMatic.Engine/Helpers`
    Utility classes, validators, and extension methods shared across the engine. Place cross-cutting helpers here when multiple modules depend on the same logic.

- `SharpOMatic.Engine/Interfaces`
    DI contracts and abstractions for engine services (execution, repository, converters, schemas, queues). New services should define their interface here to keep boundaries testable.

- `SharpOMatic.Engine/Metadata`
    Connector and model metadata definitions (plus embedded JSON resources) that describe configurable integrations like OpenAI/Azure models. These definitions drive editor UI fields and runtime validation for connectors/models.

- `SharpOMatic.Engine.Sqlite/Migrations` and `SharpOMatic.Engine.SqlServer/Migrations`
    EF Core migrations and snapshots for the engine database schema (workflows, runs, assets, evaluations). Any entity changes that affect persistence need matching migrations for both providers.

- `SharpOMatic.Engine/Nodes`
    Workflow node runtime implementations and validation logic (start/end, switch, fan-in/out, code/model call, etc.). This is where execution semantics live, including Roslyn-backed C# scripting for user code nodes.

- `SharpOMatic.Engine/obj`
    Intermediate build outputs and generated files used by the compiler and tooling. Treat as generated artifacts; safe to delete for clean builds.

- `SharpOMatic.Engine/Repository`
    EF Core DbContext, entity configurations, and repository implementations used by services to read/write workflow and run data.

- `SharpOMatic.Engine/Samples`
    Contains sample workflows that are then exposed by the editor so that users can quickly create a new workflow from a sample.

- `SharpOMatic.Engine/Services`
    Core DI services for execution, queues, asset storage, repository access, JSON conversion, and configuration. This includes the node execution background service and queue logic that drive runtime workflow processing.

- `SharpOMatic.Engine/GlobalUsings.cs`
    All required C# using statements are placed here as global using statements. This prevents the need for source files to have using statements which is the preferred method required for this project.

### Project Structure Editor

- `SharpOMatic.Editor/bin`
    Build outputs produced by local or CI builds (compiled assemblies, temporary artifacts). These files are generated and should not be edited; delete the folder if you need a clean rebuild.

- `SharpOMatic.Editor/Controllers`
    ASP.NET Core API controllers that expose editor and asset endpoints, backing the Angular UI and transfer flows. Changes here affect HTTP routing, request validation, and the contracts the frontend relies on.

- `SharpOMatic.Editor/DTO`
    Request/response payload models used by editor-specific endpoints. Keep these aligned with the frontend DTOs when changing API contracts. Ones placed here are required for communication with the Editor only. The Engine has DTO classes when the data needs to be forward from the controllers into the Engine itself.

- `SharpOMatic.Editor/Helpers`
    Utility classes and helpers used by the editor host.

- `SharpOMatic.Editor/obj`
    Intermediate build outputs and generated files used by the compiler and tooling. Treat as generated artifacts; safe to delete for clean builds.

- `SharpOMatic.Editor/Services`
    Services that encapsulate editor host behavior (asset transfer, hub integration, and editor setup). This is the DI boundary for editor-specific logic that can be reused by host applications.

- `SharpOMatic.Editor/GlobalUsings.cs`
    All required C# using statements are placed here as global using statements. This prevents the need for source files to have using statements which is the preferred method required for this project.

- `SharpOMatic.Editor/`
    Additional files are placed in the root directory of the Editor. These are relating to making us of the Editor in client projects.

### Project Structure AGUI

- `SharpOMatic.AGUI/Controllers`
    ASP.NET Core controllers that expose AG-UI endpoints for message/run flows. Changes here affect external AG-UI clients and should include endpoint tests and docs updates.

- `SharpOMatic.AGUI/DTO`
    AG-UI request/response payload models. Keep these stable and documented because they are used by external client integrations.

- `SharpOMatic.AGUI/Services`
    AG-UI service helpers such as message history builders and response shaping. Changes here should preserve portable chat history and stream event semantics.

- `SharpOMatic.AGUI/README.md`
    Package-level AG-UI usage documentation. Keep it aligned with `docs/docs/ag-ui/`.

### Project Structure FrontEnd

- `SharpOMatic.FrontEnd/src/app/components`
    Reusable UI components used by pages and dialogs (designer canvas, viewers, tabs, shared form widgets). Start here when you need a visual element used across multiple screens.

- `SharpOMatic.FrontEnd/src/app/dialogs`
    Modal dialog components for editing node properties, confirmations, and blocking info. These are typically opened from pages/components and often map to backend validation flows.

- `SharpOMatic.FrontEnd/src/app/dto`
    Client-side DTOs that mirror backend request/response payloads for editor and transfer APIs. Changes here should stay aligned with `SharpOMatic.Engine/DTO` and/or `SharpOMatic.Editor/DTO` as appropriate.

- `SharpOMatic.FrontEnd/src/app/entities`
    Client-side workflow entities and helpers for building or mutating node graphs in the UI. This mirrors engine entities but adds UI-specific conveniences and factories.

- `SharpOMatic.FrontEnd/src/app/enumerations`
    UI enums for node status, run status, and view state flags referenced by templates and services across the app.

- `SharpOMatic.FrontEnd/src/app/helper`
    UI helper utilities (formatters, mappers, or small shared functions) used across components and services.

- `SharpOMatic.FrontEnd/src/app/metadata`
    Connector/model metadata definitions and helpers used to render dynamic forms for LLM integrations and validate UI inputs before sending to the backend.

- `SharpOMatic.FrontEnd/src/app/pages`
    Routed feature pages (workflow, connectors, models, assets, transfers, settings). Page components own layout and coordinate services and dialogs.

- `SharpOMatic.FrontEnd/src/app/services`
    Angular services for API access, SignalR hubs, metadata loading, Monaco integration, settings persistence, and notifications. Any API changes usually flow through these services.

- Notes: The UI uses Monaco for JSON/C# editing and relies on backend validation; keep DTOs and metadata in sync with engine changes to avoid runtime errors.

### Project Structure Tests

- `SharpOMatic.Tests/Workflows`
    Unit and integration-style tests for node runtime behavior, workflow execution, streaming, conversations, tool calls, and model-call behavior. New or changed nodes should normally add focused coverage here.

- `SharpOMatic.Tests/Services`
    Service-level tests for shared services such as transfer import/export and editor progress forwarding.

- `SharpOMatic.Tests/AgUi`
    Tests for AG-UI controllers, history building, and chat-history integration behavior.

- `SharpOMatic.Tests/Services/TestRepositoryService.cs`
    In-memory repository test double used by workflow tests. If `IRepositoryService` changes, keep this implementation compiling even when a method only throws `NotImplementedException`.

## Feature Implementation Notes
- New workflow nodes usually require coordinated changes across Engine entity/enums/runtime node, Angular entity/enums/node factory/designer/dialog wiring, docs under `docs/docs/nodes/`, and focused tests in `SharpOMatic.Tests/Workflows`.
- Transfer exports/imports workflows, connectors, models, library asset folders/assets, evaluations, and terminal evaluation run results. Running evaluation runs are intentionally skipped during transfer because they cannot be resumed in the target instance. Transfer behavior is covered by `SharpOMatic.Tests/Services/TransferServiceUnitTests.cs`.
- Model call node `selected_tools` values are host-dependent because tools come from `AddToolMethods(...)`. At runtime, missing selected tools are ignored and registered tools are still used, so imported workflows can run in hosts that only define a subset of the original tools.
- AG-UI history/replay behavior depends on repository conversation/run/stream-event queries and portable chat message handling. Keep `docs/docs/ag-ui/`, `src/client_samples/sharpy/`, and AG-UI tests aligned when changing those contracts.

## Coding Style & Naming Conventions
- C#: 4-space indentation; nullable is enabled; follow .NET conventions (PascalCase types/methods/properties, camelCase locals/parameters, `I` prefix for interfaces).
- Angular/TypeScript: 2-space indentation and single quotes for `.ts` per `SharpOMatic.FrontEnd/.editorconfig`; HTML is formatted via Prettier.
- File naming: Angular uses kebab-case (`my-widget.component.ts`); there are no front end tests; C# test files use descriptive `*UnitTest(s).cs`, `*IntegrationTests.cs`, or similarly explicit `*Tests.cs` names.

## Build & Test Commands
- `dotnet test src/SharpOMatic.Tests/SharpOMatic.Tests.csproj --no-restore` runs the backend test suite. Because the tests reference the Editor project, this also runs the Angular production build as part of the project build.
- For focused backend checks, use xUnit filters such as `dotnet test src/SharpOMatic.Tests/SharpOMatic.Tests.csproj --no-restore --filter FullyQualifiedName~TransferServiceUnitTests`.
- The Angular production build output is generated under `src/SharpOMatic.FrontEnd/dist/SharpOMatic-Editor/` and embedded by the Editor project; treat it as generated output.

## Documentation Sync
- Any code change that affects behavior, configuration, APIs, UI, workflow semantics, or user-visible functionality must include corresponding documentation updates in the same change.
- Keep `docs/` and package-level usage docs such as README files in sync with the implementation so the documentation remains current by default.
- When adding a new feature, changing an existing flow, or altering defaults, review the relevant docs and update them as part of the task rather than leaving documentation as follow-up work.

## Commit & Pull Request Guidelines
- Commits are short, descriptive, and prefix-free; keep messages to a single line (e.g., "OpenAI parameters").
- PRs should describe intent and scope, link any related issues/TODOs, include screenshots/GIFs for editor UI changes, and state tests run.

## Security & Configuration Tips
- The DemoServer stores SQLite data under the user's LocalApplicationData path; do not commit local `.db` files.
- `SharpOMatic.DemoServer/appsettings.json` is the place for environment configuration; keep secrets out of the repo.
