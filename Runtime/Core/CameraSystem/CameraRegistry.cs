using System.Collections.Generic;
using AK.Core;
using UnityEngine;

namespace AK.Systems
{
	/// <summary>
	/// Central registry of CameraDefinitions. Inherits UID-based lookups and editor refresh
	/// from TypedUIDRegistryAsset. Adds CameraType-based lookups for camera spawning.
	/// </summary>
	[CreateAssetMenu(fileName = "CameraRegistry", menuName = "AK/Camera/Camera Registry")]
	public class CameraRegistry : TypedUIDRegistryAsset<CameraDefinition>
	{
		private Dictionary<string, List<CameraDefinition>> _cameraTypeToDefinitions;

		public CameraDefinition GetDefinitionByCameraType(CameraType cameraType)
		{
			if (cameraType == null || cameraType.IsEmpty()) return null;
			BuildCameraTypeCache();

			if (_cameraTypeToDefinitions.TryGetValue(cameraType.Id, out var defs) && defs.Count > 0)
			{
				return defs[0];
			}

			return null;
		}

		public IReadOnlyList<CameraDefinition> GetDefinitionsByCameraType(CameraType cameraType)
		{
			if (cameraType == null || cameraType.IsEmpty()) return new List<CameraDefinition>();
			BuildCameraTypeCache();

			return _cameraTypeToDefinitions.TryGetValue(cameraType.Id, out var defs)
				? defs.AsReadOnly()
				: new List<CameraDefinition>().AsReadOnly();
		}

		private void BuildCameraTypeCache()
		{
			if (_cameraTypeToDefinitions != null) return;

			_cameraTypeToDefinitions = new Dictionary<string, List<CameraDefinition>>();

			foreach (var def in GetAllObjects())
			{
				if (def == null || def.CameraType == null) continue;

				string typeId = def.CameraType.Id;
				if (!_cameraTypeToDefinitions.ContainsKey(typeId))
				{
					_cameraTypeToDefinitions[typeId] = new List<CameraDefinition>();
				}

				_cameraTypeToDefinitions[typeId].Add(def);
			}
		}
	}
}