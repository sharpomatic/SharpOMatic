namespace SharpOMatic.Engine.Services;

public class GoogleGenAIModelCaller : BaseModelCaller
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
        (var chatOptions, var generateContentConfig) = SetupGoogleBasicCapabilities(model, modelConfig, processContext, threadContext, node);
        var jsonOutput = SetupStrucuturedOutput(chatOptions, model, modelConfig, processContext, node);
        var agentServiceProvider = SetupToolCalling(chatOptions, model, modelConfig, processContext, threadContext, node);

        // Generate the chat messages for input to the model
        List<ChatMessage> chat = [];
        AddChatInputPathMessages(chat, threadContext, node);
        await AddImageMessages(chat, model, modelConfig, processContext, threadContext, node);

        // Resolve the instructions and prompts as templates
        var instructions = await ResolveInstructionsAndPrompt(chat, processContext, threadContext, node);
        var chatClient = GetChatClient(model, modelConfig, authenticationModeConfig, connectionFields);

        // Use the Microsoft Agent Framework by creating a chat client based agent
        var agent = new ChatClientAgent(chatClient, instructions: instructions, services: agentServiceProvider);
        return await CallAgent(agent, chat, chatOptions, jsonOutput, node);
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

    protected virtual (ChatOptions, Google.GenAI.Types.GenerateContentConfig) SetupGoogleBasicCapabilities(
        Model model,
        ModelConfig modelConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node,
        ChatOptions? chatOptions = null
        )
    {
        // Mostly we have set the ChatOptions but sometimes we have to drop down to the Google AI specific GenerateContentConfig
        var generateContentConfig = new Google.GenAI.Types.GenerateContentConfig();
        chatOptions = chatOptions ?? new ChatOptions() { AdditionalProperties = [], RawRepresentationFactory = (_) => generateContentConfig };

        chatOptions = SetupBasicCapabilities(model, modelConfig, processContext, threadContext, node, chatOptions);

        if (GetCapabilityInt(model, modelConfig, node, "SupportsSampling", "top_k", out int topK))
            chatOptions.TopK = topK;

        if (GetCapabilityString(model, modelConfig, node, "SupportsThinkingLevel", "thinking_level", out string thinkingLevel))
            generateContentConfig.ThinkingConfig = new Google.GenAI.Types.ThinkingConfig()
            {
                ThinkingLevel = FindThinkingLevel(thinkingLevel),
                IncludeThoughts = true
            };

        return (chatOptions, generateContentConfig);
    }

    private Google.GenAI.Types.ThinkingLevel FindThinkingLevel(string thinkingLevel)
    {
        return thinkingLevel.ToLower() switch
        {
            "minimal" => Google.GenAI.Types.ThinkingLevel.MINIMAL,
            "low" => Google.GenAI.Types.ThinkingLevel.LOW,
            "medium" => Google.GenAI.Types.ThinkingLevel.MEDIUM,
            "high" => Google.GenAI.Types.ThinkingLevel.HIGH,
            _ => Google.GenAI.Types.ThinkingLevel.THINKING_LEVEL_UNSPECIFIED
        };
    }

    protected virtual IChatClient GetChatClient(
        Model model,
        ModelConfig modelConfig,
        AuthenticationModeConfig authenticationModeConfig,
        Dictionary<string, string?> connectionFields)
    {
        if (authenticationModeConfig.Id != "gen_ai")
            throw new SharpOMaticException($"Unrecognized authentication method of '{authenticationModeConfig.Id}'");

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

        var client = new Client(apiKey: apiKey);
        return client.AsIChatClient(modelName);
    }
}