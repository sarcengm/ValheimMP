using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Framework.Extensions
{
    public static class TypeExtension
    {
        /// <summary>
        /// Use reflection to obtain a constant value.
        /// 
        /// Rather then including it at compile time and it turning into a literal, retrieve the current constant value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="from"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public static T GetConstantValue<T>(this Type from, string fieldName) 
        {
            return from.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(T) && fi.Name == fieldName)
                    .Select(x => (T)x.GetRawConstantValue())
                    .SingleOrDefault();
       }
    }
}
