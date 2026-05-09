using UnityEngine;

namespace GameplayCore.MetaData
{
    [CreateAssetMenu(fileName = "AimConfig", menuName = "Gameplay/Configs/AimConfig")]
    public class AimConfig : ScriptableObject
    {
        [Header("General")]
        public float DirectionSlerpSpeed = 24f;
        public float InputThresholdDistance = 1f;
        
        [Header("Positional Smoothing")]
        public int SmoothingTouchPoints = 6;
        public float SmoothingMinDistance = 0.1f;
    }
}