using AK.Tests.Support;
using AK.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AK.Tests.ThirdParty
{
	/// <summary>
	/// Gradient2 colors a graphic's mesh on every rebuild. These tests drive
	/// <see cref="Gradient2.ModifyMesh(VertexHelper)"/> on hand-built quads.
	/// </summary>
	public sealed class Gradient2Tests
	{
		private GameObject   _host;
		private Gradient2    _effect;
		private VertexHelper _mesh;

		[SetUp]
		public void SetUp()
		{
			_host   = new GameObject("Gradient2Tests", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
			_effect = _host.AddComponent<Gradient2>();
			_mesh   = new VertexHelper();
		}

		[TearDown]
		public void TearDown()
		{
			_mesh.Dispose();
			Object.DestroyImmediate(_host);
		}

		private void AddQuad(float xMin, float yMin, float xMax, float yMax)
		{
			UIVertex vertex = UIVertex.simpleVert;
			int      first  = _mesh.currentVertCount;

			vertex.position = new Vector3(xMin, yMin); _mesh.AddVert(vertex);
			vertex.position = new Vector3(xMin, yMax); _mesh.AddVert(vertex);
			vertex.position = new Vector3(xMax, yMax); _mesh.AddVert(vertex);
			vertex.position = new Vector3(xMax, yMin); _mesh.AddVert(vertex);

			_mesh.AddTriangle(first, first + 1, first + 2);
			_mesh.AddTriangle(first + 2, first + 3, first);
		}

		private static UnityEngine.Gradient MakeGradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
		{
			var gradient = new UnityEngine.Gradient();
			gradient.SetKeys(colors, alphas);
			return gradient;
		}

		[Test]
		public void Horizontal_SplitsTheQuadAtAStop_SoTheStopsColorShows()
		{
			// Black at both ends and white in the middle. Without vertices at x = 50, the quad would
			// blend black to black and the white would never show.
			_effect.EffectGradient = MakeGradient(
				new[] { new GradientColorKey(Color.black, 0f), new GradientColorKey(Color.white, 0.5f), new GradientColorKey(Color.black, 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
			_effect.BlendMode      = Gradient2.Blend.Override;
			_effect.GradientType   = Gradient2.Type.Horizontal;
			_effect.ModifyVertices = true;

			AddQuad(0f, 0f, 100f, 10f);
			_effect.ModifyMesh(_mesh);

			// The cut lands within float error of x = 50.
			int      atStop = 0;
			UIVertex vertex = default;
			for (int i = 0; i < _mesh.currentVertCount; i++)
			{
				_mesh.PopulateUIVertex(ref vertex, i);
				if (Mathf.Abs(vertex.position.x - 50f) < 0.01f)
				{
					atStop++;
					Assert.AreEqual(new Color32(255, 255, 255, 255), vertex.color);
				}
				else
				{
					Assert.AreEqual(new Color32(0, 0, 0, 255), vertex.color, $"vertex {i} at x {vertex.position.x}");
				}
			}

			Assert.GreaterOrEqual(atStop, 2, "the stop cuts the quad's top and bottom edges");
		}

		[TestCase(Gradient2.Type.Horizontal, true)]
		[TestCase(Gradient2.Type.Vertical, true)]
		[TestCase(Gradient2.Type.Horizontal, false)]
		[TestCase(Gradient2.Type.Radial, true)]
		[TestCase(Gradient2.Type.Diamond, true)]
		public void Rebuild_OnceWarm_AllocatesNothing(Gradient2.Type type, bool modifyVertices)
		{
			_effect.EffectGradient = MakeGradient(
				new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.green, 0.3f), new GradientColorKey(Color.blue, 0.7f), new GradientColorKey(Color.white, 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(1f, 1f) });
			_effect.GradientType   = type;
			_effect.ModifyVertices = modifyVertices;

			// A line of five glyph-sized quads, rebuilt ten times.
			void Rebuilds()
			{
				for (int r = 0; r < 10; r++)
				{
					_mesh.Clear();
					for (int q = 0; q < 5; q++) AddQuad(q * 40f, 0f, q * 40f + 32f, 50f);
					_effect.ModifyMesh(_mesh);
				}
			}

			Rebuilds(); // Fills Unity's list pools and grows the mesh's buffers.
			int allocations = GcAllocations.Count(Rebuilds);

			Assert.AreEqual(0, allocations);
		}
	}
}
