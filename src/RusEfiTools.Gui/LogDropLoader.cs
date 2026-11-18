using FishGfx.Graphics;

namespace RusEfiTools.Gui;

internal sealed class LogDropLoader : IDisposable
{
    private readonly RenderWindow window;
    private readonly MainView view;
    private CancellationTokenSource? cancellation;
    private Task<LoadResult>? pending;

    public LogDropLoader(RenderWindow window, MainView view)
    {
        this.window = window;
        this.view = view;
        window.FilesDropped += OnFilesDropped;
    }

    private void OnFilesDropped(object? sender, FilesDroppedEventArgs args)
    {
        if (args.Paths.Count != 1)
        {
            view.SetStatus("Drop one log at a time. The current graphs have been kept.");
            return;
        }

        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        string path = args.Paths[0];
        string[] channelNames = view.RequestedChannels;
        view.SetStatus($"Loading {Path.GetFileName(path)}…");
        // No UI or OpenGL calls on the worker thread; completion is applied in Tick.
        pending = Task.Run(() =>
        {
            try
            {
                return new LoadResult(Path.GetFileName(path), MlgLogReader.Load(path, token, channelNames), null);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or InvalidDataException or OperationCanceledException or ArgumentException)
            {
                return new LoadResult(Path.GetFileName(path), null, exception.Message);
            }
        });
    }

    public void Tick()
    {
        if (pending == null || !pending.IsCompleted)
        {
            return;
        }
        var completed = pending;
        pending = null;
        if (completed.IsFaulted)
        {
            view.SetStatus($"Could not load log: {completed.Exception!.GetBaseException().Message}");
            return;
        }
        var result = completed.Result;
        if (result.Log == null)
        {
            view.SetStatus($"Could not load {result.Name}: {result.Error}");
        }
        else
        {
            view.Load(result.Log, result.Name);
        }
    }

    public void Dispose()
    {
        window.FilesDropped -= OnFilesDropped;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private sealed record LoadResult(string Name, LoadedLog? Log, string? Error);
}
