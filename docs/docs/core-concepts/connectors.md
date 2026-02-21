---
title: Connectors
sidebar_position: 2
---

Connectors define how SharpOMatic connects to external providers.
They capture the credentials, endpoints, and other details needed to access third-party services.
Currently only language model providers are supported, but this will be expanded in the future.

## Connector Configs

The repository stores connector configurations. 
These are metadata definitions that describe supported providers.
Each config lists the fields the editor should collect, such as API keys, tenant IDs, endpoints, and so forth.
When the user creates a new connector instance, they can select from this list of known providers.

## Connector Instances

A connector is an instance of a connector config.
The exact set of fields presented to the user is defined by the config metadata and varies by provider needs.
Typically, you select an authentication method plus additional values as needed.
Secrets are stored with the connector instance and are hidden by default when returned by APIs.

## Supported Connectors

Currently, the following connectors are supported.

### OpenAI

This allows direct connection to the OpenAI API.
Use this if you have an account with OpenAI and want to call the models they provide.
You can provide the following values:

- **API Key** - mandatory
- **Organization ID** - optional
- **Project ID** - optional

<img src="/img/connectors_openai_apikey.png" alt="NodeCall capability tabs" width="600" style={{ maxWidth: '100%', height: 'auto' }} />

### Azure OpenAI

This allows requests to Azure-hosted models that support the OpenAI protocol.
Azure access is provided with two authentication modes.

#### API Key

The simplest method of authentication is copying the model endpoint and API key from the Azure portal.
This is good for initial prototyping but not recommended for production systems.

- **Endpoint** - mandatory
- **API Key** - mandatory

<img src="/img/connectors_azure_openai_apikey.png" alt="NodeCall capability tabs" width="600" style={{ maxWidth: '100%', height: 'auto' }} />

#### Default Azure Credential

The alternate approach avoids API keys and instead uses one of the many supported Azure authentication methods.
This approach only requires the endpoint to be specified.

- **Endpoint** - mandatory

<img src="/img/connectors_azure_openai_default.png" alt="NodeCall capability tabs" width="600" style={{ maxWidth: '100%', height: 'auto' }} />

Internally, it uses the `DefaultAzureCredential` class.
This flexible class pulls credentials from a long list of possible sources and should cover most production scenarios.
You can look up the details for each credential type in the Microsoft documentation. It attempts credentials in the order listed:

- EnvironmentCredential
- WorkloadIdentityCredential
- ManagedIdentityCredential
- VisualStudioCredential
- VisualStudioCodeCredential
- AzureCliCredential
- AzurePowerShellCredential
- AzureDeveloperCliCredential
- InteractiveBrowserCredential

## Azure Models

Azure has more than one way of exposing models.

- **Azure OpenAI** - This resource type only hosts OpenAI models.
- **Microsoft Foundry** - This resource type allows models from OpenAI and other providers.

### Azure OpenAI Resource

Using this resource type is no longer the recommended approach, but there are many instances where it is still being used.
Look up the endpoint of the resource and then use either an API key or Default Azure Credential as the authentication method.

### Azure Foundry Resource

This is the approach now recommended by Microsoft.
Model deployments within a Foundry provide access to OpenAI models and many other open-source models such as those from DeepSeek and Meta.
Fortunately, all models exposed from Foundry use the OpenAI protocol, so you can access DeepSeek, Llama, and thousands of other models using the Azure OpenAI connector.

## Google AI

This connector allows calls to Google AI compatible models.
The current auth mode is API key based.

- **API Key** - mandatory
