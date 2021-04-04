using System;
using System.Collections.Generic;
using System.Linq;

namespace ValheimMP.Framework
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ChatCommandAttribute : Attribute
    {
        internal string m_name;
        internal HashSet<string> m_aliases;
        internal string m_description;
        internal bool m_requireAdmin;

        public string Name { get { return m_name; } }
        public string Description { get { return m_description; } }
        public bool AdminRequired { get { return m_requireAdmin; } }
        public string[] GetAliases()
        { 
            return m_aliases.ToArray(); 
        }


        public ChatCommandAttribute(string name, string description, bool requireAdmin = false, string[] aliases = null)
        {
            m_name = name;
            m_description = description;
            m_aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            m_aliases.Add(name);
            if (aliases != null)
            {
                for (int i = 0; i < aliases.Length; i++)
                {
                    m_aliases.Add(aliases[i]);
                }
            }
            m_requireAdmin = requireAdmin;
        }
    }
}
