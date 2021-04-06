using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ValheimMP.Framework.Extensions
{
    public static class ZPackageExtension
    {
        /// <summary>
        /// Compress a package
        /// </summary>
        /// <param name="pkg"></param>
        /// <returns>Decompressed package</returns>
        public static ZPackage Decompress(this ZPackage pkg)
        {
            var uncompressedSize = pkg.ReadInt();
            var uncompressedBytes = new byte[uncompressedSize];
            var compressedBytes = pkg.ReadByteArray();
            using (var ms = new MemoryStream(compressedBytes))
            {
                using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Decompress))
                {
                    ds.Read(uncompressedBytes, 0, uncompressedBytes.Length);
                }
            }
            var zPackage = new ZPackage(uncompressedBytes);
            return zPackage;
        }

        /// <summary>
        /// Decompress a package
        /// </summary>
        /// <param name="zPackage"></param>
        /// <returns>Compressed package</returns>
        public static ZPackage Compress(this ZPackage zPackage)
        {
            var uncompressedBytes = zPackage.GetArray();
            var pkg = new ZPackage();

            using (var ms = new MemoryStream())
            {
                using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Compress))
                {
                    ds.Write(uncompressedBytes, 0, uncompressedBytes.Length);
                }

                pkg.Write(uncompressedBytes.Length);
                pkg.Write(ms.ToArray());
            }
            return pkg;
        }

        public static void Write(this ZPackage pkg, Dictionary<int, byte[]> dictionary)
        {
            pkg.Write(dictionary.Count);
            foreach (var item in dictionary.ToList())
            {
                pkg.Write(item.Key);
                pkg.Write(item.Value);
            }
        }

        public static void Read(this ZPackage pkg, ref Dictionary<int, byte[]> dictionary)
        {
            var count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var key = pkg.ReadInt();
                var val = pkg.ReadByteArray();
                dictionary[key] = val;
            }
        }

        /// <summary>
        /// Write a counter at a certain position, then jump back to the end of the stream.
        /// 
        /// optionally truncate the stream if the counter was 0, so anything after the counter is truncated
        /// 
        /// Useful when writing things but you don't know how many items there are initially, so you write a placerholder first
        /// then fill it in later, and if its 0 remove any of the things you may have written that are of no use;
        /// e.g. I write an item ID first, check all changes, then find out nothing changed, so the item ID was useless and can be truncated.
        /// </summary>
        /// <param name="pkg"></param>
        /// <param name="at"></param>
        /// <param name="count"></param>
        /// <param name="truncate"></param>
        public static void WriteCounter(this ZPackage pkg, int at, int count, bool truncate, bool truncateCounterAswell = false)
        {
            var endPos = pkg.GetPos();
            pkg.SetPos(at);

            if(count != 0 || !truncateCounterAswell)
                pkg.Write(count);

            if (count == 0)
            {
                if (truncate)
                {
                    pkg.m_stream.SetLength(pkg.GetPos());
                }
            }
            else
            {
                pkg.SetPos(endPos);
            }
        }


        /// <summary>
        /// Reads a long and resets the position
        /// </summary>
        /// <param name="pkg"></param>
        /// <returns></returns>
        public static long PeekLong(this ZPackage pkg)
        {
            var pos = pkg.GetPos();
            var peek = pkg.ReadLong();
            pkg.SetPos(pos);
            return peek;
        }

        /// <summary>
        /// Reads an integer and resets the position
        /// </summary>
        /// <param name="pkg"></param>
        /// <returns></returns>
        public static int PeekInt(this ZPackage pkg)
        {
            var pos = pkg.GetPos();
            var peek = pkg.ReadInt();
            pkg.SetPos(pos);
            return peek;
        }

        /// <summary>
        /// Reads a ZDOID and resets the position
        /// </summary>
        /// <param name="pkg"></param>
        /// <returns></returns>
        public static ZDOID PeekZDOID(this ZPackage pkg)
        {
            var pos = pkg.GetPos();
            var peek = pkg.ReadZDOID();
            pkg.SetPos(pos);
            return peek;
        }
    }
}
