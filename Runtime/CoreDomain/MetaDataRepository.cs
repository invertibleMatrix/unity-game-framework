using System.Collections.Generic;
using System.Linq;
using AK.Core;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AK.CoreDomain
{
	[CreateAssetMenu(fileName = "MetaDataRepository", menuName = "AK/MetaData/MetaDataRepository")]
	public class MetaDataRepository : ScriptableObject, IMetaDataRepository
	{
		[SerializeField] private UIDRegistry _uidRegistry;

		// Type-keyed registry for extensible Meta lookup
		private readonly Dictionary<System.Type, IMeta> _metaRegistry = new();

		public UIDRegistry  UIDRegistry  => _uidRegistry;

		public void RegisterMeta<T>(T meta) where T : class, IMeta
		{
			if (meta == null) return;
			_metaRegistry[typeof(T)] = meta;
		}

		public T GetMeta<T>() where T : class, IMeta
		{
			return _metaRegistry.TryGetValue(typeof(T), out var meta) ? meta as T : null;
		}

		public bool TryGetMeta<T>(out T meta) where T : class, IMeta
		{
			if (_metaRegistry.TryGetValue(typeof(T), out var m))
			{
				meta = m as T;
				return meta != null;
			}

			meta = null;
			return false;
		}

		public void InitializeRegistries()
		{
			_uidRegistry.Initialize();

			foreach (var kvp in _metaRegistry)
			{
				kvp.Value.InitializeMeta();
			}
		}

		public T GetObjectByUID<T>(UID uid) where T : ScriptableObject
		{
			if (uid == null || uid.IsEmpty()) return null;

			return null;
		}

#if UNITY_EDITOR
		[Button]
		public void PerformDataRegistration()
		{
			_uidRegistry.RefreshAllUIDs();
		}
#endif
	}
}
