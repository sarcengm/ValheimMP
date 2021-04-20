using fastJSON;
using System;
using System.Collections.Generic;

namespace ValheimMP.Framework
{
    /// <summary>
    /// Switched from Newtonsoft Json, still pretending to use newtonsoft Json.
    /// </summary>
    public static class JsonConvert
    {
        private static JSONParameters JSONParameters => new()
        {
            IgnoreAttributes = new List<Type>() { typeof(JsonIgnoreAttribute) },
            UseExtensions = false,
            ShowReadOnlyProperties = true, 
        };

        public static T DeserializeObject<T>(string jsonStr)
        {
            return JSON.ToObject<T>(jsonStr, JSONParameters);
        }

        public static string SerializeObject(object obj)
        {
            return JSON.ToNiceJSON(obj, JSONParameters);
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class JsonIgnoreAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class JsonPropertyAttribute : DataMemberAttribute
    {
    }

}
