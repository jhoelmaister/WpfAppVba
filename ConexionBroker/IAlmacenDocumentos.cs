using System.IO;
using System.Threading.Tasks;

namespace ConexionBroker
{
    /// <summary>
    /// Almacén de archivos content-addressed (la clave es el SHA-256 del
    /// contenido). Abstrae DÓNDE viven los bytes: en disco del VPS
    /// (<see cref="AlmacenLocal"/>) o en Google Drive (<see cref="AlmacenDrive"/>).
    /// La app y la base solo conocen el hash, así que cambiar de backend es cambiar
    /// una línea de config — no toca ni la app ni el esquema.
    /// </summary>
    public interface IAlmacenDocumentos
    {
        /// <summary>Carpeta donde /upload escribe el temporal antes de guardarlo.</summary>
        string CarpetaTemporal();

        /// <summary>true si ya existe un archivo con ese hash (para deduplicar).</summary>
        Task<bool> ExisteAsync(string hash);

        /// <summary>
        /// Guarda el temporal <paramref name="rutaTemporal"/> bajo la clave
        /// <paramref name="hash"/>. Puede consumir (mover) el temporal; /upload igual
        /// lo borra al final si sigue ahí.
        /// </summary>
        Task GuardarAsync(string hash, string rutaTemporal);

        /// <summary>Stream de lectura del archivo, o null si no existe.</summary>
        Task<Stream?> AbrirLecturaAsync(string hash);
    }
}
