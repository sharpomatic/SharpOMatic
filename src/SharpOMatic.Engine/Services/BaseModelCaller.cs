namespace SharpOMatic.Engine.Services;

public abstract class BaseModelCaller : IModelCaller
{
    public abstract Task<(IList<ChatMessage> chat, IList<ChatMessage> responses, ContextObject)> Call(
        Model model,
        ModelConfig modelConfig,
        Connector connector,
        ConnectorConfig connectorConfig,
        ProcessContext processContext,
        ThreadContext threadContext,
        ModelCallNodeEntity node);

    protected static object? FastDeserializeString(string json)
    {
        var deserializer = new FastJsonDeserializer(json);
        return deserializer.Deserialize();
    }

    protected static bool HasCapability(Model model, ModelConfig modelConfig, string capability)
    {
        if ((model is null) || (modelConfig is null))
            return false;

        // If the config has the capability defined  (if custom then also selected in the model itself)
        return (modelConfig.Capabilities.Any(c => c.Name == capability) && (!modelConfig.IsCustom ||
               (modelConfig.IsCustom && model.CustomCapabilities.Any(c => c == capability))));
    }

    protected static bool GetCapabilityString(
        Model model, 
        ModelConfig modelConfig, 
        ModelCallNodeEntity node, 
        string capability, 
        string field, 
        out string paramValue)
    {
        if ((model is not null) &&
            (modelConfig is not null) &&
            HasCapability(model, modelConfig, capability))
        {
            var fieldDescription = modelConfig.ParameterFields.FirstOrDefault(c => c.Name == field);
            if (fieldDescription is not null)
            {
                string? paramString;
                if ((fieldDescription.CallDefined && node.ParameterValues.TryGetValue(field, out paramString) && !string.IsNullOrWhiteSpace(paramString)) ||
                    (!fieldDescription.CallDefined && model.ParameterValues.TryGetValue(field, out paramString) && !string.IsNullOrWhiteSpace(paramString)))
                {
                    paramValue = paramString;
                    return true;
                }
            }
        }

        paramValue = "";
        return false;
    }

    protected static bool GetCapabilityCallString(
        Model model, 
        ModelConfig modelConfig, 
        ModelCallNodeEntity node, 
        string capability, 
        string field, 
        out string paramValue)
    {
        if ((model is not null) &&
            (modelConfig is not null) &&
            HasCapability(model, modelConfig, capability) &&
            node.ParameterValues.TryGetValue(field, out var paramString) &&
            !string.IsNullOrWhiteSpace(paramString))
        {
            paramValue = paramString;
            return true;
        }

        paramValue = "";
        return false;
    }

    protected static bool GetCapabilityInt(
        Model model, 
        ModelConfig modelConfig, 
        ModelCallNodeEntity node, 
        string capability, 
        string field, 
        out int paramValue)
    {
        if ((model is not null) &&
            (modelConfig is not null) &&
            HasCapability(model, modelConfig, capability))
        {
            var fieldDescription = modelConfig.ParameterFields.FirstOrDefault(c => c.Name == field);
            if (fieldDescription is not null)
            {
                string? paramString;
                int paramInteger;
                if ((fieldDescription.CallDefined && node.ParameterValues.TryGetValue(field, out paramString) && int.TryParse(paramString, out paramInteger)) ||
                    (!fieldDescription.CallDefined && model.ParameterValues.TryGetValue(field, out paramString) && int.TryParse(paramString, out paramInteger)))
                {
                    paramValue = paramInteger;
                    return true;
                }
            }
        }

        paramValue = 0;
        return false;
    }

    protected static bool GetCapabilityFloat(
        Model model, 
        ModelConfig modelConfig, 
        ModelCallNodeEntity node, 
        string capability, 
        string field, 
        out float paramValue)
    {
        if ((model is not null) &&
            (modelConfig is not null) &&
            HasCapability(model, modelConfig, capability))
        {
            var fieldDescription = modelConfig.ParameterFields.FirstOrDefault(c => c.Name == field);
            if (fieldDescription is not null)
            {
                string? paramString;
                float paramFloat;
                if ((fieldDescription.CallDefined && node.ParameterValues.TryGetValue(field, out paramString) && float.TryParse(paramString, out paramFloat)) ||
                    (!fieldDescription.CallDefined && model.ParameterValues.TryGetValue(field, out paramString) && float.TryParse(paramString, out paramFloat)))
                {
                    paramValue = paramFloat;
                    return true;
                }
            }
        }

        paramValue = 0;
        return false;
    }

    protected static bool GetCapabilityBool(
        Model model, 
        ModelConfig modelConfig, 
        ModelCallNodeEntity node, 
        string capability, 
        string field, 
        out bool paramValue)
    {
        if ((model is not null) &&
            (modelConfig is not null) &&
            HasCapability(model, modelConfig, capability))
        {
            var fieldDescription = modelConfig.ParameterFields.FirstOrDefault(c => c.Name == field);
            if (fieldDescription is not null)
            {
                string? paramString;
                bool paramBool;
                if ((fieldDescription.CallDefined && node.ParameterValues.TryGetValue(field, out paramString) && bool.TryParse(paramString, out paramBool)) ||
                    (!fieldDescription.CallDefined && model.ParameterValues.TryGetValue(field, out paramString) && bool.TryParse(paramString, out paramBool)))
                {
                    paramValue = paramBool;
                    return true;
                }
            }
        }

        paramValue = false;
        return false;
    }

}
