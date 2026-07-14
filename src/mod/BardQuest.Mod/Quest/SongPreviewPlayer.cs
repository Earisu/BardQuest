extern alias yargpkg;

using System.Threading;
using System.Threading.Tasks;

using YARG;          // GlobalVariables (SongSpeed)
using YARG.Settings; // SettingsManager (PreviewVolume)

using PreviewContext = yargpkg::YARG.Core.Audio.PreviewContext;
using RtSongEntry = yargpkg::YARG.Core.Song.SongEntry;

namespace BardQuest.Mod.Quest;

// Plays a song's audio preview while its monster is highlighted in the Hub, mirroring YARG's Music Library
// sidebar. Each new selection cancels/disposes the previous preview; PreviewContext.Create debounces via
// its delay and honors the cancellation token, so rapid Up/Down scrolling only previews the settled
// selection. Fire-and-forget async, following YARG's own MusicLibraryMenu preview lifecycle.
public sealed class SongPreviewPlayer : IDisposable
{
    private const double PreviewDelaySeconds = 0.5;  // debounce while scrolling
    private const double FadeDurationSeconds = 1.25; // matches YARG's library

    private PreviewContext? _context;
    private CancellationTokenSource? _canceller;
    private string? _currentHash;

    // Preview the song for this hash. No-op if it is already the previewing song; silently does nothing if
    // the song is not in the library or the user's preview volume is 0.
    public void Play(string songHashHex)
    {
        if (string.Equals(songHashHex, _currentHash, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Teardown();
        _currentHash = songHashHex;

        RtSongEntry? entry = SongCatalog.ByHash(songHashHex);
        if (entry == null)
        {
            return;
        }

        float volume = SettingsManager.Settings.PreviewVolume.Value;
        if (volume <= 0f)
        {
            return;
        }

        var canceller = new CancellationTokenSource();
        _canceller = canceller;
        _ = StartAsync(entry, volume, canceller);
    }

    // Stop any preview and forget the current song, so re-selecting it later replays. Called on Fight and
    // Back and when the Hub is left, so preview audio never bleeds into gameplay or lingers off-screen.
    public void Stop()
    {
        Teardown();
        _currentHash = null;
    }

    public void Dispose() => Stop();

    private async Task StartAsync(RtSongEntry entry, float volume, CancellationTokenSource canceller)
    {
        try
        {
            PreviewContext? context = await PreviewContext.Create(
                entry, volume, (float)GlobalVariables.State.SongSpeed,
                PreviewDelaySeconds, FadeDurationSeconds, canceller.Token);
            if (context == null)
            {
                return;
            }

            // A newer selection (or a Stop) may have superseded us while Create was loading; if so this
            // preview is stale and must dispose itself rather than start playing over the current one.
            if (_canceller == canceller && !canceller.IsCancellationRequested)
            {
                _context = context;
            }
            else
            {
                context.Dispose();
            }
        }
        catch (Exception ex)
        {
            ModLog.Warn("Song preview failed: " + ex.Message);
        }
    }

    private void Teardown()
    {
        _canceller?.Cancel();
        _canceller?.Dispose();
        _canceller = null;
        _context?.Dispose();
        _context = null;
    }
}
