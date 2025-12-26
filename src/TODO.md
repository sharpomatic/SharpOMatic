# Minimum Viable Product Features

Max nodes executed for a workflow to prevent looping forever
Max number of runs to keep for a workflow to prevent too much data storage

# Node Traces

- LLM streaming to get intermediate outputs
- Tool calling as child trace
- Thinking output as child trace
- 

# LLM's

- Azure OpenAI - models
- Microsoft Foundry - models

# Conversations

- ChatMessage serializable (use AIJsonUtilities.DefaultOptions), would need to update our context serializing to notice and use this for serial/deserial
- Allow ModelCall to specify the path to get current conversation from (with create one if not present), then append call result to this same instance.

# Export and Import

- Workflows
	  
# Process

- GitHub account
  - Manual build (or tag) to create and publish a release
  - Docusaurus setup for generated static pages site (auto build and publish on changes)

- Build first version of documentation

# Implementation Details

Package the editor in a package that can be added with extension method
Add UI editor as exposed via a specified path


# Futures

Usage - token usage counts

Traces - show tool calls, reasoning and other details

Images - input and output of images

ChatKit output integration
User output, as there might be a stream of outputs
User input request, for LLM or any other part of a process

Models
	Support MCP Servers

Server
	Integrate use of OmniSharp for full intellisense





