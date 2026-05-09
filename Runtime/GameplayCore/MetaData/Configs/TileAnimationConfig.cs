using DG.Tweening;
using UnityEngine;

namespace GameplayCore.MetaData
{
    [CreateAssetMenu(fileName = "TileFallAnimationConfig", menuName = "Gameplay/TileFallAnimationConfig")]
    public class TileAnimationConfig : ScriptableObject
    {
        [Header("Procedural Fall")]
        public float DeltaDelayFall             = 0.04f;
        public float ProceduralFallTime = 1f;
        public float MaxThresoldX       = 9;
        public float MinJumpUp = 0;
        public float MaxJumpUp = 0;
        
        [Header("Pop Variables")]
        public float DeltaDelayPop = 0.04f;
        public float PopOutDuration = 0.1f;
        public float PopInDuration = 0.1f;
        public float PopOutMultiplier = 1.5f;
        
        [Header("Bulge Out")]
        public float BulgeOutDeltaDelay     = 0.1f;
        public float BulgeOutDeltaOffset    = 0.1f;
        public float BulgeOutDuration       = 1f;
        
        
        [Header("Falling Scale")]
        public float InitialScale = 0.5f;
        public float InitialScaleDuration = 0.6f;
        public Ease  InitialScaleEase     = Ease.OutQuad;
        public float InitialScaleEnd      = 1.1f;

        [Header("Falling Fade")]
        public float Alpha = 0.6f;
        public float FadeDuration = 0.6f;

        [Header("Starting Reveal")]
        public float StartingRevealTime = 1f;
        public Ease StartingRevealEase = Ease.OutCirc;
        
        
        [Header("Vanish Sequence")]
        public float VanishFadeTime = 0.5f;
        public float VanishFadeDeltaDelay = 0.04f;
    }
}