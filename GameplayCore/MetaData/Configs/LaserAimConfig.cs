using UnityEngine;

namespace GameplayCore.MetaData
{
    [CreateAssetMenu(fileName = "LaserAimConfig", menuName = "Gameplay/Configs/LaserAimConfig")]
    public class LaserAimConfig : AimConfig
    {
        public float LaserTraceSpeed = 50f;
        public float FadeOutTimePerMeter = 0.05f;
        public float MinFadeOutDuration = 0.2f;
        public float MaxFadeOutDuration = 2.0f;
        
        [Tooltip("If true, uses the AnimationCurve to evaluate the laser trace progress. If false, uses linear interpolation.")]
        public bool UseAnimationCurve = false;
        
        [Tooltip("Animation curve for laser trace progress. X axis represents normalized time (0-1), Y axis represents normalized progress (0-1).")]
        public AnimationCurve LaserTraceCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }
}