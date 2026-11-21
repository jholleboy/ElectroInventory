using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ElectroInventory
{
    internal sealed class ItemArtwork : IDisposable
    {
        private readonly string folder;
        private readonly Dictionary<string, Image> cache = new Dictionary<string, Image>(StringComparer.Ordinal);

        public ItemArtwork(string folder) { this.folder = folder; }

        internal static string FileName(string id)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-", "").ToLowerInvariant() + ".png";
        }

        public Image Get(string id)
        {
            if (folder == null || string.IsNullOrEmpty(id)) return null;
            if (cache.TryGetValue(id, out Image image)) return image;
            if (cache.Count >= 256) return null;
            string file = Path.Combine(folder, FileName(id));
            try
            {
                var info = new FileInfo(file);
                if (info.Exists && info.Length <= 1024 * 1024)
                    using (var source = Image.FromFile(file))
                        if (source.Width <= 512 && source.Height <= 512)
                            image = new Bitmap(source);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException ||
                e is ArgumentException || e is OutOfMemoryException || e is System.Runtime.InteropServices.ExternalException) { }
            cache[id] = image;
            return image;
        }

        public void Dispose()
        {
            foreach (var image in cache.Values) image?.Dispose();
            cache.Clear();
        }
    }
}
