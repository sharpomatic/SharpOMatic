


# Minimum Viable Product Features

[JsonConverter(typeof(JsonStringEnumConverter))]

Model Call
	- Specify model from registry
	- Specify instructions
	- Specify prompt with argument resolution using path resolution for get
	- Specify the output path for the result
	- Output as text or json for structured output
	  Copy the OpenAI / Gemini method of defining the structued output (defined and advanced schema)
	- Show the input and output context

Model Registry
	- Define OpenAI direct and Azure OpenAI as connectors
	- Define model name
	- Define end point
	- Define authentication, start with just api key, secret name, (for secrets.json) and then other which means you handle it in overrides
	- Allow user to optionally specify the parameters allowed for OpenAIExecutionSettings
	- Add test button to check connectivity

# Integration Overrides

Models
	- Connector building
	  Just override the authentication entry
	  Just override the openaiexecutionsettings
	  Override it all by you returning the actual connector instance
	  
Build
    - Different interfaces for specifying the list of custom serializers
	  1 to find them in the assemblies automatically
	  Another that asks you to list them by type

# Process

- Domain name
- GitHub account
  - Manual build (or tag) to create and publish a release
  - Docusaurus setup for generated static pages site (auto build and publish on changes)

- Build first version of documentation
- Publish and announce

# Implementation Details

Leaving unsaved page
	Should ask user if they really want to leave

Database
	Add indexes on fields that are part of ordering queries, listy created for run and runId in trace
	Deleting a workflow, deleteds the runs and traces for it

Server
	Integrate use of OmniSharp for full intellisense

Angular
	Look into barrel files to easier imports between areas.

