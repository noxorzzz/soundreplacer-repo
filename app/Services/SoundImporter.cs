using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SoundReplacer.App.Services
{
    /// <summary>
    /// Копирует выбранный пользователем файл в BepInEx/plugins/SoundReplacer/Sounds
    /// под именем, производным от имени звука и хэша содержимого.
    ///
    /// Хэш в имени нужен, чтобы:
    ///  - разные звуки не перетирали файлы друг друга;
    ///  - при повторном «Применить» с тем же файлом не копировать его заново;
    ///  - старые файлы можно было безопасно удалить (они больше не упомянуты в конфиге).
    /// </summary>
    public sealed class SoundImportResult
    {
        public string AbsolutePath { get; set; }
        public string RelativePath { get; set; }
        public bool Copied { get; set; }
    }

    public static class SoundImporter
    {
        public static SoundImportResult Import(string soundName, string sourcePath, string soundsDirectory)
        {
            Directory.CreateDirectory(soundsDirectory);

            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            string hash = ComputeShortHash(sourcePath);
            string fileName = SanitizeName(soundName) + "_" + hash + extension;
            string target = Path.Combine(soundsDirectory, fileName);

            bool copied = false;
            if (!File.Exists(target))
            {
                File.Copy(sourcePath, target, false);
                copied = true;
            }
            else if (new FileInfo(target).Length != new FileInfo(sourcePath).Length)
            {
                File.Copy(sourcePath, target, true);
                copied = true;
            }

            return new SoundImportResult
            {
                AbsolutePath = target,
                RelativePath = "Sounds/" + fileName,
                Copied = copied
            };
        }

        public static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "sound";

            var builder = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                }
                else if (c == ' ' || c == '-' || c == '_' || c == '.' || c == '(' || c == ')')
                {
                    builder.Append('_');
                }

                if (builder.Length >= 40) break;
            }

            string result = builder.ToString().Trim('_', '.');
            return result.Length == 0 ? "sound" : result;
        }

        private static string ComputeShortHash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(16);
                for (int i = 0; i < 8; i++) builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }
}