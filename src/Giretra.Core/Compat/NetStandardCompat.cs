namespace Giretra.Core.Compat
{
    /// <summary>
    /// Cross-target shim for Random.Shared (net6+), missing from netstandard2.1 (the Unity build).
    /// </summary>
    internal static class RandomCompat
    {
#if NETSTANDARD2_1
        /// <summary>
        /// Shared thread-safe Random. A single locked instance avoids the seed
        /// collisions of per-call new Random() on tick-based seeding.
        /// </summary>
        public static Random Shared { get; } = new LockedRandom();

        private sealed class LockedRandom : Random
        {
            private readonly Random _inner = new();

            public override int Next()
            {
                lock (_inner) return _inner.Next();
            }

            public override int Next(int maxValue)
            {
                lock (_inner) return _inner.Next(maxValue);
            }

            public override int Next(int minValue, int maxValue)
            {
                lock (_inner) return _inner.Next(minValue, maxValue);
            }

            public override double NextDouble()
            {
                lock (_inner) return _inner.NextDouble();
            }

            public override void NextBytes(byte[] buffer)
            {
                lock (_inner) _inner.NextBytes(buffer);
            }
        }
#else
        public static Random Shared => Random.Shared;
#endif
    }

    /// <summary>
    /// Cross-target shim for Enum.GetValues&lt;T&gt;() (net5+).
    /// </summary>
    internal static class EnumCompat
    {
        public static T[] GetValues<T>() where T : struct, Enum
        {
#if NETSTANDARD2_1
            return (T[])Enum.GetValues(typeof(T));
#else
            return Enum.GetValues<T>();
#endif
        }
    }
}

#if NETSTANDARD2_1
namespace System.Linq
{
    /// <summary>
    /// MaxBy/MinBy polyfills (net6+ LINQ operators), compiled only for netstandard2.1.
    /// </summary>
    internal static class EnumerableCompatExtensions
    {
        public static TSource? MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            return ExtremumBy(source, keySelector, 1);
        }

        public static TSource? MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            return ExtremumBy(source, keySelector, -1);
        }

        private static TSource? ExtremumBy<TSource, TKey>(IEnumerable<TSource> source, Func<TSource, TKey> keySelector, int sign)
        {
            var comparer = Comparer<TKey>.Default;
            using var e = source.GetEnumerator();

            if (!e.MoveNext())
            {
                if (default(TSource) is null) return default;
                throw new InvalidOperationException("Sequence contains no elements");
            }

            var best = e.Current;
            var bestKey = keySelector(best);

            while (e.MoveNext())
            {
                var candidateKey = keySelector(e.Current);
                if (Math.Sign(comparer.Compare(candidateKey, bestKey)) == sign)
                {
                    best = e.Current;
                    bestKey = candidateKey;
                }
            }

            return best;
        }
    }
}
#endif
