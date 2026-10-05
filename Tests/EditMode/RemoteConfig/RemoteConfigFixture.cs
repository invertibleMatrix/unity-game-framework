using System;
using System.Collections.Generic;
using AK.Core;
using AK.CoreDomain.RemoteConfig;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Tests.RemoteConfig
{
	/// <summary>
	/// Remote variables and metas built in memory, as the inspector would set them up, and
	/// destroyed on <see cref="Dispose"/>. Each variable gets an identity of its own.
	/// </summary>
	internal sealed class RemoteConfigFixture : IDisposable
	{
		private readonly List<Object> _assets = new();

		public RemoteInt Int(string key, int defaultValue = 0, bool enabled = true, bool cache = true) =>
			Variable<RemoteInt>(key, enabled, cache, value => value.intValue = defaultValue);

		public RemoteBool Bool(string key, bool defaultValue = false, bool enabled = true, bool cache = true) =>
			Variable<RemoteBool>(key, enabled, cache, value => value.boolValue = defaultValue);

		public RemoteFloat Float(string key, float defaultValue = 0f, bool enabled = true, bool cache = true) =>
			Variable<RemoteFloat>(key, enabled, cache, value => value.floatValue = defaultValue);

		public RemoteString Text(string key, string defaultValue = "", bool enabled = true, bool cache = true) =>
			Variable<RemoteString>(key, enabled, cache, value => value.stringValue = defaultValue);

		/// <summary>A variable of any type, its default set through <paramref name="setDefault"/> unless null.</summary>
		public T Variable<T>(string key, bool enabled = true, bool cache = true, Action<SerializedProperty> setDefault = null) where T : RemoteVariableBase
		{
			T variable = Track(ScriptableObject.CreateInstance<T>());
			variable.name = key;
			variable.Editor_AssignIdentity(Uid.NewRandom(), UidProvenance.Minted, string.Empty);

			var serialized = new SerializedObject(variable);
			serialized.FindProperty("_variableKey").stringValue = key;
			serialized.FindProperty("_isEnabled").boolValue    = enabled;
			serialized.FindProperty("_cacheValue").boolValue   = cache;
			setDefault?.Invoke(serialized.FindProperty("_defaultValue"));
			serialized.ApplyModifiedPropertiesWithoutUndo();

			return variable;
		}

		/// <summary>A meta over a registry of <paramref name="variables"/>, in that order.</summary>
		public RemoteConfigMeta Meta(params RemoteVariableBase[] variables)
		{
			var registry = Track(ScriptableObject.CreateInstance<RemoteVariablesRegistry>());
			registry.Registry.ReplaceAll(variables);

			var meta = Track(ScriptableObject.CreateInstance<RemoteConfigMeta>());
			var serialized = new SerializedObject(meta);
			serialized.FindProperty("_registry").objectReferenceValue = registry;
			serialized.ApplyModifiedPropertiesWithoutUndo();

			return meta;
		}

		/// <summary>Destroys <paramref name="asset"/> with the rest.</summary>
		public T Track<T>(T asset) where T : Object
		{
			_assets.Add(asset);
			return asset;
		}

		public void Dispose()
		{
			foreach (Object asset in _assets)
			{
				Object.DestroyImmediate(asset);
			}

			_assets.Clear();
		}
	}
}
