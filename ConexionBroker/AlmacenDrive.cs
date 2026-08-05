using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace ConexionBroker
{
    /// <summary>
    /// Almacén en Google Drive (cuenta personal, vía OAuth con refresh token). Los
    /// archivos se guardan por hash dentro de una carpeta propia que la app crea la
    /// primera vez (scope mínimo <c>drive.file</c>: solo ve/gestiona lo que ella
    /// misma sube, no el resto de tu Drive).
    ///
    /// Config (sección "Drive", en appsettings.Production.json o variables
    /// Drive__*): ClientId, ClientSecret, RefreshToken y Carpeta (nombre).
    /// </summary>
    public sealed class AlmacenDrive : IAlmacenDocumentos
    {
        private const string MimeCarpeta = "application/vnd.google-apps.folder";

        private readonly DriveService _drive;
        private readonly string _nombreCarpeta;

        private readonly SemaphoreSlim _lockCarpeta = new(1, 1);
        private string? _carpetaId;

        public AlmacenDrive(string clientId, string clientSecret, string refreshToken, string nombreCarpeta)
        {
            _nombreCarpeta = string.IsNullOrWhiteSpace(nombreCarpeta) ? "ConexionBroker-Documentos" : nombreCarpeta;

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                Scopes        = new[] { DriveService.Scope.DriveFile }
            });
            // UserCredential renueva solo el access token usando el refresh token.
            var credencial = new UserCredential(flow, "user", new TokenResponse { RefreshToken = refreshToken });

            _drive = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credencial,
                ApplicationName       = "ConexionBroker"
            });
        }

        // Drive necesita el temporal en disco solo de paso; el sistema temp alcanza.
        public string CarpetaTemporal() => Path.GetTempPath();

        public async Task<bool> ExisteAsync(string hash)
        {
            string carpetaId = await CarpetaIdAsync();
            return await BuscarIdAsync(hash, carpetaId) != null;
        }

        public async Task GuardarAsync(string hash, string rutaTemporal)
        {
            string carpetaId = await CarpetaIdAsync();

            // Doble chequeo por si otro lo subió entre el ExisteAsync de /upload y esto.
            if (await BuscarIdAsync(hash, carpetaId) != null) return;

            var meta = new DriveFile { Name = hash, Parents = new List<string> { carpetaId } };
            await using var fs = File.OpenRead(rutaTemporal);
            var subida = _drive.Files.Create(meta, fs, "application/octet-stream");
            subida.Fields = "id";
            var progreso = await subida.UploadAsync();
            if (progreso.Status != UploadStatus.Completed)
                throw progreso.Exception ?? new Exception("Fallo la subida a Google Drive.");
        }

        public async Task<Stream?> AbrirLecturaAsync(string hash)
        {
            string carpetaId = await CarpetaIdAsync();
            string? id = await BuscarIdAsync(hash, carpetaId);
            if (id == null) return null;

            var ms = new MemoryStream();
            await _drive.Files.Get(id).DownloadAsync(ms);
            ms.Position = 0;
            return ms;
        }

        // Id del archivo con ese nombre (hash) dentro de la carpeta, o null.
        private async Task<string?> BuscarIdAsync(string hash, string carpetaId)
        {
            var req = _drive.Files.List();
            // hash es hex de 64 chars (validado antes) y carpetaId lo genera Google:
            // sin caracteres que rompan la query.
            req.Q      = $"name = '{hash}' and '{carpetaId}' in parents and trashed = false";
            req.Fields = "files(id)";
            req.Spaces = "drive";
            req.PageSize = 1;
            var res = await req.ExecuteAsync();
            return res.Files is { Count: > 0 } ? res.Files[0].Id : null;
        }

        // Id de la carpeta propia de la app; la busca y, si no está, la crea (una vez).
        private async Task<string> CarpetaIdAsync()
        {
            if (_carpetaId != null) return _carpetaId;
            await _lockCarpeta.WaitAsync();
            try
            {
                if (_carpetaId != null) return _carpetaId;

                var req = _drive.Files.List();
                req.Q      = $"name = '{_nombreCarpeta}' and mimeType = '{MimeCarpeta}' and trashed = false";
                req.Fields = "files(id)";
                req.Spaces = "drive";
                req.PageSize = 1;
                var res = await req.ExecuteAsync();

                if (res.Files is { Count: > 0 })
                {
                    _carpetaId = res.Files[0].Id;
                }
                else
                {
                    var meta = new DriveFile { Name = _nombreCarpeta, MimeType = MimeCarpeta };
                    var crear = _drive.Files.Create(meta);
                    crear.Fields = "id";
                    var carpeta = await crear.ExecuteAsync();
                    _carpetaId = carpeta.Id;
                }
                return _carpetaId!;
            }
            finally { _lockCarpeta.Release(); }
        }
    }
}
