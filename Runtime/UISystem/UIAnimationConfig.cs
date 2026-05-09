using AK.UISystem.Animations;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AK.UISystem
{
	[CreateAssetMenu(fileName = "UIAnimationConfig", menuName = "AK/UI/UIAnimationConfig")]
	public class UIAnimationConfig : ScriptableObject
	{
		[Title("Animation Strategy")]
		[InlineEditor, SerializeField]
		private AnimationStrategy _animationStrategy;

		[Title("Animation Properties")]
		[SerializeField]
		private bool _playInParallelWithPrevious;

		public IAnimationStrategy AnimationStrategy          => _animationStrategy;
		public bool               PlayInParallelWithPrevious => _playInParallelWithPrevious;
		public bool               NoAnimation                => _animationStrategy == null;
	}
}