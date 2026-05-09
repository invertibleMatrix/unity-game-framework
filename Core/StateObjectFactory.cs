using System;
using System.Collections.Generic;
using UnityEngine;

namespace AK.Core
{
    public sealed class StateObjectFactory : MonoBehaviour
    {
        private static StateObjectFactory _instance;

        private static List<StateObject> _stateObjects = new List<StateObject>();

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
            for (int i = 0; i < _stateObjects.Count; i++)
            {
                _stateObjects[i].OnUpdate();
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _stateObjects.Count; i++)
            {
                _stateObjects[i].OnDestroy();
            }

            _stateObjects.Clear();
            Destroy(gameObject);
            _instance = null;
        }
    }
}