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
    }
}
