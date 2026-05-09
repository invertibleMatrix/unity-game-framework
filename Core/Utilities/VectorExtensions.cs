using System.Collections.Generic;
using UnityEngine;

namespace AK.Utilities
{
    public static class VectorExtensions
    {
        public static Vector3 Average(this IEnumerable<Vector3> vectors)
        {
            Vector3 sum   = Vector3.zero;
            int     count = 0;

            foreach (Vector3 vector in vectors)
            {
                sum += vector;
                count++;
            }

            if (count > 0)
            {
                return sum / count;
            }
            else
            {
                return Vector3.zero;
            }
        }

        public static Vector2 Average(this IEnumerable<Vector2> vectors)
        {
            Vector2 sum   = Vector3.zero;
            int     count = 0;

            foreach (Vector2 vector in vectors)
            {
                sum += vector;
                count++;
            }

            if (count > 0)
            {
                return sum / count;
            }
            else
            {
                return Vector2.zero;
            }
        }

        public static Vector3Int ToVector3Int(this Vector3 vector, int precisionFactor = 10000)
        {
            return new Vector3Int(
                                  Mathf.RoundToInt(vector.x * precisionFactor),
                                  Mathf.RoundToInt(vector.y * precisionFactor),
                                  Mathf.RoundToInt(vector.z * precisionFactor)
                                 );
        }

        public static Vector3 ToVector3(this Vector3Int vectorInt, int precisionFactor = 10000)
        {
            return new Vector3(
                               vectorInt.x / (float)precisionFactor,
                               vectorInt.y / (float)precisionFactor,
                               vectorInt.z / (float)precisionFactor
                              );
        }
    }
}