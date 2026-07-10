extern alias yargpkg;

using System.Collections.Concurrent;
using System.Diagnostics;

using BardQuest.Domain.Ratings;
using BardQuest.Domain.Ratings.Drums;

using YARG.Song;                          // SongContainer (Assembly-CSharp)

using yargpkg::YARG.Core.Chart;           // SongChart (runtime)
using yargpkg::YARG.Core.Song;            // SongEntry (runtime)

using Debug = UnityEngine.Debug;
using RtDifficulty = yargpkg::YARG.Core.Difficulty;    // runtime difficulty enum (distinct CLR type)
using RtInstrument = yargpkg::YARG.Core.Instrument;    // runtime instrument enum (distinct CLR type)

namespace BardQuest.Mod.Scan;

// Fire-and-forget rating build. Kicked from BardQuestManager.OpenCanvas (when the player enters the
// BardQuest screen) on Unity's main thread: it loads the cache + diffs (cheap, main thread), then
// parses only-new charts on a background pool.
// Instrument-agnostic: one LoadChart per song fans out to every registered analyzer.
//
// Two-YARG.Core bridge: the DOMAIN (ChartMetrics, analyzers) speaks the vendored YARG.Core enums;
// the RUNTIME (SongEntry/SongChart) speaks the yargpkg (YARG.Core.Package) enums. They are distinct
// CLR types with identical byte values — convert at the boundary via ToRuntime/ToDomain below.
public static class ScanService
{
    private const int WorkerCount = 16;

    // Registered analyzers (Phase 1: drums only). Adding an instrument = add its analyzer here
    // (and its extractor branch in RateEntry).
    private static readonly IChartAnalyzer[] Analyzers =
        [new DrumChartAnalyzer(YARG.Core.Instrument.ProDrums, DrumKitMap.ProFourLane)];

    private static int _running; // 0 = idle, 1 = a build is in flight (atomic guard)

    private static RtInstrument ToRuntime(YARG.Core.Instrument i) => (RtInstrument)(byte)i;
    private static YARG.Core.Difficulty ToDomain(RtDifficulty d) => (YARG.Core.Difficulty)(byte)d;

