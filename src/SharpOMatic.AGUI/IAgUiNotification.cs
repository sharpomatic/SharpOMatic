namespace SharpOMatic.AGUI;

public interface IAgUiNotification
{
    public Task OnRunStartingAsync(AgUiRunContextNotification notification) => Task.CompletedTask;
}
