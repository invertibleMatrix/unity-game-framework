using System;
using System.Collections.Generic;
using UnityEngine;

namespace AK.Core
{
    /// <summary>
    /// Creates <see cref="StateObject"/>s and ticks them each frame. Disposing the factory, or
    /// destroying its GameObject, destroys them.
    /// </summary>
    public sealed class StateObjectFactory : MonoBehaviour
    {
        private static StateObjectFactory _instance;

        private static readonly List<StateObject> _stateObjects = new List<StateObject>();

        // With domain reload off, statics outlive a Play session: start each one empty.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _stateObjects.Clear();
        }

        public static StateObjectFactory Construct(bool dontDestroyOnLoad = true)
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject obj = new GameObject("StateObjectFactory");
            _instance = obj.AddComponent<StateObjectFactory>();
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(obj);
            }

            return _instance;
        }

        public static TType CreateStateObject<TType>() where TType : StateObject, new()
        {
            TType stateObject = new TType();
            _stateObjects.Add(stateObject);
            stateObject.InitInternal();
            return stateObject;
        }

        private void Update()
        {
            // A disposed factory lives until the end of the frame; only the current one ticks.
            if (_instance != this) return;

            for (int i = 0; i < _stateObjects.Count; i++)
            {
                _stateObjects[i].OnUpdate();
            }
        }

        public void Dispose()
        {
            Destroy(gameObject);
            DestroyStateObjects();
        }

        private void OnDestroy()
        {
            DestroyStateObjects();
        }

        private void DestroyStateObjects()
        {
            if (_instance != this) return;

            _instance = null;

            // Each one is destroyed even if another throws.
            for (int i = 0; i < _stateObjects.Count; i++)
            {
                try
                {
                    _stateObjects[i].DestroyInternal();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            _stateObjects.Clear();
        }
    }
}