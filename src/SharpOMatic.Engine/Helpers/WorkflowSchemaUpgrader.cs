namespace SharpOMatic.Engine.Helpers;

public static class WorkflowSchemaUpgrader
{
    public static WorkflowEntity Upgrade(WorkflowEntity workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        if (workflow.Version < WorkflowSchema.MinimumSupportedVersion)
            throw new SharpOMaticException($"Workflow '{workflow.Id}' uses unsupported schema version '{workflow.Version}'.");

        if (workflow.Version > WorkflowSchema.CurrentVersion)
            throw new SharpOMaticException(
                $"Workflow '{workflow.Id}' uses schema version '{workflow.Version}', but this version of SharpOMatic supports up to version '{WorkflowSchema.CurrentVersion}'."
            );

        while (workflow.Version < WorkflowSchema.CurrentVersion)
        {
            switch (workflow.Version)
            {
                case 1:
                    UpgradeVersion1ToVersion2(workflow);
                    break;
                default:
                    throw new SharpOMaticException($"Workflow '{workflow.Id}' uses unsupported schema version '{workflow.Version}'.");
            }
        }

        ValidateCurrentVersion(workflow);
        return workflow;
    }

    private static void UpgradeVersion1ToVersion2(WorkflowEntity workflow)
    {
        foreach (var node in workflow.Nodes.OfType<ModelCallNodeEntity>())
        {
#pragma warning disable CS0618
            if (node.ModelId.HasValue)
            {
                node.Models =
                [
                    new ModelCallModelDefinition
                    {
                        ModelId = node.ModelId.Value,
                        ParameterValues = node.ParameterValues is null ? [] : new Dictionary<string, string?>(node.ParameterValues, StringComparer.Ordinal),
                    },
                ];
            }
            else
            {
                node.Models = [];
            }
#pragma warning restore CS0618

            node.ClearLegacyModelConfiguration();
        }

        workflow.Version = 2;
    }

    private static void ValidateCurrentVersion(WorkflowEntity workflow)
    {
        foreach (var node in workflow.Nodes.OfType<ModelCallNodeEntity>())
        {
            if (node.LegacyModelIdPresent || node.LegacyParameterValuesPresent)
                throw new SharpOMaticException($"Model call node '{node.Title}' contains version 1 model fields in a version {workflow.Version} workflow.");

            if (node.Models.Any(model => model.ModelId == Guid.Empty))
                throw new SharpOMaticException($"Model call node '{node.Title}' contains an empty model identifier.");
        }
    }
}
