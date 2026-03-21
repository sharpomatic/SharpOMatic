# vNext

## Major Features

### Model Calls

- Separate table to record each model call, time, tokens, error
- Reference to owning workflow name, connector name, model name because...
- Needs to be valid even when the connector/model has been deleted
- Will be used for metrics/cost of model usage

### Upgrade

- Move packages to latest once the GA of MAF occurs

### Metadata

- List models in descending order so higher numbers appear first

### Engine

- Integrate use of LSP/OmniSharp for full intellisense in code nodes

### Connectors

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












