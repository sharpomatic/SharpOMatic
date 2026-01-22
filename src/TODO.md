# vNext

- Backward pointing outputs need to route up/down -> backwards -> up/down -> forward
- Check that trace works well for gosub workflows.

## Major Features

### Engine

- User interaction node, suspend workflow and allow resume
- Integrate use of LSP/OmniSharp for full intellisense in code nodes

### Connectors

- Gemini connector
- Anthropic connector

### Evaluations

- Add new sidebar area for evaluations
- Import of existing data using csv
- Run an evaluation using workflow as the judge

### MCP Servers 

- Add MCP Server as new sidebar page
- ModelCall should allow them to be provided in calls

### Editor

- Allow run assets to be viewed in current run and historic run display
- Cut, copy and paste of workflow nodes/connectors

### ChatClient

- Create chat bot UI that calls workflow and shows results
- Or investigate using OpenAI ChatKit to route to our backend
- Need to handle domain lookup issue for ChatKit
- Would need to see OpenAI Python library to see protocol, including initial get session call

## Minor Features

Usage - token usage counts
Traces - show tool calls, reasoning and other details within a node trace
OpenAI - allow calling of image generator
OpenAI - allow calling embeddings











