using System;
using System.IO;
using System.Threading.Tasks;

namespace ConexionBroker
{
    /// <summary>
    /// Almacén en disco del VPS: cada archivo se guarda como {carpeta}/{hash}.
    /// El temporal se crea en la misma carpeta (mismo volumen) para que el paso
    /// final sea un rename atómico.
    /// </summary>
    public sealed class AlmacenLocal : IAlmacenDocumentos
    {
        private readonly string _carpeta;

        public AlmacenLocal(string carpeta)
        {
            _carpeta = carpeta;
            Directory.CreateDirectory(_carpeta);
        }

        public string CarpetaTemporal()
        {
            Directory.CreateDirectory(_carpeta);
            return _carpeta;
        }

        public Task<bool> ExisteAsync(string hash) =>
            Task.FromResult(File.Exists(Path.Combine(_carpeta, hash)));

        public Task GuardarAsync(string hash, string rutaTemporal)
        {
            string destino = Path.Combine(_carpeta, hash);
            if (File.Exists(destino))
            {
                File.Delete(rutaTemporal); // ya estaba (dedup): descartar el temporal
            }
            else
            {
                // Carrera: si otro subió el mismo archivo nuevo en el mismo instante,
                // el rename choca contra un destino ya creado → tratarlo como dedup.
                try { File.Move(rutaTemporal, destino); }
                catch (IOException) when (File.Exists(destino)) { File.Delete(rutaTemporal); }
            }
            return Task.CompletedTask;
        }

        public Task<Stream?> AbrirLecturaAsync(string hash)
        {
            string ruta = Path.Combine(_carpeta, hash);
            Stream? s = File.Exists(ruta) ? File.OpenRead(ruta) : null;
            return Task.FromResult(s);
        }
    }
}
