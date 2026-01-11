# Release 1

Fix designer bug where connectors do not show when zoomed in and trace bar shows
Improve ModelCall using a couple of classes
Update intro page of docs
Check that new asp.net with nuget packages works as described
Update the readme page on github
announce release to relevant people blogs

# vNext

## Major Features

### Engine

- Parallel batch processing of arrays
- User interaction node, suspend workflow and allow resume
- Integrate use of OmniSharp for full intellisense in code nodes
- Add workflow node to call another workflow as sub-task

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











