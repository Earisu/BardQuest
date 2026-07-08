extern alias yargpkg;
using System.Collections.Concurrent;
using System.Diagnostics;

using BardQuest.Domain.Ratings;

using YARG.Song;                          // SongContainer (Assembly-CSharp)

using yargpkg::YARG.Core.Chart;           // SongChart (runtime)
using yargpkg::YARG.Core.Song;            // SongEntry (runtime)

using Debug = UnityEngine.Debug;
using RtDifficulty = yargpkg::YARG.Core.Difficulty;    // runtime difficulty enum (distinct CLR type)
using RtInstrument = yargpkg::YARG.Core.Instrument;    // runtime instrument enum (distinct CLR type)

namespace BardQuest.Mod.Scan;

// Fire-and-forget rating build. Kicked from the FillContainers seam on Unity's main thread: it
// loads the cache + diffs (cheap, main thread), then parses only-new charts on a background pool.
// Instrument-agnostic: one LoadChart per song fans out to every registered analyzer.
//
// Two-YARG.Core bridge: the DOMAIN (ChartRating, analyzers) speaks the vendored YARG.Core enums;
// the RUNTIME (SongEntry/SongChart) speaks the yargpkg (YARG.Core.Package) enums. They are distinct
// CLR types with identical byte values — convert at the boundary via ToRuntime/ToDomain below.
public static class ScanService
{
    private const int WorkerCount = 16;

    // Registered analyzers (Phase 1: drums only). Adding an instrument = add its analyzer here
    // (and its extractor branch in RateEntry).
    private static readonly IChartRatingAnalyzer[] Analyzers = [new DrumChartRatingAnalyzer()];

    private static int _running; // 0 = idle, 1 = a build is in flight (atomic guard)

    private static RtInstrument ToRuntime(YARG.Core.Instrument i) => (RtInstrument)(byte)i;
    private static YARG.Core.Difficulty ToDomain(RtDifficulty d) => (YARG.Core.Difficulty)(byte)d;

    public static void OnLibraryRefreshed()
    {
        try
        {
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            {
                return; // a build is already draining; the cache diff will catch anything new next time
            }

            Dictionary<string, List<ChartRating>> cache = RatingCacheFile.Load();
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

            RunBuild(cache, work, relevant);
        }
        catch (Exception ex)
        {
            ModLog.Error("Rating scan kickoff failed: " + ex);
            _ = Interlocked.Exchange(ref _running, 0);
        }
    }

    private static bool HasAnyAnalyzerInstrument(SongEntry entry)
    {
        foreach (IChartRatingAnalyzer a in Analyzers)
        {
            if (entry.HasInstrument(ToRuntime(a.Instrument)))
            {
                return true;
            }
        }

        return false;
    }

    // A song needs (re)rating if any registered analyzer's instrument it has lacks a cached rating
    // for its hash. Keeps incrementality instrument-agnostic: a hash cached for drums still gets
    // rated for a later-added instrument.
    private static bool NeedsRating(SongEntry entry, Dictionary<string, List<ChartRating>> cache)
    {
        if (!cache.TryGetValue(entry.Hash.ToString(), out List<ChartRating> ratings))
        {
            return true;
        }

        foreach (IChartRatingAnalyzer a in Analyzers)
        {
            if (!entry.HasInstrument(ToRuntime(a.Instrument)))
            {
                continue;
            }

            bool has = false;
            foreach (ChartRating r in ratings)
            {
                if (r.Instrument == a.Instrument)
                {
                    has = true;
                    break;
                }
            }

            if (!has)
            {
                return true;
            }
        }

        return false;
    }

    private static void RunBuild(
        Dictionary<string, List<ChartRating>> cache, List<SongEntry> work, int relevant)
    {
        var thread = new Thread(() =>
        {
            var sw = Stopwatch.StartNew();
            var queue = new BlockingCollection<SongEntry>();
            var results = new ConcurrentDictionary<string, List<ChartRating>>();
            int rated = 0;
            int failed = 0;

            var workers = new Thread[WorkerCount];
            for (int i = 0; i < WorkerCount; i++)
            {
                workers[i] = new Thread(() =>
                {
                    foreach (SongEntry entry in queue.GetConsumingEnumerable())
                    {
                        try
                        {
                            List<ChartRating> ratings = RateEntry(entry);
                            if (ratings.Count > 0)
                            {
                                results[entry.Hash.ToString()] = ratings;
                                _ = Interlocked.Increment(ref rated);
                            }
                        }
                        catch (Exception ex)
                        {
                            _ = Interlocked.Increment(ref failed);
                            Debug.LogWarning($"[BardQuest] rate failed for {entry.Name}: {ex.Message}");
                        }
                    }
                })
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

            foreach (KeyValuePair<string, List<ChartRating>> kv in results)
            {
                cache[kv.Key] = kv.Value;
            }

            var toWrite = new Dictionary<string, IReadOnlyList<ChartRating>>(cache.Count);
            foreach (KeyValuePair<string, List<ChartRating>> kv in cache)
            {
                toWrite[kv.Key] = kv.Value;
            }

            RatingCacheFile.Save(toWrite);
            sw.Stop();
            ModLog.Info(
                $"Ratings built — {relevant} rated songs, {rated} new rated, {failed} failed, " +
                $"in {sw.Elapsed.TotalSeconds:F1}s (workers={WorkerCount}).");
            _ = Interlocked.Exchange(ref _running, 0);
        })
        { IsBackground = true, Name = "BardQuestRateBuild" };
        thread.Start();
    }

    private static List<ChartRating> RateEntry(SongEntry entry)
    {
        var ratings = new List<ChartRating>();
        SongChart chart = entry.LoadChart();
        if (chart == null)
        {
            return ratings;
        }

        foreach (IChartRatingAnalyzer analyzer in Analyzers)
        {
            RtInstrument runtimeInstrument = ToRuntime(analyzer.Instrument);
            if (!entry.HasInstrument(runtimeInstrument))
            {
                continue;
            }

            int rawIntensity = Intensity(entry, runtimeInstrument);

            // Phase 1: the only registered analyzer is drums, so the drum extractor applies.
            // A future instrument pairs its own extractor with its analyzer here.
            foreach (RtDifficulty diff in DrumChartExtractor.AvailableDifficulties(chart))
            {
                IReadOnlyList<(double Time, int Lane)> hits =
                    DrumChartExtractor.Extract(chart, diff, out double duration);
                if (hits.Count == 0)
                {
                    continue;
                }

                ratings.Add(analyzer.Analyze(hits, duration, rawIntensity, bpm: 0, ToDomain(diff)));
            }
        }

        return ratings;
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