    // Kicked (fire-and-forget) when the player opens the BardQuest screen. Loads the cache, diffs it
    // against the already-scanned SongContainer.Songs on the main thread, and spawns the background
    // build only for charts that are missing. A build already in flight, or a warm library with
    // nothing new, returns near-instantly.
    public static void EnsureRatings()
    {
        try
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            {
                return; // a build is already draining; the cache diff will catch anything new next time
            }

            // Resolved here on Unity's main thread — RunBuild's background thread must never touch
            // Application.persistentDataPath (a main-thread-only Unity API) itself.
            string cachePath = RatingCacheFile.Path();
            Dictionary<string, List<ChartMetrics>> cache = RatingCacheFile.Load();
            var work = new List<SongEntry>();
            int relevant = 0;

            foreach (SongEntry entry in SongContainer.Songs)
            {
                if (!HasAnyAnalyzerInstrument(entry))
                {
                    continue;
                }

                relevant++;
                if (NeedsRating(entry, cache))
                {
                    work.Add(entry);
                }
            }

            if (work.Count == 0)
            {
                ModLog.Info($"Ratings warm — {relevant} rated songs, 0 new.");
                _ = Interlocked.Exchange(ref _running, 0);
                return;
            }

            RunBuild(cachePath, cache, work, relevant);
        }
        catch (Exception ex)
        {
            ModLog.Error("Rating scan kickoff failed: " + ex);
            _ = Interlocked.Exchange(ref _running, 0);
        }
    }

    private static bool HasAnyAnalyzerInstrument(SongEntry entry) =>
        Analyzers.Any(a => entry.HasInstrument(ToRuntime(a.Instrument)));

    // A song needs (re)rating if any registered analyzer's instrument it has lacks a cached rating
    // for its hash. Keeps incrementality instrument-agnostic: a hash cached for drums still gets
    // rated for a later-added instrument.
    private static bool NeedsRating(SongEntry entry, Dictionary<string, List<ChartMetrics>> cache)
    {
        if (!cache.TryGetValue(entry.Hash.ToString(), out List<ChartMetrics> ratings))
        {
            return true;
        }

        return (from a in Analyzers
            where entry.HasInstrument(ToRuntime(a.Instrument))
            select ratings.Any(r => r.Instrument == a.Instrument)).Any(has => !has);
    }

    private static void RunBuild(
        string cachePath, Dictionary<string, List<ChartMetrics>> cache, List<SongEntry> work, int relevant)
    {
        var thread = new Thread(() => BuildAndSave(cachePath, cache, work, relevant))
        { IsBackground = true, Name = "BardQuestRateBuild" };
        thread.Start();
    }

    private static void BuildAndSave(
        string cachePath, Dictionary<string, List<ChartMetrics>> cache, List<SongEntry> work, int relevant)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            (ConcurrentDictionary<string, List<ChartMetrics>> results, int rated, int failed) = RateInParallel(work);

            foreach (KeyValuePair<string, List<ChartMetrics>> kv in results)
            {
                cache[kv.Key] = kv.Value;
            }

            SaveCache(cachePath, cache);
            sw.Stop();
            ModLog.Info(
                $"Ratings built — {relevant} rated songs, {rated} new rated, {failed} failed, " +
                $"in {sw.Elapsed.TotalSeconds:F1}s (workers={WorkerCount}).");
        }
        catch (Exception ex)
        {
            ModLog.Error("Rating build failed: " + ex);
        }
        finally
        {
            _ = Interlocked.Exchange(ref _running, 0);
        }
    }

    // Fans work out to WorkerCount consumer threads draining a shared queue, then joins them all.
    private static (ConcurrentDictionary<string, List<ChartMetrics>> Results, int Rated, int Failed) RateInParallel(
        List<SongEntry> work)
    {
        var queue = new BlockingCollection<SongEntry>();
        var results = new ConcurrentDictionary<string, List<ChartMetrics>>();
        var counts = new RateCounts();

        var workers = new Thread[WorkerCount];
        for (int i = 0; i < WorkerCount; i++)
        {
            workers[i] = new Thread(() => DrainQueue(queue, results, counts))
            { IsBackground = true, Name = "BardQuestRate" };
            workers[i].Start();
        }

        foreach (SongEntry entry in work)
        {
            queue.Add(entry);
        }

        queue.CompleteAdding();
        foreach (Thread w in workers)
        {
            w.Join();
        }

        return (results, counts.Rated, counts.Failed);
    }

    // One worker's loop body: rate entries until the queue is drained, tallying successes/failures.
    private static void DrainQueue(
        BlockingCollection<SongEntry> queue,
        ConcurrentDictionary<string, List<ChartMetrics>> results,
        RateCounts counts)
    {
        foreach (SongEntry entry in queue.GetConsumingEnumerable())
        {
            try
            {
                List<ChartMetrics> ratings = RateEntry(entry);
                if (ratings.Count > 0)
                {
                    results[entry.Hash.ToString()] = ratings;
                    _ = Interlocked.Increment(ref counts.Rated);
                }
            }
            catch (Exception ex)
            {
                _ = Interlocked.Increment(ref counts.Failed);
                Debug.LogWarning($"[BardQuest] rate failed for {entry.Name}: {ex.Message}");
            }
        }
    }

    private static void SaveCache(string cachePath, Dictionary<string, List<ChartMetrics>> cache)
    {
        var toWrite = new Dictionary<string, IReadOnlyList<ChartMetrics>>(cache.Count);
        foreach (KeyValuePair<string, List<ChartMetrics>> kv in cache)
        {
            toWrite[kv.Key] = kv.Value;
        }

        RatingCacheFile.Save(cachePath, toWrite);
    }

    // Mutable, heap-allocated so Interlocked can take a ref to a field shared across worker threads.
    private sealed class RateCounts
    {
        public int Rated;
        public int Failed;
    }

    private static List<ChartMetrics> RateEntry(SongEntry entry)
    {
        var metrics = new List<ChartMetrics>();
        SongChart chart = entry.LoadChart();

        foreach (IChartAnalyzer analyzer in Analyzers)
        {
            RtInstrument runtimeInstrument = ToRuntime(analyzer.Instrument);
            if (!entry.HasInstrument(runtimeInstrument))
            {
                continue;
            }

            bool ratedAny = false;

            if (chart != null)
            {
                int intensity = Intensity(entry, runtimeInstrument);
                SyncInfo sync = DrumChartExtractor.BuildSyncInfo(chart);

                foreach (RtDifficulty diff in DrumChartExtractor.AvailableDifficulties(chart))
                {
                    IReadOnlyList<(double Time, int Lane, uint Tick)> notes =
                        DrumChartExtractor.Extract(chart, diff, out double duration);
                    if (notes.Count == 0)
                    {
                        continue;
                    }

                    metrics.Add(analyzer.Analyze(notes, duration, intensity, ToDomain(diff), sync));
                    ratedAny = true;
                }
            }

            if (!ratedAny)
            {
                // Negative-cache marker: the song's metadata claims this instrument, but no rateable
                // chart could be loaded/extracted. Sentinel.Intensity < 0 flags "attempted,
                // unrateable" so NeedsRating won't re-parse this hash every refresh — while a
                // LATER-added analyzer instrument (absent from the cached list entirely) still
                // triggers re-rating.
                metrics.Add(ChartMetrics.Sentinel(analyzer.Instrument));
            }
        }

        return metrics;
    }

    // YARG's per-instrument star for this song (our Tier when >= 1). Confirm entry[instrument].Intensity
    // against YARG.Core SongEntry while implementing; fall back to -1 (unknown -> density fallback tier).
    private static int Intensity(SongEntry entry, RtInstrument instrument)
    {
        try
        {
            return entry[instrument].Intensity;
        }
        catch
        {
            return -1;
        }
    }
}
