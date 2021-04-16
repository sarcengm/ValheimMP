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
            UseExtensions = false,
            IgnoreAttributes = new List<Type>() { typeof(JsonIgnoreAttribute) },
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
    public class JsonIgnoreAttribute : Attribute
    {
    }

    public class JsonPropertyAttribute : DataMemberAttribute
    {
    }

}
