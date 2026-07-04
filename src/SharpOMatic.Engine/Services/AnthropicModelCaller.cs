#pragma warning disable MAAI001
// Aliased because Anthropic.Models.Messages defines Model/Message/etc. that collide with SharpOMatic's own types.
using AnthropicMessages = Anthropic.Models.Messages;

namespace SharpOMatic.Engine.Services;

public class AnthropicModelCaller(IEnumerable<IEngineNotification> engineNotifications) : BaseModelCaller
{
    private readonly IEnumerable<IEngineNotification> _engineNotifications = engineNotifications;

    public AnthropicModelCaller()
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
        (var authenticationModeConfig, var connectionFields) = GetAuthenticationFields(connector, connectorConfig, processContext);

        if (!HasCapability(model, modelConfig, "SupportsTextIn"))
            throw new SharpOMaticException("Model does not support text input.");

        var chatOptions = SetupAnthropicBasicCapabilities(model, modelConfig, processContext, threadContext, node);
        var jsonOutput = SetupStrucuturedOutput(chatOptions, model, modelConfig, processContext, node);
        var modelCallExitState = new ModelCallExitState();
        var agentServiceProvider = SetupToolCalling(chatOptions, model, modelConfig, processContext, threadContext, node, modelCallExitState);

        List<ChatMessage> chat = [];
        AddChatInputPathMessages(chat, threadContext, node);
        await AddImageMessages(chat, model, modelConfig, processContext, threadContext, node);

        (var instructions, var prompt) = await ResolveInstructionsAndPrompt(chat, processContext, threadContext, node);
        (var client, var modelName) = GetAnthropicClient(model, modelConfig, authenticationModeConfig, connectionFields);

