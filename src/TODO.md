# vNext

## Major Features

### Agent

- Add node for templated output of text/reasoning events with templated text

- Endpoint for returning history of messages for existing conversation
  limit how many of the most recent are returned
- Investigate the compacting down to a message snapshot.

- Make the input.chat a detais defined path instead of being hardcoded.
- Make the agents path details defined and not hard coded.

- Check two paths outputing at the same time
- Check two paths, the first has a FE tool call, other does not

### Others

- Ensure template expansion is recursive
- Add helper that takes a string and performs the context based/asset based expansion

- Add view button to Runs and Conversation history and causes it to be loaded as the current data for the trace viewer.

### Model Calls

- Ensure that parameter substituion recurses into the text that is replaced
- Separate table to record each model call, time, tokens, error
- Reference to owning workflow name, connector name, model name because...
- Needs to be valid even when the connector/model has been deleted
- Will be used for metrics/cost of model usage

### Upgrade

- Move agent nuget to latest versions

### Engine

- Integrate use of LSP/OmniSharp for full intellisense in code nodes

### Connectors

- Anthropic connector

Investigate adding the hosted tools

Microsoft.Extensions.AI.AIFunctionDeclaration
Microsoft.Extensions.AI.HostedCodeInterpreterTool
Microsoft.Extensions.AI.HostedFileSearchTool
Microsoft.Extensions.AI.HostedImageGenerationTool
Microsoft.Extensions.AI.HostedMcpServerTool
Microsoft.Extensions.AI.HostedWebSearchTool

### Evaluations

- Import/Merging of existing data using csv

### MCP Servers 

- Add MCP Server as new sidebar page
- ModelCall should allow them to be provided in calls

### Editor

- Cut, copy and paste of workflow nodes/connectors

### Assets

- Check that removing a run gets rid of run assets and the run assets folder for that run

### Promotion

- Add YouTube channel and some initial videos
- Add tab that links to you tube and website etc












