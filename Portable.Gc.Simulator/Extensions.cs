using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Portable.Gc.Simulator
{
    static class Extensions
    {
        public static int AlignTo(this int size, int alignment)
        {
            return size + (alignment - ((size - 1) % alignment)) - 1;
        }

        public static T CreateDelegate<T>(this MethodInfo method)
        {
            return (T)(object)Delegate.CreateDelegate(typeof(T), method);
        }

        public static T[] GetEnumValues<T>(this T x)
        {
            return GetEnumValues<T>();
        }

        public static T[] GetEnumValues<T>()
        {
            return Enum.GetValues(typeof(T)).OfType<T>().ToArray();
        }

        public static void ForEach<T>(this IEnumerable<T> seq, Action<T> act)
        {
            foreach (var item in seq)
            {
                act(item);
            }
        }
    }
}
