# vNext

## Major Features

### EF

- Create SQLite specific nuget package for migrations
- Create SQLServer specific nuget package for migrations
- Create initial migrations for both
- Check both work by connecting to Azure SQL instance from DemoServer

### Engine

- Integrate use of LSP/OmniSharp for full intellisense in code nodes

### Connectors

- Gemini connector (tool calling fails with: Duplicate function declaration found: GetTime when using raw factory)
- Anthropic connector

### Evaluations

- Generate notification at each stage
- Front end use notification for start, each row finished and overall finished
- Make frontend look good with graphs or pie charts etc.
- Parallel execution
- Allow cancelling a run part way through
- Import/Merging of existing data using csv

### MCP Servers 

- Add MCP Server as new sidebar page
- ModelCall should allow them to be provided in calls

### Editor

- Cut, copy and paste of workflow nodes/connectors

### ChatClient

- Investigate exposing a workflow as AG-UI
- Investigate using CopilotKit as example client to workflow

### Assets

- Add two level structure, folders and then assets

## Minor Features

Usage - token usage counts
Trace - Child traces for model call, for thinking and tool calls
Connectors - allow calling of image generator
Connectors - allow calling embeddings











