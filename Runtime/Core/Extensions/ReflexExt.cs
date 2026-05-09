using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;

namespace AK.Core.Extensions
{
    public static class ReflexExt
    {
        public static void Inject(this object obj, Container container)
        {
            AttributeInjector.Inject(obj, container);
        }

        public static void InjectRecursive(this GameObject gameObject, Container container)
        {
            GameObjectInjector.InjectRecursive(gameObject, container);
        }

        public static T Instantiate<T>(this Container container, T original) where T : UnityEngine.Object
        {
            var obj = Object.Instantiate(original);
            obj.Inject(container);
            return obj;
        }
    }
}