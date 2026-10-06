using System;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AK.Core.Extensions
{
    public static class ReflexExt
    {
        /// <summary>Injects the <c>[Inject]</c> members of <paramref name="obj"/> itself.</summary>
        /// <exception cref="ArgumentException"><paramref name="obj"/> is a GameObject, which has none: use <see cref="InjectRecursive"/>.</exception>
        public static void Inject(this object obj, Container container)
        {
            if (obj is GameObject)
            {
                throw new ArgumentException("A GameObject has nothing to inject: use InjectRecursive, which injects its components and its children's.", nameof(obj));
            }

            AttributeInjector.Inject(obj, container);
        }

        /// <summary>
        /// Refused at compile time: a GameObject has no members to inject, so injecting it would
        /// do nothing. <see cref="InjectRecursive"/> injects its components and its children's.
        /// </summary>
        [Obsolete("A GameObject has nothing to inject: use InjectRecursive, which injects its components and its children's.", true)]
        public static void Inject(this GameObject gameObject, Container container)
        {
            throw new NotSupportedException("Use InjectRecursive.");
        }

        /// <summary>Injects every component of <paramref name="gameObject"/> and its children, inactive ones included.</summary>
        public static void InjectRecursive(this GameObject gameObject, Container container)
        {
            GameObjectInjector.InjectRecursive(gameObject, container);
        }

        /// <summary>
        /// Instantiates <paramref name="original"/> and injects the copy: for a GameObject or a
        /// component, every component of the copied hierarchy; for any other object, the object.
        /// The copy's <c>Awake</c> has run by then, as for objects in a scene.
        /// </summary>
        public static T Instantiate<T>(this Container container, T original) where T : Object
        {
            T instance = Object.Instantiate(original);

            switch (instance)
            {
                case GameObject gameObject:
                    GameObjectInjector.InjectRecursive(gameObject, container);
                    break;
                case Component component:
                    GameObjectInjector.InjectRecursive(component.gameObject, container);
                    break;
                default:
                    AttributeInjector.Inject(instance, container);
                    break;
            }

            return instance;
        }
    }
}
