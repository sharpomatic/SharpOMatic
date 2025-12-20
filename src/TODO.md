


# Minimum Viable Product Features

Model Call
	- Support tool calling, reuse existing attributes etc?
	- Support MCP Servers
	- Text tab
	- Images

Connection
	- Add test button to check connectivity

Model
	- Add test button to check connectivity

Max nodes executed for a workflow to prevent looping forever
Max execution time for workflow to prevent it running forever
Max number of runs to keep for a workflow to prevent too much data storage

Designer, right mouse down to drag surface, maybe zoom/unzoom

# Integration Overrides

Max values for nodes, execution and runs to keep
	  
Build
    - Different interfaces for specifying the list of custom serializers
	  1 to find them in the assemblies automatically
	  Another that asks you to list them by type

# Process

- GitHub account
  - Manual build (or tag) to create and publish a release
  - Docusaurus setup for generated static pages site (auto build and publish on changes)

- Build first version of documentation
- Publish and announce

# Implementation Details

Improve sharpomatic setup via a builder.

Add UI editor as exposed via a specified path

Database
	Add a version field to all serializable data, like workflow, connector, model, run, trace so easier to upgrade in future
	Add indexes on fields that are part of ordering queries
	Deleting a workflow, deletes the runs and traces for it
	Built example connecting to SQL Server instead


# Futures

Server
	Integrate use of OmniSharp for full intellisense



