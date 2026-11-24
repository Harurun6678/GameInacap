using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NeonSpite.Persistence
{
    // Independiente de Unity: escritura atómica y copia del guardado anterior.
    public sealed class SaveFileStore
    {
        private const string Header = "GYARU_SAVE_1";
        private readonly string directory;
        private readonly SaveFileStore previousProduct;
        public SaveFileStore(string directory, string previousDirectory = null)
        {
            this.directory = directory;
            if (!string.IsNullOrEmpty(previousDirectory) && previousDirectory != directory)
                previousProduct = new SaveFileStore(previousDirectory);
        }
        public string PathFor(int slot)
        {
            if (slot < 1 || slot > 3)
                throw new ArgumentOutOfRangeException("slot");
            return Path.Combine(directory, "partida_" + slot + ".sav");
        }
        public bool Exists(int slot)
        {
            string path = PathFor(slot);
            return File.Exists(path) || File.Exists(path + ".bak") || (previousProduct?.Exists(slot) ?? false);
        }
        public bool TryWrite(int slot, string json, out string error)
        {
            error = null;
            string temporary = null;
            try
            {
                if (string.IsNullOrEmpty(json) || Encoding.UTF8.GetByteCount(json) > 2000000)
                    throw new InvalidDataException();
                string path = PathFor(slot);
                Directory.CreateDirectory(directory);
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                byte[] bytes = Encoding.UTF8.GetBytes(Header + "\n" + Digest(json) + "\n" + json);
                using (FileStream file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    file.Write(bytes, 0, bytes.Length);
                    file.Flush(true);
                }
                if (File.Exists(path))
                {
                    string ignored;
                    // Si el original está dañado, conserva la copia buena anterior.
                    File.Replace(temporary, path, TryReadFile(path, out ignored) ? path + ".bak" : null);
                }
                else
                    File.Move(temporary, path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException || e is System.Security.SecurityException)
            {
                error = "No se pudo guardar. Comprueba el espacio libre y el acceso a la carpeta de partidas.";
                return false;
            }
            finally { if (temporary != null) { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } } }
        }
        public bool TryRead(int slot, out string json, out bool recovered, out string error)
        {
            recovered = false;
            error = null;
            json = null;
            string path = PathFor(slot);
            if (!File.Exists(path) && !File.Exists(path + ".bak") && previousProduct != null)
                return previousProduct.TryRead(slot, out json, out recovered, out error);
            if (TryReadFile(path, out json))
                return true;
            if (TryReadFile(path + ".bak", out json))
            {
                recovered = true;
                return true;
            }
            error = Exists(slot) ? "El archivo está dañado y no hay una copia válida." : "Esta ranura está vacía.";
            return false;
        }
        private static bool TryReadFile(string path, out string json)
        {
            json = null;
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 2100000)
                    return false;
                string data = File.ReadAllText(path, Encoding.UTF8);
                int a = data.IndexOf('\n'), b = a < 0 ? -1 : data.IndexOf('\n', a + 1);
                if (a < 0 || b < 0 || data.Substring(0, a) != Header)
                    return false;
                string payload = data.Substring(b + 1);
                if (data.Substring(a + 1, b - a - 1) != Digest(payload))
                    return false;
                json = payload;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is System.Security.SecurityException) { return false; }
        }
        private static string Digest(string value)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
    }
}
