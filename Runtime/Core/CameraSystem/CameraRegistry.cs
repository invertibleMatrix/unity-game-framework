using System.Collections.Generic;
using AK.Core;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Central registry of CameraDefinitions. The base registry resolves a definition by its
	/// own identity; this adds the CameraType → definitions view used for spawning.
	/// </summary>
	[CreateAssetMenu(fileName = "CameraRegistry", menuName = "AK/Camera/Camera Registry")]
	public class CameraRegistry : UidRegistryAsset<CameraDefinition>
	{
		private static readonly IReadOnlyList<CameraDefinition> Empty = new List<CameraDefinition>(0).AsReadOnly();

		private Dictionary<Uid, List<CameraDefinition>> _byCameraType;
		private int                                     _builtForVersion = -1;

		public CameraDefinition GetDefinitionByCameraType(Uid<CameraType> cameraType)
		{
			if (cameraType.IsNone) return null;
			EnsureCameraTypeCache();

			return _byCameraType.TryGetValue(cameraType.Value, out List<CameraDefinition> defs) && defs.Count > 0 ? defs[0] : null;
		}

		public CameraDefinition GetDefinitionByCameraType(CameraType cameraType)
		{
			return cameraType != null ? GetDefinitionByCameraType(cameraType.IdAs<CameraType>()) : null;
		}

		public IReadOnlyList<CameraDefinition> GetDefinitionsByCameraType(Uid<CameraType> cameraType)
		{
			if (cameraType.IsNone) return Empty;
			EnsureCameraTypeCache();

			return _byCameraType.TryGetValue(cameraType.Value, out List<CameraDefinition> defs) ? defs : Empty;
		}

		private void EnsureCameraTypeCache()
		{
			if (_byCameraType != null && _builtForVersion == _registry.Version) return;

			_byCameraType = new Dictionary<Uid, List<CameraDefinition>>();

			foreach (CameraDefinition def in _registry.Objects)
			{
				if (def == null || def.CameraType == null || !def.CameraType.HasIdentity) continue;

				Uid typeId = def.CameraType.Id;
				if (!_byCameraType.TryGetValue(typeId, out List<CameraDefinition> list))
				{
					list = new List<CameraDefinition>();
					_byCameraType[typeId] = list;
				}

				list.Add(def);
			}

			_builtForVersion = _registry.Version;
		}
	}
}
