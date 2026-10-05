using System;
using System.Collections.Generic;
using Random = System.Random;

namespace AK.Utilities
{
    public static class Extensions
    {
        [ThreadStatic] private static Random _shared;

        /// <summary>Shuffles the list in place, with random numbers shared by every call on this thread.</summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            list.Shuffle(_shared ??= new Random());
        }

        /// <summary>Shuffles the list in place (Fisher-Yates), drawing from <paramref name="random"/>.</summary>
        public static void Shuffle<T>(this IList<T> list, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            int count = list.Count;
            while (count > 1)
            {
                --count;
                int index = random.Next(count + 1);
                (list[index], list[count]) = (list[count], list[index]);
            }
        }
    }
}