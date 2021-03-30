using System.IO;

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
    }
}
