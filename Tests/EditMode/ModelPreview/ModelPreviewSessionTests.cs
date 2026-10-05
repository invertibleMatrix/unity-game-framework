using System;
using System.Collections.Generic;
using System.Threading;
using AK.Kernel.Timing;
using AK.Tests.Support;
using AK.Utilities.Previews;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AK.Tests.Previews
{
	/// <summary>
	/// Session behaviour against a fake asset source that counts claims. Loads either land at
	/// once or wait for the test, and UniTask runs continuations as soon as a load lands, so
	/// every interleaving here is deterministic. Intros are off: they need DOTween's player loop.
	/// </summary>
	public class ModelPreviewSessionTests
	{
		private static readonly ModelPreviewOptions NoIntro = new() { Intro = ModelPreviewIntro.None };

		private readonly List<Object> _objects = new();
		private readonly List<ModelPreviewSession> _sessions = new();
		private readonly HashSet<ModelPreviewCamera> _preexisting = new();

		private ModelPreviewCamera _stage;
		private GameObject _cube;
		private GameObject _sphere;
		private GameObject _capsule;
		private FakePreviewAssets _assets;
		private ModelPreviewStageSpace _space;

		[SetUp]
		public void SetUp()
		{
			_preexisting.UnionWith(Object.FindObjectsByType<ModelPreviewCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None));

			_stage = PreviewRig.CreateStage();
			Track(_stage.gameObject);

			_cube    = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
			_sphere  = Track(GameObject.CreatePrimitive(PrimitiveType.Sphere));
			_capsule = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));

			_assets = new FakePreviewAssets(_stage.gameObject);
			_assets.Models.Add("cube", _cube);
			_assets.Models.Add("sphere", _sphere);
			_assets.Models.Add("capsule", _capsule);
			_assets.Models.Add("empty", Track(new GameObject("NoRenderers")));

			_space = new ModelPreviewStageSpace();
		}

		[TearDown]
		public void TearDown()
		{
			foreach (ModelPreviewSession session in _sessions)
			{
				session.Dispose();
			}

			// Booths a failed test left behind.
			foreach (ModelPreviewCamera booth in Booths())
			{
				Object.DestroyImmediate(booth.gameObject);
			}

			foreach (Object created in _objects)
			{
				if (created != null)
				{
					Object.DestroyImmediate(created);
				}
			}

			_sessions.Clear();
			_objects.Clear();
			_preexisting.Clear();
		}

		// ------------------------------------------------------------------ ownership

		[Test]
		public void Load_ByAddress_ShowsTheModel_AndHoldsOneClaimEach()
		{
			ModelPreviewSession session = Session();
			RawImage image = Image("Target");

			ModelPreview preview = Done(session.LoadAsync("a", "cube", image, options: NoIntro));

			Assert.IsNotNull(preview);
			Assert.IsTrue(preview.IsValid);
			Assert.AreEqual("a", preview.Key);
			Assert.IsTrue(preview.OwnsTexture);
			Assert.AreSame(preview.Texture, image.texture);
			Assert.AreEqual(1, session.Count);
			Assert.IsTrue(session.TryGet("a", out ModelPreview found));
			Assert.AreSame(preview, found);
			Assert.AreEqual(1, _assets.ClaimsOn(_stage.gameObject));
			Assert.AreEqual(1, _assets.ClaimsOn(_cube));

			ModelPreviewCamera booth = SingleBooth();
			Assert.AreEqual(PreviewRig.ModelLayer, ModelIn(booth).layer, "models render on the stage's model layer");
			Assert.AreSame(preview.Texture, booth.Camera.targetTexture);
			Assert.IsTrue(booth.Camera.enabled, "a live booth renders");
			Assert.AreEqual(TimeDomain.Unscaled, booth.TimeDomain, "previews run on unscaled time by default");
		}

		[Test]
		public void Dispose_GivesBackEveryClaim_AndDestroysEveryBooth()
		{
			ModelPreviewSession session = Session();
			ModelPreview a = Done(session.LoadAsync("a", "cube", options: NoIntro));
			ModelPreview b = Done(session.LoadAsync("b", "sphere", options: NoIntro));
			RenderTexture texture = a.Texture;

			session.Dispose();

			Assert.IsTrue(session.IsDisposed);
			Assert.AreEqual(0, _assets.OutstandingClaims);
			Assert.AreEqual(0, session.Count);
			Assert.AreEqual(0, _space.Count, "slots are freed");
			Assert.IsEmpty(Booths());
			Assert.IsFalse(a.IsValid);
			Assert.IsFalse(b.IsValid);
			Assert.IsTrue(texture == null, "a session-created texture is destroyed with its booth");
		}

		[Test]
		public void CallerPrefab_IsNeverReleased_AndCallerTextureNeverDestroyed()
		{
			ModelPreviewSession session = Session();
			RenderTexture texture = Track(new RenderTexture(64, 64, 16));

			ModelPreview preview = Done(session.LoadAsync("a", _cube, renderTexture: texture, options: NoIntro));

			Assert.IsFalse(preview.OwnsTexture);
			Assert.AreEqual(0, _assets.ModelLoads);

			session.Dispose();

			Assert.AreEqual(0, _assets.ReleasesOf(_cube));
			Assert.AreEqual(1, _assets.ReleasesOf(_stage.gameObject));
			Assert.IsTrue(texture != null, "the caller's texture survives");
		}

		[Test]
		public void Release_ClosesTheBooth_AndAStaleHandleCannotCloseItsSuccessor()
		{
			ModelPreviewSession session = Session();
			ModelPreview first = Done(session.LoadAsync("a", "cube", options: NoIntro));

			session.Release("a");

			Assert.IsFalse(first.IsValid);
			Assert.AreEqual(0, _assets.OutstandingClaims);
			Assert.IsEmpty(Booths());

			ModelPreview second = Done(session.LoadAsync("a", "cube", options: NoIntro));
			first.Dispose();

			Assert.IsTrue(second.IsValid, "disposing a stale preview must not close the new booth under its key");
			Assert.AreEqual(1, _assets.ClaimsOn(_cube));
		}

		[Test]
		public void Decoration_IsLentToTheBooth_AndReturnedOnDispose()
		{
			ModelPreviewSession session = Session();
			GameObject owner = Track(new GameObject("Spawner"));
			GameObject decoration = Track(new GameObject("Sparkle"));
			decoration.transform.SetParent(owner.transform);

			ModelPreview preview = Done(session.LoadAsync("a", "cube", options: NoIntro));
			preview.Attach(decoration, changeLayer: true);

			Assert.AreSame(SingleBooth().transform, decoration.transform.parent);
			Assert.AreEqual(PreviewRig.ModelLayer, decoration.layer);

			session.Dispose();

			Assert.IsTrue(decoration != null, "decorations stay the caller's");
			Assert.AreSame(owner.transform, decoration.transform.parent);
			Assert.AreEqual(0, decoration.layer);
		}

		// ------------------------------------------------------------------ same key again

		[Test]
		public void SameKey_SameModel_OnlyRebinds()
		{
			ModelPreviewSession session = Session();
			RawImage before = Image("Before");
			RawImage after = Image("After");

			ModelPreview first = Done(session.LoadAsync("a", "cube", before, options: NoIntro));
			ModelPreview again = Done(session.LoadAsync("a", "cube", after, options: NoIntro));

			Assert.AreSame(first, again);
			Assert.AreEqual(1, _assets.ModelLoads);
			Assert.AreSame(first.Texture, after.texture);
			Assert.IsNull(before.texture, "the texture moves to the new image");
		}

		[Test]
		public void SameKey_OtherModel_SwapsIt_AndGivesBackThePreviousClaim()
		{
			ModelPreviewSession session = Session();
			ModelPreview first = Done(session.LoadAsync("a", "cube", options: NoIntro));

			ModelPreview swapped = Done(session.LoadAsync("a", "sphere", options: NoIntro));

			Assert.AreSame(first, swapped, "the booth and its handle stay");
			StringAssert.StartsWith("Sphere", ModelIn(SingleBooth()).name);
			Assert.AreEqual(0, _assets.ClaimsOn(_cube));
			Assert.AreEqual(1, _assets.ClaimsOn(_sphere));
			Assert.AreEqual(1, _assets.ClaimsOn(_stage.gameObject));
		}

		[Test]
		public void SameKey_OtherTexture_RebuildsTheBooth()
		{
			ModelPreviewSession session = Session();
			RenderTexture firstTexture = Track(new RenderTexture(64, 64, 16));
			RenderTexture secondTexture = Track(new RenderTexture(64, 64, 16));

			ModelPreview first = Done(session.LoadAsync("a", "cube", renderTexture: firstTexture, options: NoIntro));
			ModelPreview second = Done(session.LoadAsync("a", "cube", renderTexture: secondTexture, options: NoIntro));

			Assert.AreNotSame(first, second);
			Assert.IsFalse(first.IsValid);
			Assert.IsTrue(second.IsValid);
			Assert.AreSame(secondTexture, SingleBooth().Camera.targetTexture);
			Assert.AreEqual(1, _assets.ClaimsOn(_cube));
			Assert.AreEqual(1, _assets.ClaimsOn(_stage.gameObject));
			Assert.IsTrue(firstTexture != null);
		}

		[Test]
		public void TextureUsedByAnotherKey_IsRefused()
		{
			ModelPreviewSession session = Session();
			RenderTexture texture = Track(new RenderTexture(64, 64, 16));
			Done(session.LoadAsync("a", "cube", renderTexture: texture, options: NoIntro));

			using (ExpectedLog.Error("already used by preview 'a'"))
			{
				Assert.IsNull(Done(session.LoadAsync("b", "sphere", renderTexture: texture, options: NoIntro)));
			}

			Assert.AreEqual(1, session.Count);
			Assert.AreEqual(0, _assets.ClaimsOn(_sphere));
		}

		[Test]
		public void UpdateAsync_SwapsTheModel_KeepingTheBooth()
		{
			ModelPreviewSession session = Session();
			ModelPreview preview = Done(session.LoadAsync("a", "cube", options: NoIntro));

			Assert.IsTrue(Done(session.UpdateAsync("a", "capsule")));

			Assert.IsTrue(preview.IsValid);
			StringAssert.StartsWith("Capsule", ModelIn(SingleBooth()).name);
			Assert.AreEqual(0, _assets.ClaimsOn(_cube));
			Assert.AreEqual(1, _assets.ClaimsOn(_capsule));

			Assert.IsTrue(Done(session.UpdateAsync("a", "capsule")), "the same model is already showing");
			Assert.AreEqual(2, _assets.ModelLoads, "so it isn't loaded again");
		}

		[Test]
		public void UpdateAsync_UnknownKey_LogsAndReturnsFalse()
		{
			ModelPreviewSession session = Session();

			using (ExpectedLog.Error("no preview 'missing' to update"))
			{
				Assert.IsFalse(Done(session.UpdateAsync("missing", "cube")));
			}

			Assert.AreEqual(0, _assets.ModelLoads);
		}

		// ------------------------------------------------------------------ concurrency

		[Test]
		public void DifferentKeys_LoadInParallel_InSeparateSpots()
		{
			_assets.Deferred = true;
			ModelPreviewSession session = Session();

			UniTask<ModelPreview> a = session.LoadAsync("a", "cube", options: NoIntro);
			UniTask<ModelPreview> b = session.LoadAsync("b", "sphere", options: NoIntro);

			Assert.AreEqual(2, _assets.PendingCount, "both keys load at once");

			_assets.CompleteAll();

			Assert.IsTrue(Done(a).IsValid);
			Assert.IsTrue(Done(b).IsValid);

			List<ModelPreviewCamera> booths = Booths();
			Assert.AreEqual(2, booths.Count);
			Assert.AreNotEqual(booths[0].transform.position, booths[1].transform.position);
		}

		[Test]
		public void SameKey_OperationsRunInCallOrder()
		{
			_assets.Deferred = true;
			ModelPreviewSession session = Session();

			UniTask<ModelPreview> load = session.LoadAsync("a", "cube", options: NoIntro);
			UniTask<bool> update = session.UpdateAsync("a", "sphere");

			Assert.AreEqual(1, _assets.PendingCount, "the update waits for the load");
			Assert.AreSame(_stage.gameObject, _assets.NextPending);
			_assets.CompleteNext();

			Assert.AreSame(_cube, _assets.NextPending);
			_assets.CompleteNext();

			Assert.IsNotNull(Done(load));
			AssertPending(update);
			Assert.AreSame(_sphere, _assets.NextPending, "the update starts once the load is done");
			_assets.CompleteNext();

			Assert.IsTrue(Done(update));
			StringAssert.StartsWith("Sphere", ModelIn(SingleBooth()).name);
			Assert.AreEqual(0, _assets.ClaimsOn(_cube));
			Assert.AreEqual(1, _assets.ClaimsOn(_sphere));
		}

		[Test]
		public void CallerCancel_WhileQueued_KeepsTheRestInOrder()
		{
			_assets.Deferred = true;
			ModelPreviewSession session = Session();
			using var cancel = new CancellationTokenSource();

			UniTask<ModelPreview> load = session.LoadAsync("a", "cube", options: NoIntro);
			UniTask<bool> skipped = session.UpdateAsync("a", "sphere", cancel.Token);
			UniTask<bool> last = session.UpdateAsync("a", "capsule");

			cancel.Cancel();

			AssertCanceled(skipped);
			AssertPending(last);
			Assert.AreEqual(1, _assets.PendingCount, "the last update still waits for the load");

			_assets.CompleteAll();

			Assert.IsNotNull(Done(load));
			Assert.IsTrue(Done(last));
			StringAssert.StartsWith("Capsule", ModelIn(SingleBooth()).name);
			Assert.AreEqual(2, _assets.ModelLoads, "the cancelled update never loaded");
			Assert.AreEqual(0, _assets.ClaimsOn(_sphere));
			Assert.AreEqual(1, _assets.ClaimsOn(_capsule));
		}

		[Test]
		public void CallerCancel_MidLoad_GivesBackClaims_AndFreesTheKey()
		{
			_assets.Deferred = true;
			ModelPreviewSession session = Session();
			using var cancel = new CancellationTokenSource();

			UniTask<ModelPreview> load = session.LoadAsync("a", "cube", options: NoIntro, cancellation: cancel.Token);
			_assets.CompleteNext(); // the stage lands; the model is loading
			cancel.Cancel();

			AssertCanceled(load);
			Assert.AreEqual(0, _assets.OutstandingClaims);
			Assert.AreEqual(0, _assets.PendingCount);

			_assets.Deferred = false;
			Assert.IsTrue(Done(session.LoadAsync("a", "cube", options: NoIntro)).IsValid);
		}

		[Test]
		public void AlreadyCancelledToken_TouchesNothing()
		{
			ModelPreviewSession session = Session();
			using var cancel = new CancellationTokenSource();
			cancel.Cancel();

			AssertCanceled(session.LoadAsync("a", "cube", options: NoIntro, cancellation: cancel.Token));

			Assert.AreEqual(0, _assets.StageLoads);
			Assert.AreEqual(0, session.Count);
		}

		[Test]
		public void Dispose_MidLoad_CancelsIt_AndLeavesNothingBehind()
		{
			_assets.Deferred = true;
			ModelPreviewSession session = Session();
			UniTask<ModelPreview> load = session.LoadAsync("a", "cube", options: NoIntro);
			_assets.CompleteNext(); // the stage lands; the model is loading

			session.Dispose();

			AssertCanceled(load);
			Assert.AreEqual(0, _assets.PendingCount);
			Assert.AreEqual(0, _assets.OutstandingClaims, "the stage claim went back");
			Assert.AreEqual(0, _space.Count);
			Assert.IsEmpty(Booths());
		}

		[Test]
		public void Release_MidLoad_CancelsThatKeyOnly()
		{
			_assets.Deferred = true;
			ModelPreviewSession session = Session();
			UniTask<ModelPreview> a = session.LoadAsync("a", "cube", options: NoIntro);
			UniTask<ModelPreview> b = session.LoadAsync("b", "sphere", options: NoIntro);

			session.Release("a");

			AssertCanceled(a);
			AssertPending(b);

			_assets.CompleteAll();

			Assert.IsTrue(Done(b).IsValid);
			Assert.AreEqual(1, session.Count);
			Assert.AreEqual(0, _assets.ClaimsOn(_cube));
		}

		[Test]
		public void LoadLandingAfterRelease_GivesItsClaimStraightBack()
		{
			_assets.Deferred = true;
			_assets.IgnoreCancellation = true;
			ModelPreviewSession session = Session();
			UniTask<ModelPreview> load = session.LoadAsync("a", "cube", options: NoIntro);

			session.Release("a");

			AssertPending(load); // a provider that can't abort is waited for, so its claim can go back
			_assets.CompleteNext();

			AssertCanceled(load);
			Assert.AreEqual(0, _assets.OutstandingClaims);
			Assert.AreEqual(0, _assets.ModelLoads, "nothing more loads once cancelled");
		}

		[Test]
		public void MaxConcurrent_CountsBoothsStillLoading()
		{
			_assets.Deferred = true;
			ModelPreviewSession session = Session(new ModelPreviewSessionOptions { MaxConcurrent = 1 });

			UniTask<ModelPreview> first = session.LoadAsync("a", "cube", options: NoIntro);

			using (ExpectedLog.Error(@"MaxConcurrent \(1\) reached"))
			{
				Assert.IsNull(Done(session.LoadAsync("b", "sphere", options: NoIntro)));
			}

			_assets.CompleteAll();

			Assert.IsNotNull(Done(first));
			Assert.AreEqual(1, session.Count);
		}

		// ------------------------------------------------------------------ failures

		[Test]
		public void ModelWithoutRenderers_IsRefused_AndLeavesNothing()
		{
			ModelPreviewSession session = Session();

			using (ExpectedLog.Error("has no renderers"))
			{
				Assert.IsNull(Done(session.LoadAsync("a", "empty", options: NoIntro)));
			}

			Assert.AreEqual(0, _assets.OutstandingClaims);
			Assert.AreEqual(0, _space.Count);
			Assert.IsEmpty(Booths());
			Assert.AreEqual(0, session.Count);
		}

		[Test]
		public void StageLoadFailure_Throws_AndTheNextLoadTriesAgain()
		{
			ModelPreviewSession session = Session();
			_assets.NextStageFailure = new InvalidOperationException("stage bundle missing");

			Exception failure = AssertFaulted(session.LoadAsync("a", "cube", options: NoIntro));

			StringAssert.Contains("stage bundle missing", failure.Message);
			Assert.AreEqual(0, _assets.OutstandingClaims);
			Assert.AreEqual(0, _assets.ModelLoads, "no model is loaded without a stage");

			Assert.IsTrue(Done(session.LoadAsync("a", "cube", options: NoIntro)).IsValid);
			Assert.AreEqual(2, _assets.StageLoads);
		}

		[Test]
		public void ModelLoadFailure_Throws_AndGivesTheStageBack()
		{
			ModelPreviewSession session = Session();

			Exception failure = AssertFaulted(session.LoadAsync("a", "unknown", options: NoIntro));

			StringAssert.Contains("unknown", failure.Message);
			Assert.AreEqual(0, _assets.OutstandingClaims);
			Assert.AreEqual(1, _assets.ReleasesOf(_stage.gameObject));
			Assert.IsEmpty(Booths());
		}

		[Test]
		public void MissingModel_IsRefused_BeforeLoadingAnything()
		{
			ModelPreviewSession session = Session();

			using (ExpectedLog.Error("no model address or prefab given for preview 'a'"))
			{
				Assert.IsNull(Done(session.LoadAsync("a", string.Empty, options: NoIntro)));
			}

			using (ExpectedLog.Error("no model address or prefab given for preview 'b'"))
			{
				Assert.IsNull(Done(session.LoadAsync("b", (GameObject)null, options: NoIntro)));
			}

			Assert.AreEqual(0, _assets.StageLoads);
			Assert.AreEqual(0, session.Count);
		}

		[Test]
		public void AfterDispose_LoadsAndUpdatesThrow_AndEverythingElseDoesNothing()
		{
			ModelPreviewSession session = Session();
			ModelPreview preview = Done(session.LoadAsync("a", "cube", options: NoIntro));
			RawImage image = Image("Late");
			GameObject decoration = Track(new GameObject("LateSparkle"));
			session.Dispose();

			Assert.IsInstanceOf<ObjectDisposedException>(AssertFaulted(session.LoadAsync("a", "cube", options: NoIntro)));
			Assert.IsInstanceOf<ObjectDisposedException>(AssertFaulted(session.LoadAsync("a", string.Empty, options: NoIntro)),
				"disposal is reported before anything about the request");
			Assert.IsInstanceOf<ObjectDisposedException>(AssertFaulted(session.UpdateAsync("a", "sphere")));

			// Late UI callbacks after a dialog closes: no throw, and no log (one would fail the test).
			session.Bind("a", image);
			session.Unbind("a");
			session.RotateBy("a", 10f, 0f);
			session.ZoomBy("a", 0.5f);
			session.ResetView("a");
			session.Attach("a", decoration, changeLayer: true);
			session.Detach("a", decoration);
			session.DetachAll("a");
			session.Release("a");
			preview.Bind(image);
			preview.RotateBy(10f, 0f);
			preview.Dispose();

			Assert.IsFalse(session.TryGet("a", out _));
			Assert.IsNull(image.texture);
			Assert.IsNull(decoration.transform.parent);
			Assert.AreEqual(0, _assets.OutstandingClaims);
		}

		// ------------------------------------------------------------------ space, binding, framing

		[Test]
		public void SessionsSharingASpace_NeverOverlap_AndReuseFreedSpots()
		{
			ModelPreviewSession first = Session();
			ModelPreviewSession second = Session();
			Vector3[] spots =
			{
				ModelPreviewStageSpace.Origin,
				ModelPreviewStageSpace.Origin + new Vector3(ModelPreviewStageSpace.SlotSpacing, 0f, 0f),
			};

			Done(first.LoadAsync("a", "cube", options: NoIntro));
			Done(second.LoadAsync("a", "cube", options: NoIntro));

			CollectionAssert.AreEquivalent(spots, BoothPositions());

			first.Release("a");
			Done(first.LoadAsync("b", "sphere", options: NoIntro));

			CollectionAssert.AreEquivalent(spots, BoothPositions(), "the freed spot is reused");
		}

		[Test]
		public void DirectionalLight_OnlyOneBoothLightsTheLayer_AndHandsItOverWhenItCloses()
		{
			// A second directional light, authored off: no booth may turn it on.
			var spare = new GameObject("SpareSun", typeof(Light));
			spare.transform.SetParent(_stage.transform, false);
			Light spareLight = spare.GetComponent<Light>();
			spareLight.type    = LightType.Directional;
			spareLight.enabled = false;

			ModelPreviewSession first = Session();
			ModelPreviewSession second = Session();
			Done(first.LoadAsync("a", "cube", options: NoIntro));
			Done(second.LoadAsync("b", "sphere", options: NoIntro));
			Done(second.LoadAsync("c", "capsule", options: NoIntro));

			Assert.AreEqual(1, LitDirectionalLights(), "one light already reaches every model on the layer; more would add up");

			first.Release("a"); // the first booth holds the light
			Assert.AreEqual(1, LitDirectionalLights(), "a booth still open, from any session, takes it over");

			second.Release("b");
			Assert.AreEqual(1, LitDirectionalLights());

			second.Release("c");
			Assert.AreEqual(0, _space.Count);

			Done(first.LoadAsync("d", "cube", options: NoIntro));
			Assert.AreEqual(1, LitDirectionalLights(), "the next booth lights again");
		}

		[Test]
		public void Bind_MovesAnOwnedInteractable_AndLeavesTheCallersAlone()
		{
			ModelPreviewSession session = Session(new ModelPreviewSessionOptions { EnableInteraction = true });
			RawImage first = Image("First");
			RawImage second = Image("Second");
			RawImage callers = Image("Callers");
			var callersInteractable = callers.gameObject.AddComponent<ModelPreviewInteractable>();

			ModelPreview preview = Done(session.LoadAsync("a", "cube", first, options: NoIntro));
			Assert.IsTrue(first.TryGetComponent(out ModelPreviewInteractable _), "interaction is added to the bound image");

			preview.Bind(second);
			Assert.IsFalse(first.TryGetComponent(out ModelPreviewInteractable _), "the session's interactable leaves with the binding");
			Assert.IsTrue(second.TryGetComponent(out ModelPreviewInteractable _));
			Assert.IsNull(first.texture);

			preview.Bind(callers);
			Assert.AreEqual(1, callers.GetComponents<ModelPreviewInteractable>().Length, "an interactable already there is used");

			preview.Dispose();
			Assert.IsTrue(callersInteractable != null, "the caller's interactable is never destroyed");
			Assert.IsNull(callers.texture);
		}

		[Test]
		public void Framing_FollowsVisibleGeometryOnly()
		{
			GameObject decorated = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
			AddChild(GameObject.CreatePrimitive(PrimitiveType.Cube), decorated, new Vector3(50f, 0f, 0f)).SetActive(false);
			AddChild(GameObject.CreatePrimitive(PrimitiveType.Cube), decorated, new Vector3(-60f, 0f, 0f)).GetComponent<Renderer>().enabled = false;
			AddChild(new GameObject("Sparks", typeof(ParticleSystem)), decorated, new Vector3(0f, 80f, 0f));
			AddChild(new GameObject("Trail", typeof(TrailRenderer)), decorated, new Vector3(0f, 0f, 90f));
			_assets.Models.Add("decorated", decorated);

			ModelPreviewSession session = Session();
			Done(session.LoadAsync("a", "cube", options: NoIntro));
			float plain = CameraDistance(SingleBooth());
			session.Release("a");

			Done(session.LoadAsync("a", "decorated", options: NoIntro));
			ModelPreviewCamera booth = SingleBooth();

			Assert.AreEqual(plain, CameraDistance(booth), 1e-3f, "hidden, disabled and effect renderers don't widen the framing");
			Vector3 visibleCentre = ModelIn(booth).GetComponent<Renderer>().bounds.center;
			Assert.Less(Vector3.Distance(visibleCentre, booth.Pivot.position), 1e-3f, "the visible geometry sits on the turntable");
		}

		[Test]
		public void SessionOptions_ReachTheBooth()
		{
			ModelPreviewSession session = Session(new ModelPreviewSessionOptions
			{
				RenderMode         = ModelPreviewRenderMode.Static,
				StaticWarmupFrames = 2,
				TimeDomain         = TimeDomain.Scaled,
				TextureSize        = 128,
			});

			ModelPreview preview = Done(session.LoadAsync("a", "cube", options: NoIntro));
			ModelPreviewCamera booth = SingleBooth();

			Assert.AreEqual(TimeDomain.Scaled, booth.TimeDomain);
			Assert.AreEqual(128, preview.Texture.width);
			Assert.IsTrue(booth.Camera.enabled, "a static booth renders its first frames");

			booth.Tick(0.016f);
			booth.Tick(0.016f);

			Assert.IsFalse(booth.Camera.enabled, "then stops once the view has settled");
		}

		// ------------------------------------------------------------------ helpers

		private ModelPreviewSession Session(ModelPreviewSessionOptions options = null)
		{
			var session = new ModelPreviewSession(_space, _assets, options);
			_sessions.Add(session);
			return session;
		}

		private T Track<T>(T created) where T : Object
		{
			_objects.Add(created);
			return created;
		}

		private RawImage Image(string name)
		{
			GameObject holder = Track(new GameObject(name, typeof(RectTransform)));
			return holder.AddComponent<RawImage>();
		}

		private static GameObject AddChild(GameObject child, GameObject parent, Vector3 localPosition)
		{
			child.transform.SetParent(parent.transform, false);
			child.transform.localPosition = localPosition;
			return child;
		}

		private List<ModelPreviewCamera> Booths()
		{
			var booths = new List<ModelPreviewCamera>();
			foreach (ModelPreviewCamera rig in Object.FindObjectsByType<ModelPreviewCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (rig != _stage && !_preexisting.Contains(rig))
				{
					booths.Add(rig);
				}
			}

			return booths;
		}

		private ModelPreviewCamera SingleBooth()
		{
			List<ModelPreviewCamera> booths = Booths();
			Assert.AreEqual(1, booths.Count, "expected exactly one booth in the scene");
			return booths[0];
		}

		private List<Vector3> BoothPositions()
		{
			var positions = new List<Vector3>();
			foreach (ModelPreviewCamera booth in Booths())
			{
				positions.Add(booth.transform.position);
			}

			return positions;
		}

		private int LitDirectionalLights()
		{
			int lit = 0;
			foreach (ModelPreviewCamera booth in Booths())
			{
				foreach (Light light in booth.GetComponentsInChildren<Light>(true))
				{
					if (light.type == LightType.Directional && light.enabled)
					{
						lit++;
					}
				}
			}

			return lit;
		}

		private static GameObject ModelIn(ModelPreviewCamera booth)
		{
			Assert.AreEqual(1, booth.Pivot.childCount, "expected one model on the turntable");
			return booth.Pivot.GetChild(0).gameObject;
		}

		private static float CameraDistance(ModelPreviewCamera booth) =>
			Vector3.Distance(booth.Camera.transform.position, booth.Pivot.position);

		private static T Done<T>(UniTask<T> task)
		{
			Assert.AreEqual(UniTaskStatus.Succeeded, task.Status, "the operation should have finished");
			return task.GetAwaiter().GetResult();
		}

		private static void AssertPending<T>(UniTask<T> task)
		{
			Assert.AreEqual(UniTaskStatus.Pending, task.Status, "the operation should still be waiting");
		}

		private static void AssertCanceled<T>(UniTask<T> task)
		{
			Assert.AreEqual(UniTaskStatus.Canceled, task.Status);
			Assert.Catch<OperationCanceledException>(() => task.GetAwaiter().GetResult());
		}

		private static Exception AssertFaulted<T>(UniTask<T> task)
		{
			Assert.AreEqual(UniTaskStatus.Faulted, task.Status);
			return Assert.Catch<Exception>(() => task.GetAwaiter().GetResult());
		}
	}
}