        var agent = client.AsAIAgent(
            modelName,
            instructions: instructions,
            clientFactory: chatClient => CreateFunctionInvokingChatClient(chatClient, agentServiceProvider),
            services: agentServiceProvider
        );
        await EmitPromptStreamEvents(processContext, prompt, node.DisableStreamUser);
        var result = await CallConfiguredAgent(agent, chat, chatOptions, jsonOutput, node, progressSink, modelCallExitState);
        return result.ProviderModelName is null
            ? new ModelCallResult()
            {
                Chat = result.Chat,
                Responses = result.Responses,
                ResultValue = result.ResultValue,
                Usage = result.Usage,
                ProviderModelName = modelName,
                ExitContext = result.ExitContext,
                ExitContextPath = result.ExitContextPath,
            }
            : result;
    }

    public virtual (AnthropicClient client, string modelName) GetAnthropicClient(
        Model model,
        ModelConfig modelConfig,
        AuthenticationModeConfig authenticationModeConfig,
        Dictionary<string, string?> connectionFields
    )
    {
        foreach (var notification in _engineNotifications)
        {
            var overrideClient = notification.AnthropicOverride(model, modelConfig, authenticationModeConfig, connectionFields);
            if (overrideClient is not null)
                return overrideClient.Value;
        }

        if (authenticationModeConfig.Id != "api_key")
            throw new SharpOMaticException($"Unrecognized authentication method of '{authenticationModeConfig.Id}'");

        if (!connectionFields.TryGetValue("api_key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            throw new SharpOMaticException("Connector api key not specified.");

        return (new AnthropicClient { ApiKey = apiKey }, ResolveProviderModelName(model, modelConfig));
    }

    protected virtual (AuthenticationModeConfig, Dictionary<string, string?>) GetAuthenticationFields(Connector connector, ConnectorConfig connectorConfig, ProcessContext processContext)
    {
        var authenticationModel = connectorConfig.AuthModes.FirstOrDefault(a => a.Id == connector.AuthenticationModeId);
        if (authenticationModel is null)
            throw new SharpOMaticException("Connector has no selected authentication method");

        Dictionary<string, string?> connectionFields = [];
        foreach (var field in authenticationModel.Fields)
            if (connector.FieldValues.TryGetValue(field.Name, out var fieldValue))
                connectionFields.Add(field.Name, fieldValue);

        var notifications = processContext.ServiceScope.ServiceProvider.GetServices<IEngineNotification>();
        foreach (var notification in notifications)
            notification.ConnectionOverride(processContext.Run.RunId, processContext.Run.WorkflowId, processContext.Run.ConversationId, connector.ConfigId, authenticationModel, connectionFields);

        return (authenticationModel, connectionFields);
    }

    protected virtual ChatOptions SetupAnthropicBasicCapabilities(
        Model model,
        ModelConfig modelConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node,
        ChatOptions? chatOptions = null
    )
    {
        chatOptions = SetupBasicCapabilities(model, modelConfig, processContext, threadContext, node, chatOptions);

        // Thinking mode and reasoning effort can't be expressed through the provider-agnostic ChatOptions, so
        // drop down to the Anthropic-native MessageCreateParams and hand it to the chat client via
        // RawRepresentationFactory. The client merges the resolved ChatOptions (model, max tokens, the chat
        // messages, tools) over this seed - so Model/MaxTokens/Messages here are placeholders it overwrites,
        // and only the Thinking/OutputConfig it doesn't otherwise know about survive. This mirrors how the
        // OpenAI Responses caller supplies its ReasoningOptions via a CreateResponseOptions seed.
        if (HasCapability(model, modelConfig, "SupportsReasoningEffort"))
        {
            // Adaptive thinking by default; only turn it off when the caller explicitly selects "Disabled".
            // Pin the summarized display so the reasoning summary keeps streaming to the trace even on models
            // that would otherwise default to omitting it.
            AnthropicMessages.ThinkingConfigParam thinking;
            if (GetCapabilityString(model, modelConfig, node, "SupportsReasoningEffort", "thinking_mode", out var thinkingMode) && thinkingMode == "Disabled")
                thinking = new AnthropicMessages.ThinkingConfigDisabled();
            else
                thinking = new AnthropicMessages.ThinkingConfigAdaptive { Display = AnthropicMessages.Display.Summarized };

            AnthropicMessages.OutputConfig? outputConfig = null;
            if (GetCapabilityString(model, modelConfig, node, "SupportsReasoningEffort", "effort_level", out var effortLevel) && TryMapEffort(effortLevel, out var effort))
                outputConfig = new AnthropicMessages.OutputConfig { Effort = effort };

            var messageCreateParams = new AnthropicMessages.MessageCreateParams
            {
                // Placeholders - overwritten by the chat client from the resolved ChatOptions/messages.
                Model = modelConfig.DisplayName,
                MaxTokens = chatOptions.MaxOutputTokens ?? 4096,
                Messages = [],
                Thinking = thinking,
                OutputConfig = outputConfig,
            };
            chatOptions.RawRepresentationFactory = _ => messageCreateParams;
        }

        return chatOptions;
    }

    private static bool TryMapEffort(string effortLevel, out AnthropicMessages.Effort effort)
    {
        switch (effortLevel)
        {
            case "Low":
                effort = AnthropicMessages.Effort.Low;
                return true;
            case "Medium":
                effort = AnthropicMessages.Effort.Medium;
                return true;
            case "High":
                effort = AnthropicMessages.Effort.High;
                return true;
            case "XHigh":
                effort = AnthropicMessages.Effort.Xhigh;
                return true;
            case "Max":
                effort = AnthropicMessages.Effort.Max;
                return true;
            default:
                effort = default;
                return false;
        }
    }

    protected override IServiceProvider SetupToolCalling(
        ChatOptions chatOptions,
        Model model,
        ModelConfig modelConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node,
        ModelCallExitState? modelCallExitState = null
    )
    {
        var agentServiceProvider = base.SetupToolCalling(chatOptions, model, modelConfig, processContext, threadContext, node, modelCallExitState);
        ApplyParallelToolCalls(chatOptions, model, modelConfig, node);
        return agentServiceProvider;
    }

    // The base SetupToolCalling handles tool selection and tool_choice but not parallel tool calls, so map the
    // shared `parallel_tool_calls` field onto the MEAI-standard ChatOptions.AllowMultipleToolCalls - the Anthropic
    // chat client translates that to disable_parallel_tool_use. When the field is unset we leave it alone and the
    // model keeps its default (parallel enabled). Extracted from the override so it can be unit-tested without a
    // ProcessContext (which base.SetupToolCalling dereferences).
    protected static void ApplyParallelToolCalls(ChatOptions chatOptions, Model model, ModelConfig modelConfig, ModelCallNodeEntity node)
    {
        if (GetCapabilityBool(model, modelConfig, node, "SupportsToolCalling", "parallel_tool_calls", out var parallelToolCalls))
            chatOptions.AllowMultipleToolCalls = parallelToolCalls;
    }

    protected static string ResolveProviderModelName(Model model, ModelConfig modelConfig)
    {
        if (modelConfig.IsCustom)
        {
            if (!model.ParameterValues.TryGetValue("model_name", out var modelName) || string.IsNullOrWhiteSpace(modelName))
                throw new SharpOMaticException("Model does not specify the custom model name");

            return modelName;
        }

        return modelConfig.DisplayName;
    }
}
