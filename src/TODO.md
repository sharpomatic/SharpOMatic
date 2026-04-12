# vNext

## Major Features

### Agent

- Endpoint for returning history of messages for existing conversation
- Override mechanism so the incoming headers and payload and generated context can be accessed and the context modified

### Output Stream

- Need to save and noify each trace event as it happens not as a batch at the end of the call
- Convert model call to streaming the results into information and stream events
- Allow model call checkbox for outputting stream events for text, reasoning, tool calls
- Add node for templated output of text events
- Engine notification to get the event

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

### Examples

- Chatbot where the workflow is started each time from the start node
- (uses the full history from the input to have history)
- Chatbot where the conversation is restarted from the suspend point
- (uses the chat history array that is persisted to have history)

### Promotion

- Add YouTube channel and some initial videos
- Add tab that links to you tube and website etc












