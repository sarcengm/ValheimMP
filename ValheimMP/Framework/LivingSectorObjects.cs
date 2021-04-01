using System.Collections.Generic;

namespace ValheimMP.Framework
{
    /// <summary>
    /// Class that stores all instances objects by sector. 
    /// 
    /// The vanilla game does it basically two ways, all instances in a ZDOID dictionary, not useful for fast location looksups
    /// And a sector based dictionary with all ZDOs regardless whether they are instanced or not, but no reference to the instance, 
    /// requiring you to find all instances.
    /// 
    /// Since looks up of these objects happens on an almost frame by frame basis it basically needs to be fast, so I added this
    /// for lookup of only *instanced* objects by sector.
    /// 
    /// Also pregrouped by type so no sorting or filtering by that is needed while updating ZDOs
    /// </summary>
    public class LivingSectorObjects
    {
        private static Dictionary<int, Dictionary<int, LivingSectorObjects>> m_objects = new Dictionary<int, Dictionary<int, LivingSectorObjects>>();

        /// <summary>
        /// Get the full lists of objects for a sector
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static LivingSectorObjects GetObject(int x, int y)
        {
            if(m_objects.TryGetValue(x, out var ydic))
            {
                if(ydic.TryGetValue(y, out var obj))
                {
                    return obj;
                }
            }

            return null;
        }

        /// <summary>
        /// Add ZDO to sector
        /// </summary>
        /// <param name="zdo"></param>
        public static void AddObject(ZDO zdo)
        {
            // Only add living objects, one that does not have a netview is not instantiated!
            if (zdo.m_nview == null)
                return;

            //ZLog.Log("Add to sector " + zdo.m_sector);

            Dictionary<int, LivingSectorObjects> ydic;

            if (!m_objects.TryGetValue(zdo.m_sector.x, out ydic))
            {
                ydic = new Dictionary<int, LivingSectorObjects>();
                m_objects.Add(zdo.m_sector.x, ydic);
            }

            LivingSectorObjects obj;
            if (!ydic.TryGetValue(zdo.m_sector.y, out obj))
            {
                obj = new LivingSectorObjects();
                ydic.Add(zdo.m_sector.y, obj);
            }

            switch(zdo.m_type)
            {
                case (ZDO.ObjectType)(-1):
                    break;
                case ZDO.ObjectType.Solid:
                    obj.SolidObjects.Add(zdo.m_uid, zdo);
                    break;
                case ZDO.ObjectType.Prioritized:
                    obj.PriorityObjects.Add(zdo.m_uid, zdo);
                    break;
                case ZDO.ObjectType.Default:
                    obj.DefaultObjects.Add(zdo.m_uid, zdo);
                    break;
            }

            //ZLog.Log("Added object zdo.m_type: " + zdo.m_type + " name: " + zdo.m_netView + " sector: " + zdo.m_sector);
            //ZLog.Log(" SolidObjects:" + obj.SolidObjects.Count + " PriorityObjects:" + obj.PriorityObjects.Count + " DefaultObjects:" + obj.DefaultObjects.Count);
        }


        /// <summary>
        /// Remove ZDO from sector
        /// </summary>
        /// <param name="zdo"></param>
        /// <returns>True if the ZDO was removed</returns>
        public static bool RemoveObject(ZDO zdo)
        {
            var wasRemoved = false;

            if (m_objects.TryGetValue(zdo.m_sector.x, out var ydic))
            {
                if (ydic.TryGetValue(zdo.m_sector.y, out var obj))
                {
                    switch (zdo.m_type)
                    {
                        case ZDO.ObjectType.Solid:
                            wasRemoved = obj.SolidObjects.Remove(zdo.m_uid);
                            break;
                        case ZDO.ObjectType.Prioritized:
                            wasRemoved = obj.PriorityObjects.Remove(zdo.m_uid);
                            break;
                        default:
                            wasRemoved = obj.DefaultObjects.Remove(zdo.m_uid);
                            break;
                    }

                    // Clean up of empty sectors
                    if(obj.SolidObjects.Count == 0 && obj.PriorityObjects.Count == 0 && obj.DefaultObjects.Count == 0)
                    {
                        ydic.Remove(zdo.m_sector.y);
                        if (ydic.Count == 0)
                        {
                            m_objects.Remove(zdo.m_sector.x);
                        }
                    }
                }
            }

            return wasRemoved;
        }

        /// <summary>
        /// Solid objects, these objects only need to be send and checked once when a player enters a new sector
        /// For example trees, since they do not move, or basically do not do anything there is no reason to continues loop over all of them
        /// If something changes they are temporarily added to a list
        /// </summary>
        public Dictionary<ZDOID, ZDO> SolidObjects { get; private set; } = new Dictionary<ZDOID, ZDO>();

        /// <summary>
        /// Priority objects, these are basically all moving characters
        /// </summary>
        public Dictionary<ZDOID, ZDO> PriorityObjects { get; private set; } = new Dictionary<ZDOID, ZDO>();

        /// <summary>
        /// The rest ?
        /// </summary>
        public Dictionary<ZDOID, ZDO> DefaultObjects { get; private set; } = new Dictionary<ZDOID, ZDO>();
    }
}
