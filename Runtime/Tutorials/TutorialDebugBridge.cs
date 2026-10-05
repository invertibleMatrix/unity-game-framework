using System;
using System.Threading;
using AK.CoreDomain.Facts;
using AK.Services.Facts;
using AK.Systems;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace AK.Tutorials
{
	/// <summary>
	/// Editor-facing handle onto the live tutorial services. The host wires it at binding
	/// time with <see cref="Configure"/>, and the TutorialProvider inspector drives it to
	/// force tutorials: Count(ProgressFact) is the step pointer, so running from step N is a
	/// SetCount plus recording the step's missing condition facts. PresentDirectly runs the
	/// provider's RunDueAsync on the spot; PresentViaChain defers to the host's chain kick.
	///
	/// Editor only. In a player build, calls to <see cref="Configure"/> compile away with
	/// their arguments, and the rest of the bridge does not exist.
	/// </summary>
	public static class TutorialDebugBridge
	{
		/// <summary>
		/// Binds the bridge to the live services. <paramref name="chainKick"/> kicks the host's
		/// presentation arbiter after the facts are forced; without one, chain-present is
		/// unavailable.
		/// </summary>
		[System.Diagnostics.Conditional("UNITY_EDITOR")]
		public static void Configure(IFactService facts, IUISystem uiSystem, IUITargetRegistry targets, Action chainKick = null)
		{
#if UNITY_EDITOR
			_facts = facts;
			_uiSystem = uiSystem;
			_targets = targets;
			_chainKick = chainKick;
#endif
		}

#if UNITY_EDITOR
		private static IFactService      _facts;
		private static IUISystem         _uiSystem;
		private static IUITargetRegistry _targets;
		private static Action            _chainKick;

		public static bool IsReady => _facts != null && _uiSystem != null && _targets != null;
		public static bool CanPresentViaChain => _chainKick != null;

		/// <summary>Read-only count passthrough for the inspector's condition breakdown.</summary>
		public static int Count(FactType fact)
		{
			return _facts != null && fact != null ? _facts.Count(fact) : 0;
		}

		/// <summary>Forces the provider due and hands off to the host's chain kick.</summary>
		public static void PresentViaChain(TutorialProvider provider, int fromStep, bool forceAllSteps)
		{
			if (!IsReady || !ForceDue(provider, fromStep, forceAllSteps))
			{
				return;
			}

			_chainKick?.Invoke();
		}

		/// <summary>
		/// Forces the provider due and presents it on the spot, bypassing any host
		/// arbiter. The provider's own EnabledGate kill-switch still applies. Re-Init
		/// abandons a run of the same provider still in flight, so forcing mid-presentation
		/// restarts it cleanly.
		/// </summary>
		public static void PresentDirectly(TutorialProvider provider, int fromStep, bool forceAllSteps)
		{
			if (!IsReady || !ForceDue(provider, fromStep, forceAllSteps))
			{
				return;
			}

			provider.Init(_facts, _uiSystem, _targets);
			provider.RunDueAsync(CancellationToken.None).Forget();
		}

		public static void ResetProgress(TutorialProvider provider)
		{
			if (provider == null || provider.ProgressFact == null || _facts == null)
			{
				return;
			}

			_facts.SetCount(provider.ProgressFact, 0);
		}

		public static void ResetAllFacts()
		{
			_facts?.ResetAll();
		}

		private static bool ForceDue(TutorialProvider provider, int fromStep, bool forceAllSteps)
		{
			if (provider == null || provider.ProgressFact == null || provider.Steps == null || provider.Steps.Count == 0)
			{
				return false;
			}

			int from = Mathf.Clamp(fromStep, 0, provider.Steps.Count - 1);

			using (_facts.BeginBatch())
			{
				_facts.SetCount(provider.ProgressFact, from);

				// "All" satisfies every step's conditions; a specific step satisfies only
				// its own, so later steps stay gated by their real conditions.
				int last = forceAllSteps ? provider.Steps.Count - 1 : from;
				for (int i = from; i <= last; i++)
				{
					var step = provider.Steps[i];
					if (step?.Conditions == null)
					{
						continue;
					}

					foreach (var condition in step.Conditions)
					{
						if (condition == null || condition.Type == null)
						{
							continue;
						}

						int missing = condition.MinCount - _facts.Count(condition.Type);
						for (int j = 0; j < missing; j++)
						{
							_facts.Record(condition.Type);
						}
					}
				}
			}

			return true;
		}
#endif
	}
}
