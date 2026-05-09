using System.Collections.Generic;
using Random = System.Random;

namespace AK.Utilities
{
    public static class Extensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            Random random = new Random();
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