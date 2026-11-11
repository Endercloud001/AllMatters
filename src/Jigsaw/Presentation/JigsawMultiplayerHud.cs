using Godot;

namespace AllMatters.Jigsaw.Presentation;

// The native top bar remains available; only the overlapping multiplayer portrait row is scoped.
internal sealed class JigsawMultiplayerHud : IDisposable
{
    private readonly Control screen;
    private readonly CanvasItem hud;
    private bool? previousVisibility;
    private bool disposed;

    internal JigsawMultiplayerHud(Control screen, CanvasItem hud)
    {
        this.screen = screen;
        this.hud = hud;
        screen.VisibilityChanged += Update;
        screen.TreeExiting += Dispose;
        Update();
    }

    private void Update()
    {
        if (!GodotObject.IsInstanceValid(hud))
            return;
        if (screen.IsVisibleInTree())
        {
            previousVisibility ??= hud.Visible;
            hud.Hide();
        }
        else
            Restore();
    }

    private void Restore()
    {
        if (previousVisibility is { } previous && GodotObject.IsInstanceValid(hud))
            hud.Visible = previous;
        previousVisibility = null;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        screen.VisibilityChanged -= Update;
        screen.TreeExiting -= Dispose;
        Restore();
    }
}
