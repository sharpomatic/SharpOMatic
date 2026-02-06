namespace SharpOMatic.Editor.DTO;

public record class AssetSummary(Guid AssetId, string Name, string MediaType, long SizeBytes, AssetScope Scope, DateTime Created);
