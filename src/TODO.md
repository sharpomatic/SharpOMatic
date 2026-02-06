# vNext

## Major Features

### Engine

- Integrate use of LSP/OmniSharp for full intellisense in code nodes

### Connectors

- Gemini connector (tool calling fails with: Duplicate function declaration found: GetTime when using raw factory)
- Anthropic connector

### Evaluations

- Manual creation of column definitions (add/update/remove)
- Manual view and modify of a data grid (add/edit/remove)
- Start an evalaution run (full or sampled)
- Cancel a running evaluation
- List runs
- Import/Merging of existing data using csv

### MCP Servers 

- Add MCP Server as new sidebar page
- ModelCall should allow them to be provided in calls

### Editor

- Fan In and Batch need to specify the path to merge with output as default
- EditNode, copy section. So it can move, upsert, duplicate then delete
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











