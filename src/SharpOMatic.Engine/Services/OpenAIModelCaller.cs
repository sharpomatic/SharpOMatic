#pragma warning disable OPENAI001
namespace SharpOMatic.Engine.Services;

public class OpenAIModelCaller : BaseModelCaller
{
    public override async Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, ContextObject)> Call(
        Model model, 
        ModelConfig modelConfig, 
        Connector connector, 
        ConnectorConfig connectorConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node)
    {
        // Get authentication method specific fields, allows for optional customization by user override mechanism
        (var authenticationModeConfig, var connectionFields) = GetAuthenticationFields(connector, connectorConfig, processContext);

        // Currently we only support models that can provide text input
        if (!HasCapability(model, modelConfig, "SupportsTextIn"))
            throw new SharpOMaticException("Model does not support text input.");

        // Setup the basic capabilities, then the more specialized options
        (var chatOptions, var responseCreationOptions) = SetupResponsesBasicCapabilities(model, modelConfig, processContext, threadContext, node);
        var jsonOutput = SetupStrucuturedOutput(chatOptions, model, modelConfig, processContext, node);
        var agentServiceProvider = SetupToolCalling(chatOptions, responseCreationOptions, model, modelConfig, processContext, threadContext, node);

        // Generate the chat messages for input to the model
        List<ChatMessage> chat = [];
        AddChatInputPathMessages(chat, threadContext, node);
        await AddImageMessages(chat, model, modelConfig, processContext, threadContext, node);

        // Resolve the instructions and prompts as templates
        var instructions = await ResolveInstructionsAndPrompt(chat, processContext, threadContext, node);
        var agentClient = GetOpenAIResponseClient(model, modelConfig, authenticationModeConfig, connectionFields);

        // Use the Microsoft Agent Framework by creating an agent from the responses AI client, then run the agent call
        var agent = agentClient.CreateAIAgent(instructions: instructions, services: agentServiceProvider);
        return await CallAgent(agent, chat, chatOptions, jsonOutput, node);
    }

    public virtual OpenAIResponseClient GetOpenAIResponseClient(
        Model model,
        ModelConfig modelConfig,
        AuthenticationModeConfig authenticationModeConfig,
        Dictionary<string, string?> connectionFields)
    {
        var options = new OpenAIClientOptions();
        if (!connectionFields.TryGetValue("api_key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            throw new SharpOMaticException("Connector api key not specified.");

        string? modelName;
        if (modelConfig.IsCustom)
        {
            if (!model.ParameterValues.TryGetValue("model_name", out modelName) || string.IsNullOrWhiteSpace(modelName))
                throw new SharpOMaticException("Model does not specify the custom model name");
        }
        else
            modelName = modelConfig.DisplayName;

        if (connectionFields.TryGetValue("organization_id", out var organizationId) && !string.IsNullOrWhiteSpace(organizationId))
            options.OrganizationId = organizationId;

        if (connectionFields.TryGetValue("project_id", out var projectId) && !string.IsNullOrWhiteSpace(projectId))
            options.ProjectId = projectId;

        var client = new OpenAIClient(new ApiKeyCredential(apiKey ?? ""), options);
        return client.GetOpenAIResponseClient(modelName);
    }

    protected virtual (AuthenticationModeConfig, Dictionary<string, string?>) GetAuthenticationFields(
        Connector connector,
        ConnectorConfig connectorConfig,
        ProcessContext processContext)
    {
        var authenticationModel = connectorConfig.AuthModes.FirstOrDefault(a => a.Id == connector.AuthenticationModeId);
        if (authenticationModel is null)
            throw new SharpOMaticException("Connector has no selected authentication method");

        // Copy only the relevant connector fields into a new dictionary
        Dictionary<string, string?> connectionFields = [];
        foreach (var field in authenticationModel.Fields)
            if (connector.FieldValues.TryGetValue(field.Name, out var fieldValue))
                connectionFields.Add(field.Name, fieldValue);

        // Allow the user notifications to customize the field values
        var notifications = processContext.ServiceScope.ServiceProvider.GetServices<IEngineNotification>();
        foreach (var notification in notifications)
            notification.ConnectionOverride(processContext.Run.RunId, processContext.Run.WorkflowId, connector.ConfigId, authenticationModel, connectionFields);

        return (authenticationModel, connectionFields);
    }

    protected virtual (ChatOptions, ResponseCreationOptions) SetupResponsesBasicCapabilities(
        Model model,
        ModelConfig modelConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node,
        ChatOptions? chatOptions = null)
    {
        // Mostly we have set the ChatOptions but sometimes we have to drop down to the Responses AI specific ResponseCreationOptions
        var responseOptions = new ResponseCreationOptions();
        chatOptions = chatOptions ?? new ChatOptions() { AdditionalProperties = [], RawRepresentationFactory = (_) => responseOptions };

        // Setup common capabilites using base class
        chatOptions = SetupBasicCapabilities(model, modelConfig, processContext, threadContext, node, chatOptions);

        if (GetCapabilityString(model, modelConfig, node, "SupportsReasoningEffort", "reasoning_effort", out string reasoningEffort))
            responseOptions.ReasoningOptions = new ResponseReasoningOptions()
            {
                ReasoningEffortLevel = new ResponseReasoningEffortLevel(reasoningEffort.ToLower())
            };

        return (chatOptions, responseOptions);
    }


    protected virtual IServiceProvider SetupToolCalling(
        ChatOptions chatOptions,
        ResponseCreationOptions responseOptions,
        Model model,
        ModelConfig modelConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node)
    {
        var agentServiceProvider = processContext.ServiceScope.ServiceProvider;
        if (HasCapability(model, modelConfig, "SupportsToolCalling"))
        {
            if (GetCapabilityBool(model, modelConfig, node, "SupportsToolCalling", "parallel_tool_calls", out bool parallelToolCalls))
                responseOptions.ParallelToolCallsEnabled = parallelToolCalls;

            // Process all the other tool calling functionality which is common
            agentServiceProvider = SetupToolCalling(chatOptions, model, modelConfig, processContext, threadContext, node);
        }

        return agentServiceProvider;
    }

}
