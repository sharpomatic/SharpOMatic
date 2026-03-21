# vNext

## Major Features

### Bugs

- Only if we hit run do we take notice of the signalR feedback for a running model trace/information/toastie 

### Model Calls

- Separate table to record each model call, time, tokens, error
- Reference to owning workflow name, connector name, model name because...
- Needs to be valid even when the connector/model has been deleted
- Will be used for metrics/cost of model usage

### Upgrade

- Move packages to latest once the GA of MAF occurs
- Check for when the Google Tool Calling bug is fixed

### Metadata

- List models in descending order so higher numbers appear first
- Include as information only, pricing and context length

### Engine

- Integrate use of LSP/OmniSharp for full intellisense in code nodes

### Connectors

- Gemini connector (tool calling fails with: Duplicate function declaration found: GetTime when using raw factory)
- Anthropic connector

### Evaluations

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

- Check that removing a run gets rid of run assets and the run assets folder for that run

### Promotion

- Add YouTube channel and some initial videos
- Add tab that links to you tube and website etc












