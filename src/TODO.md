## Assets

Convert from context entry into actual ContextObject entries, AssetRef and ContextList of AssetRef.
Input and Output context should correct show the entries, maybe special case for better output.

# Images

Add Supports Image In and Image Out

ModelCall

What about pdf or other file types? Are any allowed? Should it be Data in and Out?

Image In - define a list of input paths that contain images
 - Only the ones with a type starting with image are processed, others ignored?
Image Out, just a path for any output images to be placed into as assetref but with RunId and Run scope.
	
How to pass a new AssetRef into a workflow via code which is then treated as Runid/Run scope?

	
## LLM's

- Azure OpenAI - models
- Microsoft Foundry - models

## Export and Import to move between dev/staging/prod

- Workflows
- Connections
- Models

 To zip file?
	  
## Process

- GitHub account
  - Manual build (or tag) to create and publish a release
  - Docusaurus setup for generated static pages site (auto build and publish on changes)

- Build first version of documentation

## Implementation Details

Output the editor and engine as nuget packages
GitHub build pipeline to build/package and publish to nuget the two packages

## Security

How does the user add extra security that impacts the controllers that the hosting adds?





# Futures

Usage - token usage counts

Traces - show tool calls, reasoning and other details

Images - input and output of images

ChatKit output integration
User output, as there might be a stream of outputs
User input request, for LLM or any other part of a process

MCP Server

Server
	Integrate use of OmniSharp for full intellisense





