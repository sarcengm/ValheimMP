using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValheimMP.Framework.Extensions
{
    public enum ZDOType
    {
        /// <summary>
        /// Normal will always be replicated to everyone (if changed)
        /// </summary>
        Normal = 0,
        /// <summary>
        /// Private will only be replicated to the client that owns the object (usually only player characters are owned)
        /// </summary>
        Private = 1,
        /// <summary>
        /// Ignored will never be replicated.
        /// </summary>
        Ignored = 2,
        /// <summary>
        /// AllExceptOriginator, will be replicated to all clients except the client that instigated the creation of this object (used for effects, the owning client does not need another copy that is already dated!)
        /// </summary>
        AllExceptOriginator = 3
    }

    public enum ZDOFieldType
    {
        /// <summary>
        /// Normal fields, these are all fields that are unspecified, will always be replicated to everyone (if changed)
        /// </summary>
        Normal = 0,
        /// <summary>
        /// Private fields, will only be replicated to the client that owns the object (usually only player characters are owned)
        /// </summary>
        Private = 1,
        /// <summary>
        /// Ignored fields, will never be replicated.
        /// </summary>
        Ignored = 2,
        /// <summary>
        /// AllExceptOwner, will be replicated to all clients except the client who owns this object.
        /// </summary>
        AllExceptOwner = 3
    }

    public static class ZDOExtension
    {
        public static void RegisterZDOEvent(this ZDO zdo, string variableName, Action<ZDO> action)
        {
            zdo.RegisterZDOEvent(variableName.GetStableHashCode(), action);
        }

        public static void RegisterZDOEvent(this ZDO zdo, int variableHash, Action<ZDO> action)
        {
            if (zdo.m_zdoEvents.ContainsKey(variableHash))
            {
                zdo.m_zdoEvents[variableHash] += action;
            }
            else
            {
                zdo.m_zdoEvents.Add(variableHash, action);
            }
        }

        public static void UnregisterZDOEvent(this ZDO zdo, string variableName, Action<ZDO> action)
        {
            zdo.UnregisterZDOEvent(variableName.GetStableHashCode(), action);
        }

        public static void UnregisterZDOEvent(this ZDO zdo, int variableHash, Action<ZDO> action)
        {
            if (zdo.m_zdoEvents.ContainsKey(variableHash))
            {
                zdo.m_zdoEvents[variableHash] -= action;
            }
        }

        public static void ClearZDOEvent(this ZDO zdo, string variableName)
        {
            zdo.ClearZDOEvent(variableName);
        }

        public static void ClearZDOEvent(this ZDO zdo, int variableHash)
        {
            zdo.m_zdoEvents.Remove(variableHash);
        }

        public static void SetFieldType(this ZDO zdo, string variableName, ZDOFieldType fieldType)
        {
            zdo.SetFieldType(variableName.GetStableHashCode(), fieldType);
        }

        public static void SetFieldType(this ZDO zdo, int variableHash, ZDOFieldType fieldType)
        {
            zdo.m_fieldTypes[variableHash] = (int)fieldType;
        }

        public static void SetZDOType(this ZDO zdo, ZDOType fieldType)
        {
            zdo.m_zdoType = (int)fieldType;
        }

        public static ZDOType GetFieldType(this ZDO zdo)
        {
            return (ZDOType)zdo.m_zdoType;
        }
    }
}
