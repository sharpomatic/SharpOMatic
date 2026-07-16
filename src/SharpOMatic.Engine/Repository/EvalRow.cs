namespace SharpOMatic.Engine.Repository;

public class EvalRow
{
    public const int DefaultRepeat = 1;
    public const int MinRepeat = 0;
    public const int MaxRepeat = 10000;

    [Key]
    public required Guid EvalRowId { get; set; }
    public required Guid EvalConfigId { get; set; }
    public required int Order { get; set; }
    public int? Repeat { get; set; } = DefaultRepeat;

    [JsonIgnore]
    public int EffectiveRepeat => Repeat ?? DefaultRepeat;
}
