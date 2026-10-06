using System.Collections.Generic;
using AK.Core;
using AK.Tests.Support;
using AK.Tutorials;
using NUnit.Framework;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests.Tutorials
{
	/// <summary>
	/// A UITarget is registered exactly while it is enabled, so a target in a hidden or pooled
	/// view can't be pointed at. Injection that arrives after it was enabled registers it at
	/// once, and with no registry to register with it does nothing.
	/// </summary>
	public class UITargetPlayModeTests
	{
		private readonly List<Object> _created = new();

		private UITargetRegistry _registry;
		private Container        _container;

		[SetUp]
		public void SetUp()
		{
			_registry = new UITargetRegistry();
			_container = new ContainerBuilder()
			             .SetName(nameof(UITargetPlayModeTests))
			             .RegisterValue(_registry, new[] { typeof(IUITargetRegistry) })
			             .Build();
		}

		[TearDown]
		public void TearDown()
		{
			foreach (Object o in _created)
			{
				if (o != null) Object.Destroy(o);
			}

			_created.Clear();
			_container.Dispose();
		}

		private UITargetId MakeId()
		{
			var id = ScriptableObject.CreateInstance<UITargetId>();
			_created.Add(id);
			LiveUISystem.SetField(id, "_id", Uid.NewRandom());
			return id;
		}

		/// <summary>A target inside a view-like parent, enabled or not.</summary>
		private UITarget MakeTarget(UITargetId id, bool active)
		{
			var view = new GameObject("View", typeof(RectTransform));
			_created.Add(view);

			var go = new GameObject("Target", typeof(RectTransform));
			go.SetActive(false);
			go.transform.SetParent(view.transform, false);

			var target = go.AddComponent<UITarget>();
			LiveUISystem.SetField(target, "_id", id);
			go.SetActive(active);
			return target;
		}

		private Transform Registered(UITargetId id) => _registry.TryGet(id, out RectTransform target) ? target : null;

		[Test]
		public void ATarget_IsRegisteredExactlyWhileItIsEnabled()
		{
			UITargetId id = MakeId();
			UITarget target = MakeTarget(id, active: false);
			AttributeInjector.Inject(target, _container);
			GameObject view = target.transform.parent.gameObject;

			Assert.That(Registered(id), Is.Null, "an inactive target is not registered");

			target.gameObject.SetActive(true);
			Assert.That(Registered(id), Is.SameAs(target.transform), "enabled, it is");

			target.enabled = false;
			Assert.That(Registered(id), Is.Null, "disabled, it is not");

			target.enabled = true;
			view.SetActive(false);
			Assert.That(Registered(id), Is.Null, "nor while its view is hidden");

			view.SetActive(true);
			Assert.That(Registered(id), Is.SameAs(target.transform), "and it is again once the view is back");
		}

		[Test]
		public void ATarget_InjectedAfterItWasEnabled_RegistersAtOnce()
		{
			// A view is enabled when it is instantiated, and injected after.
			UITargetId id = MakeId();
			UITarget target = MakeTarget(id, active: true);
			Assert.That(Registered(id), Is.Null, "nothing to register with yet");

			AttributeInjector.Inject(target, _container);

			Assert.That(Registered(id), Is.SameAs(target.transform));
		}

		[Test]
		public void ATarget_WithNoRegistryToRegisterWith_DoesNothing()
		{
			Container bare = new ContainerBuilder().SetName("NoRegistry").Build();
			try
			{
				UITarget target = MakeTarget(MakeId(), active: true);

				Assert.DoesNotThrow(() =>
				{
					AttributeInjector.Inject(target, bare);
					target.enabled = false;
					target.enabled = true;
				});
			}
			finally
			{
				bare.Dispose();
			}
		}
	}
}
