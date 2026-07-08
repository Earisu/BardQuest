// Polyfill for Enumerable.MaxBy/MinBy (introduced in .NET 6's System.Linq), which netstandard2.1's
// BCL doesn't ship. Compiled only into the netstandard2.1 output — see the Compile/netstandard2.1
// condition in BardQuest.Domain.csproj. Behavior matches the BCL contract, including empty-source
// handling: a non-nullable value-type TSource throws InvalidOperationException on an empty source,
// while a reference/nullable TSource returns default(TSource) (null). Ties: first-encountered wins
// (the BCL does not guarantee which of equal keys wins either way).
namespace System.Linq;

internal static class EnumerableMaxByMinByPolyfill
{
    public static TSource? MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        => MaxBy(source, keySelector, comparer: null);

    public static TSource? MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (keySelector is null)
        {
            throw new ArgumentNullException(nameof(keySelector));
        }

        comparer ??= Comparer<TKey>.Default;

        using IEnumerator<TSource> e = source.GetEnumerator();
        if (!e.MoveNext())
        {
            return default(TSource) is null ? default : throw new InvalidOperationException("Sequence contains no elements");
        }

        TSource best = e.Current;
        TKey bestKey = keySelector(best);
        while (e.MoveNext())
        {
            TSource candidate = e.Current;
            TKey candidateKey = keySelector(candidate);
            if (comparer.Compare(candidateKey, bestKey) > 0)
            {
                best = candidate;
                bestKey = candidateKey;
            }
        }
        return best;
    }

    public static TSource? MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        => MinBy(source, keySelector, comparer: null);

    public static TSource? MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (keySelector is null)
        {
            throw new ArgumentNullException(nameof(keySelector));
        }

        comparer ??= Comparer<TKey>.Default;

        using IEnumerator<TSource> e = source.GetEnumerator();
        if (!e.MoveNext())
        {
            return default(TSource) is null ? default : throw new InvalidOperationException("Sequence contains no elements");
        }

        TSource best = e.Current;
        TKey bestKey = keySelector(best);
        while (e.MoveNext())
        {
            TSource candidate = e.Current;
            TKey candidateKey = keySelector(candidate);
            if (comparer.Compare(candidateKey, bestKey) < 0)
            {
                best = candidate;
                bestKey = candidateKey;
            }
        }
        return best;
    }
}
