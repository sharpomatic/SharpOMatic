namespace SharpOMatic.Engine.Helpers;

public class DebugInformationHelper
{
    private readonly Trace _trace;
    private readonly List<Information> _informations;

    public DebugInformationHelper(Trace trace, List<Information> informations)
    {
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        _informations = informations ?? throw new ArgumentNullException(nameof(informations));
    }

    public void Add(string text, string? data = null)
    {
        var normalizedText = text?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
            throw new SharpOMaticException("Debug information text cannot be empty.");

        _informations.Add(
            new Information()
            {
                InformationId = Guid.NewGuid(),
                TraceId = _trace.TraceId,
                RunId = _trace.RunId,
                Created = DateTime.Now,
                InformationType = InformationType.Debug,
                Text = normalizedText,
                Data = string.IsNullOrWhiteSpace(data) ? null : data,
            }
        );
    }
}
