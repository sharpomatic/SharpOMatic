#pragma warning disable OPENAI001, MAAI001
namespace SharpOMatic.Engine.Services;

public class OpenAIModelCaller(IEnumerable<IEngineNotification> engineNotifications) : BaseModelCaller
{
    private readonly IEnumerable<IEngineNotification> _engineNotifications = engineNotifications;

    public OpenAIModelCaller()
        : this([]) { }

    public override async Task<ModelCallResult> Call(
        Model model,
        ModelConfig modelConfig,
        Connector connector,
        ConnectorConfig connectorConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node,
        IModelCallProgressSink progressSink
    )
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
        (var instructions, var prompt) = await ResolveInstructionsAndPrompt(chat, processContext, threadContext, node);
        (var agentClient, var modelName) = GetOpenAIResponseClient(model, modelConfig, authenticationModeConfig, connectionFields);

        // Use the Microsoft Agent Framework by creating an agent from the responses AI client, then run the agent call
        //
        // WORKAROUND: AsIChatClientWithStoredOutputDisabled forces stateless mode (StoredOutputEnabled=false).
        // The default stateful mode (StoredOutputEnabled=true) causes an intermittent HTTP 400
        // "No tool call found for function call output with call_id" when multiple tools are available.
        // The bug is inside FunctionInvokingChatClient (Microsoft.Extensions.AI): when the Responses API
        // returns a ConversationId on one turn but not the next, the framework misassembles the follow-up
        // request, breaking the strict function_call → function_call_output pairing the Responses API requires.
        // Track: https://github.com/microsoft/agent-framework/issues/3795
        // TO REVERT: replace the clientFactory lambda below with the commented-out original line.
        var agent = agentClient.AsAIAgent(
            modelName,
            instructions: instructions,
            // Original (stateful): clientFactory: chatClient => CreateFunctionInvokingChatClient(chatClient, agentServiceProvider),
            clientFactory: _ => CreateFunctionInvokingChatClient(agentClient.AsIChatClientWithStoredOutputDisabled(modelName), agentServiceProvider),
            services: agentServiceProvider
        );
        await EmitPromptStreamEvents(processContext, prompt, node.DisableStreamUser);
        var result = await CallConfiguredAgent(agent, chat, chatOptions, jsonOutput, node, progressSink);
        return result.ProviderModelName is null
            ? new ModelCallResult()
            {
                Chat = result.Chat,
                Responses = result.Responses,
                ResultValue = result.ResultValue,
                Usage = result.Usage,
                ProviderModelName = modelName,
            }
            : result;
    }

    public virtual (ResponsesClient client, string modelName) GetOpenAIResponseClient(
        Model model,
        ModelConfig modelConfig,
        AuthenticationModeConfig authenticationModeConfig,
        Dictionary<string, string?> connectionFields
    )
    {
        foreach (var notification in _engineNotifications)
        {
            var responseClient = notification.OpenAIOverride(model, modelConfig, authenticationModeConfig, connectionFields);
            if (responseClient is not null)
                return responseClient.Value;
        }

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
        return (client.GetResponsesClient(), modelName);
    }

    protected virtual (AuthenticationModeConfig, Dictionary<string, string?>) GetAuthenticationFields(Connector connector, ConnectorConfig connectorConfig, ProcessContext processContext)
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
            notification.ConnectionOverride(processContext.Run.RunId, processContext.Run.WorkflowId, processContext.Run.ConversationId, connector.ConfigId, authenticationModel, connectionFields);

        return (authenticationModel, connectionFields);
    }

    protected virtual (ChatOptions, CreateResponseOptions) SetupResponsesBasicCapabilities(
        Model model,
        ModelConfig modelConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node,
        ChatOptions? chatOptions = null
    )
    {
        // Mostly we have set the ChatOptions but sometimes we have to drop down to the Responses AI specific ResponseCreationOptions
        var responseOptions = new CreateResponseOptions();
        chatOptions = chatOptions ?? new ChatOptions() { AdditionalProperties = [], RawRepresentationFactory = (_) => responseOptions };

        // Setup common capabilites using base class
        chatOptions = SetupBasicCapabilities(model, modelConfig, processContext, threadContext, node, chatOptions);

        if (GetCapabilityString(model, modelConfig, node, "SupportsReasoningEffort", "reasoning_effort", out string reasoningEffort))
            responseOptions.ReasoningOptions = new ResponseReasoningOptions() { ReasoningEffortLevel = new ResponseReasoningEffortLevel(reasoningEffort.ToLower()) };

        return (chatOptions, responseOptions);
    }

    protected virtual IServiceProvider SetupToolCalling(
        ChatOptions chatOptions,
        CreateResponseOptions responseOptions,
        Model model,
        ModelConfig modelConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node
    )
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
