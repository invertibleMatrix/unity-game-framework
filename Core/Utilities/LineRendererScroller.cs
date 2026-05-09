using UnityEngine;

namespace AK.Utilities
{
	public class LineRendererScroller : MonoBehaviour
	{
		[SerializeField] private float ScrollSpeed = 2f;
		[Range(-1, 1)] [SerializeField] private int ScrollDirection = -1;

		private LineRenderer _lineRenderer;
		private static readonly int MainTex = Shader.PropertyToID("_BaseMap");

		void Start()
		{
			_lineRenderer = GetComponent<LineRenderer>();
		}

		void Update()
		{
			float offset = Time.time * ScrollSpeed * ScrollDirection;
			_lineRenderer.material.SetTextureOffset(MainTex, new Vector2(offset, 0));
		}
	}
}