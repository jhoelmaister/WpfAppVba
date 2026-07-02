namespace VisorEmpresa
{
    /// <summary>
    /// Estado global mínimo de la sesión del visor. A diferencia del AppState de
    /// la app principal, NO hay sucursal activa ni apertura: el visor siempre
    /// trabaja a nivel de empresa completa y es de solo lectura.
    /// </summary>
    public static class VisorState
    {
        public static string UsuarioActivo { get; set; } = "";
        public static string TipoUsuario   { get; set; } = "";
        public static string EmpresaActiva { get; set; } = "";
        public static string TemaActivo    { get; set; } = TemaVisor.TemaClaro;

        // Año global del visor: lo fija el combo de la top bar de la consola y
        // lo leen el dashboard y las 4 vistas de documentos.
        public static int AnioActivo { get; set; } = System.DateTime.Now.Year;
    }
}
