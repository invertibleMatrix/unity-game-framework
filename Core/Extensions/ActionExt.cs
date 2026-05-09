using System;

namespace AK.Core.Extensions
{
    public static class ActionExt
    {
        public static void SafeInvoke(this Action action)
        {
            if (action != null) action();
        }
        
        public static void SafeInvoke<T>(this Action<T> action, T @param)
        {
            if (action != null) action(@param);
        }
    }
}