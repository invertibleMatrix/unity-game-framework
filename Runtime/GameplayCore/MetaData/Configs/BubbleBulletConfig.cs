using UnityEngine;

namespace GameplayCore.MetaData
{
    [CreateAssetMenu(fileName = "BubbleBulletConfig", menuName = "Gameplay/BubbleBulletConfig")]
    public class BubbleBulletConfig : ScriptableObject
    {
        public int   MaxBounce        = 5;
        public float CircleCastRadius = 0.225f;
        public float DefaultRadius    = 0.45f;
        public float Speed            = 25f;
    }
}