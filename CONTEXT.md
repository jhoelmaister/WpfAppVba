# CONTEXT.md — Sistema de Gestión

## Objetivo General del Proyecto
Aplicación de escritorio Windows (WPF, .NET 8) para gestión empresarial: artículos, pedidos (ventas/compras), traspasos entre sucursales, correcciones de stock, terceros (clientes/proveedores), sucursales, inventarios y precios. Reemplaza un sistema VBA/Excel anterior.

## Stack Tecnológico
- **Framework**: .NET 8.0 Windows / WPF (XAML)
- **Lenguaje**: C# 12 con `Nullable enable` e `ImplicitUsings enable`
- **Base de datos**: SQL Server — acceso vía `Microsoft.Data.SqlClient 7.0.1`
- **Reportes Excel**: `ClosedXML 0.102.2`
- **IDE recomendado**: Visual Studio 2022 / VS Code + extensión C#
- **Proyecto**: `WpfAppVba/WpfAppVba.csproj`
- **Rama de desarrollo activa**: `claude/brave-albattani-03ox62`

## Lo Que Está Implementado y Funciona

### Infraestructura de UI (ConsolaMovimientos)
- Ventana única (`ConsolaMovimientos`) con sidebar de navegación y `TabControl` de pestañas dinámicas.
- Sistema de pestañas con deduplicación por clave (`AbrirPestaña`, `CerrarPestaña`, `CerrarPestañaPorClave`, `SeleccionarPestaña`).
- **Registro de pestañas por sección**: cada sección del sidebar conserva sus propias pestañas abiertas al navegar (`_pestañasPorSeccion`).
- **Memoria de pestaña activa por sección**: al volver a una sección, se restaura la última pestaña enfocada (`_pestañaSeleccionadaPorSeccion`).
- Tema visual dinámico (recursos `ThemeBgPrincipal`, `ThemeBtnSecBg`, etc.) configurable desde Configuración.

### Secciones del sidebar (orden visual)
| Botón | Panel fijo | Detalle en pestaña | Selector en pestaña |
|-------|-----------|-------------------|---------------------|
| 📋 Artículos | `ArticulosGeneral` | `ArticulosDetalle` (pestaña) | — |
| 📑 Pedidos | `PedidosGeneral` | `PedidosDetalle` (pestaña) | TercerosGeneral, ArticulosGeneral |
| 🔄 Traspasos | `TraspasosGeneral` | `TraspasosDetalle` (pestaña) | SucursalesGeneral, ArticulosGeneral |
| 🔧 Correcciones | `CorreccionesGeneral` | `CorreccionesDetalle` (pestaña) | ArticulosGeneral (pestaña) |
| *(separador)* | | | |
| 👥 Terceros | `TercerosGeneral` | `TercerosDetalle` (pestaña) | — |
| 🏢 Sucursales | `SucursalesGeneral` | `SucursalesDetalle` (pestaña) | RegionesGeneral (pestaña-selector) |
| 🗂 Familias | `FamiliasGeneral` | `FamiliasDetalle` (pestaña) | — |
| 📦 Productos | `ProductosGeneral` | `ProductosDetalle` (pestaña) | — |
| 🏭 Industrias | `IndustriasGeneral` | `IndustriasDetalle` (pestaña) | — |
| 🏷 Categorías | `CategoriasGeneral` | `CategoriasDetalle` (pestaña) | — |
| 📊 Inventarios | `InventariosGeneral` | `InventariosDetalle` (pestaña) | ArticulosGeneral (pestaña) |
| 🌐 Regiones | `RegionesGeneral` | `RegionesDetalle` (pestaña) | — |
| 💲 Precios | `PreciosGeneral` | `PreciosDetalle` (pestaña) | RegionesGeneral (pestaña-selector) |
| 🏢 Empresas | `EmpresasGeneral` | `EmpresasDetalle` (pestaña) | — |
| ⚙ Configuración | `Configuracion` (embedded) | — | — |

### Paneles declarados en ConsolaMovimientos
```csharp
private readonly ArticulosGeneral    _panelArticulos     = new();
private readonly PedidosGeneral      _panelPedidos       = new();
private readonly TraspasosGeneral    _panelTraspasos     = new();
private readonly CorreccionesGeneral _panelCorrecciones  = new();
private readonly TercerosGeneral     _panelTerceros      = new();
private readonly SucursalesGeneral   _panelSucursales    = new();
private readonly FamiliasGeneral     _panelFamilias      = new();
private readonly ProductosGeneral    _panelProductos     = new();
private readonly IndustriasGeneral   _panelIndustrias    = new();
private readonly CategoriasGeneral   _panelCategorias    = new();
private readonly InventariosGeneral  _panelInventarios   = new();
private readonly PreciosGeneral      _panelPrecios       = new();
private readonly RegionesGeneral     _panelRegiones      = new();
private readonly EmpresasGeneral     _panelEmpresas      = new();
private readonly Configuracion       _panelConfiguracion = new();
```

### Patrones de comportamiento implementados
- `_iniciado` flag en todos los paneles (evita recarga al cambiar de pestaña).
- `Cerrando` event + `IntentarCerrar()` en todos los detalles embebidos en pestañas.
- Selector tabs deduplicados por contexto: `seleccionar-tercero|{contexto}`, `seleccionar-sucursal|{contexto}`, `buscar-articulo|{contexto}`, `importar-articulos|{contexto}`, `seleccionar-region|{contexto}`.
- Botones de gestión (Nuevo/Editar/Eliminar) ocultos en modo selector; botón "Seleccionar" visible solo en modo selector; "Actualizar" siempre visible.
- Actualización incremental del grid tras crear/editar/eliminar (sin recargar todo).
- **Nombres de botones CRUD específicos por entidad**: todos los formularios General usan etiquetas explícitas ("Nuevo Artículo", "Editar Traspaso", "Eliminar Corrección", etc.).
- **Botones Guardar/Cancelar en detalles**: posición inferior izquierda, estilo uniforme — Guardar `#1A73E8`/blanco, Cancelar `ThemeBtnSecBg`/`ThemeBtnSecFg`, ambos con `Cursor="Hand"`.

## Decisiones de Arquitectura Importantes

### 1. Ventana única (Single-Window UI)
`ConsolaMovimientos` es la **única ventana OS**. Todo se embebe en pestañas. No hay ventanas secundarias de gestión principal. Todos los detalles están embebidos en pestañas; no hay ventanas secundarias de gestión principal.

### 2. Patrón `Cerrando` + `IntentarCerrar`
```csharp
// En el UserControl (detalle):
public event Action? Cerrando;
public void IntentarCerrar() {
    if (!_hayCambios) { Cerrando?.Invoke(); return; }
    var res = MessageBox.Show("¿Guardar cambios?", ...);
    if (res == Yes && Guardar()) Cerrando?.Invoke();
    else if (res == No) Cerrando?.Invoke();
}
// BtnGuardar: if (Guardar()) Cerrando?.Invoke();
// BtnCancelar: _hayCambios = false; Cerrando?.Invoke();
```
El tab X button debe llamar `IntentarCerrar()` en lugar de cerrar directamente (para proteger cambios no guardados). El `Cerrando +=` del abridor hace el cleanup.

### 3. Deduplicación de pestañas por clave
```csharp
consola.AbrirPestaña($"Pedido {docSel}", dlg, $"pedido-{docSel}");
// Si ya existe una pestaña con esa clave, solo la enfoca
```

### 4. Selector tabs con contexto (callback-based)
```csharp
// OpenAsTab con callback — no usa estado global, sino lambda:
RegionesGeneral.OpenAsTab(
    Window.GetWindow(this)!,
    id => { Box_Referido_Codigo.Text = id; },  // callback al seleccionar
    contexto: _tituloTab,
    llamador: this
);
```
Clave de deduplicación: `seleccionar-region|{_tituloTab}`. Un pedido abierto puede tener exactamente un "Seleccionar Tercero (Pedido 34)" y otro "Seleccionar Tercero (Pedido 50)".

### 5. `_iniciado` flag
```csharp
Loaded += (_, _) => { if (_iniciado) return; _iniciado = true; CargarDatos(); ConfigurarModo(); };
```
WPF re-dispara `Loaded` cuando un UserControl se mueve entre pestañas del TabControl. Sin este guard, los datos se recargarían al cambiar de sección.

### 6. `OpenAsTab` (patrón selector)
```csharp
// Método estático en el General:
public static void OpenAsTab(Window ventana, Action<string>? callback, string contexto, UserControl llamador)
```
- Sin callback → modo normal (Nuevo/Editar/Eliminar visibles, Seleccionar oculto).
- Con callback → modo selector (Nuevo/Editar/Eliminar ocultos, Seleccionar visible en pie izquierdo).

### 7. Registro de pestañas por sección
```csharp
private readonly Dictionary<string, List<TabItem>> _pestañasPorSeccion = new()
{
    ["articulos"] = new(), ["pedidos"] = new(), ["traspasos"] = new(),
    ["correcciones"] = new(), ["terceros"] = new(), ["sucursales"] = new(),
    ["familias"] = new(), ["productos"] = new(), ["industrias"] = new(),
    ["categorias"] = new(), ["inventarios"] = new(), ["precios"] = new(),
    ["regiones"] = new(), ["configuracion"] = new(),
};
```
Al cambiar sección: guardar pestañas actuales + pestaña activa → cambiar panel fijo → restaurar pestañas + pestaña activa de la nueva sección.

### 8. Convención de botones CRUD
Todos los `XxxGeneral.xaml` usan etiquetas que incluyen el nombre de la entidad:
- Nuevo/Nueva `{Entidad}` (azul `#1A73E8`)
- Insertar `{Entidad}` (azul, solo en Artículos)
- Editar `{Entidad}` (secundario `ThemeBtnSecBg`)
- Eliminar `{Entidad}` (rojo `#D93025`)
- Actualizar (secundario, siempre visible)
- Seleccionar (visible solo en modo selector)

## Lo Que Falta Por Hacer

### Alta prioridad
- [x] **CorreccionesDetalle → lógica de pestañas**: migrado a UserControl con `Cerrando` + `IntentarCerrar`; selectores usan `OpenAsTab`.
- [x] **ArticulosDetalle → lógica de pestañas**: migrado a UserControl con `Cerrando` + `IntentarCerrar`; selectores usan `OpenAsTab`.
- [ ] Verificar compilación y prueba completa del sistema (no hay herramientas de build en el entorno cloud — debe hacerse en máquina local).

### Media prioridad
- [x] Botón X de las pestañas dinámicas llama `IntentarCerrar()` si el contenido lo implementa (via reflexión), en vez de cerrar directamente.
- [ ] Exportación de datos en otras secciones (actualmente solo ArticulosGeneral tiene "Informe Excel").

### Baja prioridad / Futuro
- [ ] Reportes adicionales en Excel
- [ ] Mejoras de rendimiento en carga de grids grandes

## Problemas Conocidos o Pendientes

- **Sin herramientas de build en el entorno cloud**: todos los cambios se aplican siguiendo patrones existentes, pero no pueden compilarse ni ejecutarse remotamente. Siempre verificar localmente antes de merge a producción.
- **`AppState.TipoPedido` y `AppState.TipoMovimiento` son globales**: en `PedidosGeneral.AbrirEditar` se leen del DB antes de abrir la pestaña para garantizar el valor correcto. Si se abrieran dos pestañas de edición simultáneas muy rápido, podría haber condición de carrera (actualmente no es un problema práctico).
- **`CorreccionesDetalle` ya es pestaña**: usa `OpenAsTab` de ArticulosGeneral para buscar artículos, igual que Pedidos/Traspasos.
- **Rama de trabajo**: todos los cambios van a `claude/brave-albattani-03ox62`. Es la rama activa de desarrollo.

## Historial de Cambios por Sesión

### Sesión 2026-06-12 — Guardado diferencial de líneas de documentos (rama `claude/cool-hopper-vo3mxo`)
- Los detalles de documentos ya NO borran todas las líneas y las recrean al guardar. Ahora hacen un **guardado diferencial**:
  - Línea **nueva** (id de fila vacío) → `Nuevo()` + insertar, con índice libre (`SiguienteIndice`).
  - Línea **existente** (id sigue en la grilla) → `EstablecerItem` sobre su mismo id (UPDATE), conservando id e índice.
  - Línea **quitada** (estaba al abrir y ya no está) → `Ocultar()` (estadof).
- Cada detalle captura los ids existentes al abrir para editar (`_xxxOrig`) y los limpia en modo nuevo.
- Aplicado en: **PedidosDetalle** (pedidos/transacciones/entregas), **TraspasosDetalle**, **CorreccionesDetalle**, **InventariosDetalle**.
- Se eliminaron los métodos/loops de "borrar todo": `EliminarLineas` (Pedidos) y los loops `idsEliminar`+`Eliminar` (Traspasos/Correcciones/Inventarios). Métodos de creación de líneas convertidos a diferenciales (Traspasos: `GuardarLineasTraspaso`; Inventarios: `GuardarLineasInventario`).

### Sesión 2026-06-12 — Índices sin duplicar, empresa en topbar, validación de sucursal (rama `claude/cool-hopper-vo3mxo`)
- **Índices de líneas de documentos sin duplicar** (efecto del borrado lógico): nuevo `DataConsulta.SiguienteIndice(filtroColumna, filtroValor)` = `MAX(indice)+1` **sin filtrar estadof** (cuenta ocultas/eliminadas). Se usa como base en las re-creaciones de líneas de: Pedidos (`pedidos`/`transacciones`/`entregas`), Traspasos (nuevo/editar), Correcciones, Inventarios (nuevo/editar). Cada línea: `indice = base + i`. Antes se reusaba `i+1` y colisionaba con las filas ocultas del mismo documento.
- **TOP BAR**: nuevo `LblEmpresa` ("Empresa: {desc}") a la derecha de `LblSucursal`; se setea en `ActualizarInfoUsuario()` desde `EmpresasObj`.
- **Configuración**: no se puede guardar si `CmbSucursal` está vacío (empresa sin sucursales) → `MessageBox` de advertencia y `return`. Se quitó la rama que ponía `usuarios.sucursal` en NULL (ahora bloqueada).
- **Períodos basados en la máxima fecha de inventario**: `Configuracion.ActualizarPeriodos` ahora calcula el año inicial del desplegable `CmbPeriodo` como el año de la MÁXIMA fecha de inventario de la sucursal (`DocumentosIObj.MaxFecha("sucursal", id)`, consulta directa a SQL); si la sucursal no tiene inventarios, usa el año de `sucursal.fecha`. Nuevo helper `DataConsulta.MaxFecha(filtroColumna, filtroValor)`. Esto evita que se pueda elegir un período anterior al inventario (que recargaba documentos anteriores a la fecha máxima de inventario vía `ActualizarBase`). Ej.: sucursal con `fecha`=01/01/2024 e inventario 19/02/2026 → el desplegable solo muestra 2026.

### Sesión 2026-06-12 — Eliminación de la tabla `stocks` (rama `claude/cool-hopper-vo3mxo`)
- El usuario eliminó la tabla `stocks` en SQL Server. Se quitó del proyecto todo lo que la usaba:
  - `SqlData.StocksObj` (eliminado).
  - `AppLoader.ConectarProductos`: se quitó el `Conectar("stocks", ...)`.
  - `AppState.ActualizarStocks()` (eliminado por completo) y sus 3 llamadas (`ArticulosGeneral` al eliminar, `ArticulosDetalle` al guardar nuevo/editar).
- **No** se tocó el cálculo de stock: `StockCalculator.ContarStock/ContarStock2`, `GridStock`, columnas "Stock", avisos de stock insuficiente — esos calculan con apertura + documentos y NO dependían de la tabla `stocks`.

### Sesión 2026-06-12 — Borrado lógico y signo en Regiones/Sucursales (rama `claude/cool-hopper-vo3mxo`)
- **Borrado lógico global**: `DataConsulta.ExportarItems` ya NO ejecuta `DELETE FROM` físico. Las filas con `estadof` = `"eliminado"` u `"ocultado"` se persisten con un `UPDATE` del `estadof` (se filtran al recargar, que solo trae `estadof='normal'`). Se eliminó el bloque DELETE y la lista `deleteIds`. Afecta a todos los callers de `.Eliminar(...)`/`.Ocultar(...)` (General de maestros, líneas de Pedidos/Traspasos/Correcciones/Inventarios, `ActualizarStocks`). Nota: las tablas pueden acumular filas con estadof≠normal (no se borran nunca).
- **`RegionesDetalle`**: nuevo `Box_Signo` (SIGNO, MaxLength 4, mayúscula) a la derecha de DESCRIPCIÓN; se carga y guarda `regiones.signo`.
- **`SucursalesDetalle`**: nuevo `Box_Signo` (SIGNO, MaxLength 4, mayúscula) a la derecha de DESCRIPCIÓN; se carga y guarda `sucursales.signo`.

### Sesión 2026-06-12 — Formularios de Empresas (rama `claude/cool-hopper-vo3mxo`)
- Nueva sección **Empresas** en el sidebar, entre Precios y Configuración (botón `🏢 Empresas`).
- `EmpresasGeneral` (UserControl): lista con columnas Línea/Código/Descripción/Signo; CRUD "Nueva/Editar/Eliminar Empresa" + Actualizar; patrón estándar (`_iniciado`, `Cerrando`/`IntentarCerrar`, actualización incremental, `OpenAsTab` selector con clave `seleccionar-empresa|{contexto}`). Usa `Sql.EmpresasObj`.
- `EmpresasDetalle` (UserControl): campos Código (int, autoincremental con `SiguienteCodigoInt()`, read-only al editar), Descripción, Signo (NVARCHAR(4), forzado a mayúscula), Observación. Valida `CodigoExiste()` al crear. Persiste `codigo, descripcion, signo, observacion, fecha, emision, edicion, usuario, usuarioE`.
- `ConsolaMovimientos`: panel `_panelEmpresas`, sección `"empresas"` en los diccionarios de pestañas, caso en `MostrarPanel`, handler `BtnNav_Empresas_Click`.
- La columna `codigo` (INT) de `empresas` fue agregada por el usuario en SQL Server.

#### Configuración — empresa activa + refresco sin logout
- Nuevo `CmbEmpresa` ("EMPRESA ACTIVA") a la izquierda de `CmbSucursal`. `CmbSucursal` es **dependiente** de la empresa: al cambiar empresa se repueblan las sucursales mediante una consulta directa (`DataConsulta` temporal `_sucursalesEmpresa`), ya que el caché global está filtrado por la empresa activa.
- `ActualizarFechaInicio`/`ActualizarPeriodos` leen la fecha desde `_sucursalesEmpresa` (no del caché global).
- **Guardar ya NO cierra sesión.** Si cambia empresa/sucursal/periodo se recargan los cachés (`ConectarProductos` si cambió empresa, `ConectarBases`, `ActualizarBase`, `ConectarDocumentos`) y se llama `ConsolaMovimientos.RecargarContexto()`, que cierra las pestañas dinámicas y **recrea los paneles General** para que relean los cachés — manteniendo el enfoque en Configuración (como si recién se hubiera iniciado sesión). Tras recargar, `Configuracion.CargarDatos()` repuebla sus propios combos.
- La empresa elegida se persiste en `usuarios.empresa` (queda como predeterminada en el próximo login), igual que la sucursal.
- Se eliminó `CerrarSesionYReabrirLogin` (ya no se usa). Los paneles General de `ConsolaMovimientos` pasaron de `readonly` a mutables para poder recrearlos.

#### MovimientosGeneral — columna "Movimiento" muestra código
- `CargarMovimientos` ahora guarda `DocumentoCodigo` (de `Documentos[P/T/C]Obj.ObtenerItem("codigo", id)`) y la columna "Movimiento" muestra `código-tipo` en vez de `id(UUID)-tipo`.

#### Diseño esquinas redondeadas
- `EmpresasGeneral` y `RegionesGeneral` actualizados al estilo redondeado unificado: botones con `ControlTemplate CornerRadius=6`, `SearchInput` (TextBox) redondeado y `DataGrid` envuelto en `<Border CornerRadius="6">`.

#### Limpieza
- Eliminado `MovimientosWindow.xaml`/`.cs` (versión Window legacy del visor de movimientos, sin referencias; reemplazada por `MovimientosGeneral`). Las clases `MovimientoDato`/`MovimientoFila` se movieron a `MovimientosGeneral.xaml.cs`.

#### Ajustes
- Configuración: al cambiar a una empresa sin sucursales y guardar, `usuarios.sucursal` se deja en **NULL** (rama `else` que llama `EstablecerItem(...,"")`, que persiste como NULL). Antes no se actualizaba si no había sucursal seleccionada.
- `EmpresasDetalle`: `Box_Observacion` ahora alinea el texto arriba (TextBox plano dentro de `Border`, igual que `TercerosDetalle`) en vez de centrado.
- **`AppLoader.ConectarBases`/`ConectarDocumentos`**: si `AppState.SucursalActiva` está vacía, se usa el GUID nulo `00000000-0000-0000-0000-000000000000` en vez de `''`. Antes, `sucursal = ''` contra una columna `uniqueidentifier` lanzaba *"Conversion failed when converting from a character string to uniqueidentifier"* (al guardar sin sucursal en Configuración y también en el login siguiente). Esa excepción abortaba el guardado antes de `RecargarContexto`, por lo que la consola tampoco se refrescaba (pestañas/grids quedaban cargados). Con el fix, el guardado completa y `RecargarContexto` limpia pestañas y recrea los paneles.

### Sesión 2026-06-12 — Empresas, regeneración de códigos y UI de conexión (rama `claude/brave-albattani-03ox62`)

#### Nueva entidad: Empresa (multi-empresa)
- Cambios de esquema hechos por el usuario en SQL Server:
  - Nueva tabla `empresas` (`id` UNIQUEIDENTIFIER, `secuencia` INT IDENTITY, `descripcion`, `signo` NVARCHAR(4), `observacion`, `fecha`, `emision`, `edicion`, `usuario`, `usuarioE`, `estadof`).
  - Nueva columna `empresa` (UNIQUEIDENTIFIER) en: `usuarios, sucursales, articulos, categorias, familias, industrias, productos, regiones, stocks, terceros`.
  - Todas las tablas tienen `id` con `DEFAULT NEWID()` y una columna `secuencia` IDENTITY (para que la herramienta SQL ordene sin tocar el `id`).
- `AppState.EmpresaActiva` (string); se setea al iniciar sesión desde `usuarios.empresa` y se limpia en logout (`ConsolaMovimientos`, `Configuracion`).
- `SqlData.EmpresasObj`; `AppLoader.ConectarProductos` carga la tabla `empresas`.
- **Filtro por empresa en la carga de caché** (`ConectarProductos`, solo cuando hay empresa activa):
  - Filtro directo `empresa = '{guid}'`: `stocks, articulos, familias, productos, Categorias, industrias, terceros, sucursales, regiones`.
  - `usuarios`: SIN filtro (necesario para el login).
  - `precios` (sin columna empresa): cascada `region IN (SELECT id FROM regiones WHERE empresa = '{guid}')`.
  - Tras autenticar se vuelve a llamar `ConectarProductos()` ya filtrado por la empresa del usuario.
- **Documentos**: se evaluó cargarlos por todas las sucursales de la empresa (cascada empresa→sucursales) pero **se revirtió**; siguen filtrándose por **sucursal activa + rango de fechas** (commit revert `4eeb572`). `ConectarBases`/`ConectarDocumentos` quedaron como antes.

#### Conexión SQL Server: formulario propio (extraído de Configuración)
- Nuevo `ConexionServidoresWindow` (Window): lista/agregar/editar/eliminar/conectar servidores (antes era el CARD "CONEXIÓN SQL SERVER" embebido en `Configuracion`).
- Se **eliminó** ese card de `Configuracion.xaml`/`.cs` (y el estilo `SelectorBtn` y todo el code-behind de servidores); el grid de Configuración quedó a una sola columna (GENERAL + MI CUENTA).
- `LoginWindow.BtnConfigurarConexion_Click` abre `ConexionServidoresWindow` (en vez de `ConfiguracionDbWindow`); al cerrar recarga `DatabaseConnection.CargarDesdeConfiguracion()` y reconecta. `ConfiguracionDbWindow` se conserva (diálogo de un solo servidor usado por Agregar/Editar).
- **Conectar** no cierra el formulario: marca el servidor activo, reconfigura `DatabaseConnection` y refresca la lista (queda abierto; se cierra con "Cerrar").
- Fix de layout: el `DataGrid` usa `MinHeight` y ocupa el espacio (`Grid` con filas), para que los botones Conectar/Editar/Eliminar queden siempre visibles.

#### Regeneración de códigos (`CodigoRegenerator` + botón en `ConexionServidoresWindow`)
- Botón "🔢 Regenerar códigos" (a la derecha de Eliminar) que ejecuta `CodigoRegenerator.RegenerarTodos()` en una transacción sobre el servidor activo. Reescribe **todas** las filas (sin filtro `estadof`).
- Maestras (`usuarios, familias, productos, Categorias, industrias, terceros, sucursales, regiones`): `codigo = 1..N` (`ROW_NUMBER() OVER (ORDER BY id)`).
- `documentosI/P/C`: `codigo = signo_sucursal + correlativo por sucursal`.
- `documentosT` (traspasos): `codigo = signo_empresa + correlativo por empresa`, vía cascada `emitido → sucursales.empresa → empresas.signo` (documentosT NO tiene columna `sucursal`; tiene `origen/destino/emitido`).
- `precios`: `codigo = signo_region + correlativo por región`.
- Los signos siempre en **MAYÚSCULA** (`UPPER(...)`).

#### Códigos: signo en mayúscula y visualización del código completo
- Al construir el código en los detalles (`Pedidos/Traspasos/Inventarios/Correcciones/Precios`) el signo va en mayúscula (`signo.ToUpper()`).
- **Traspasos**: el signo del código sale de `empresas.signo` (no `sucursales.signo`); nuevo método `DataConsulta.SiguienteNumeroDocPorEmpresa(signo, empresaId)` (cascada `emitido→sucursales.empresa`) para numerar el traspaso nuevo por empresa. Corrige el error "Invalid column name 'sucursal'" al crear un traspaso.
- `Box_Documento*` (Pedidos/Traspasos/Correcciones/Inventarios) muestra el **código completo** (signo+número) en modo nuevo y editar (antes mostraba solo el número).
- Grids "General": la columna "Documento" muestra el **código** del documento (propiedad `Codigo` agregada a `PedidoFila/TraspasoFila/CorreccionFila/InventarioFila`); la clave interna (`DocumentoP/DocumentoT/Id`) no cambia.
- Cabecera del panel de detalle (Pedidos/Traspasos/Correcciones) muestra el código del documento.
- Título de la pestaña de edición usa el **código** (no el id UUID); la clave de deduplicación sigue usando el id.

#### ConsolaMovimientos — top bar
- `LblUsuario` muestra el **nombre** del usuario (`usuarios.nombres`) en vez del id; conserva el Período.
- Nuevo `LblSucursal` a la derecha con `Sucursal: {descripción}` de la sucursal activa.

#### Persistencia — columna IDENTITY `secuencia`
- `DataConsulta.ExportarItems` **excluye** la columna autogenerada `secuencia` (helper `EsAutogenerada` + `ColumnasPersistibles`) de INSERT y UPDATE, para evitar "Cannot insert/update identity column 'secuencia'". El `id` se sigue insertando explícitamente (convive con `DEFAULT NEWID()`).
- Pendiente conocido: si se agrega un alta de `empresas` desde el app, habría que asegurarse de que su `secuencia` IDENTITY también quede excluida (ya lo está por nombre).

### Sesión anterior (antes de compactación)
- Precios migrado a panel sidebar + pestañas (`PreciosGeneral`, `PreciosDetalle`).
- Regiones: nueva sección en sidebar entre Inventarios y Precios (`RegionesGeneral`, `RegionesDetalle`).
- Sucursales: selector de región vía `RegionesGeneral.OpenAsTab` con callback (sin estado global).
- Inventarios migrado a panel sidebar + pestañas (`InventariosGeneral`, `InventariosDetalle`); selección de artículos vía `ArticulosGeneral.OpenAsTab`.
- Configuración: ya tenía soporte embedded en `BtnGuardar_Click`; no requirió cambios de código.

### Sesión anterior (antes de compactación)
- **SucursalesDetalle**: botones Guardar/Cancelar movidos a inferior izquierda con estilo uniforme (`#1A73E8`, `ThemeBtnSecBg`/`ThemeBtnSecFg`, `Cursor="Hand"`), igual a TercerosDetalle.
- **Todos los formularios General**: botones CRUD renombrados para incluir el nombre de la entidad (e.g., "Nuevo Traspaso", "Editar Corrección", "Eliminar Artículo").

### Sesión anterior (antes de compactación)
- **Botón X de pestañas dinámicas**: ahora llama `IntentarCerrar()` vía reflexión si el contenido lo implementa, protegiendo cambios no guardados antes de cerrar.

### Sesión 2026-06-11 — Refactor id/codigo (commit c5002d6)
Separación completa del campo `id` interno (UUID) del campo `codigo` visible al usuario en todos los formularios. **41 archivos modificados.**

#### Infraestructura
- **`AppState.cs`**: `SucursalActiva`, `RegionActiva`, `UsuarioActivo`, `AperturaIdActiva` cambiados de `long` a `string`.
- **`DataConsulta.cs`**:
  - `BuscarIdentificador()` ahora devuelve `string` en vez de `long`.
  - Nuevo método `SiguienteCodigoInt()`: `MAX(CAST(codigo AS INT)) + 1` para tablas maestras.
  - Nuevo método `SiguienteNumeroDoc(signo, filtroColumna, filtroValor)`: siguiente número de documento filtrado por sucursal o región activa; devuelve solo el número (sin el signo).
  - Nuevo método `CodigoExiste(codigo, idActual?)`: verifica duplicados antes de guardar.
  - Fix DELETE: ids UUID se citan correctamente en SQL (`'uuid'` con comillas).
- **`AppLoader.cs`, `StockCalculator.cs`, `LoginWindow.xaml.cs`, `Configuracion.xaml.cs`, `ConsolaMovimientos.xaml.cs`**: actualizados para usar `AppState` con campos `string`.

#### Formularios maestros (Productos, Industrias, Categorías, Regiones, Terceros, Familias, Sucursales, Artículos)
- Modo nuevo: `id = Guid.NewGuid().ToString()`; `codigo` = entero autoincremental sugerido via `SiguienteCodigoInt()`, editable por el usuario.
- Modo editar: usa `_idEditar` (UUID) como clave interna; `Box_Codigo` muestra el `codigo` legible.
- Validación: `CodigoExiste()` antes de guardar nuevo; si ya existe, sugiere el siguiente disponible.
- `OrdenarData(("codigo", false))` en todos (antes `("id", false)`).
- Campos FK en formularios: muestran `codigo` del registro referenciado (no el UUID). Al guardar, se resuelve el UUID internamente con `BuscarIdentificador("codigo", cod)`.
- Helpers `ResolverXxxId()` agregados en los formularios con FK (Familias→Producto, Sucursales→Región, Artículos→Familia/Industria/Categoría).

#### Formularios de documentos (Pedidos, Traspasos, Correcciones, Inventarios, Precios)
- `codigo` del documento = `signo_sucursal + numero` (ej. `"A5"`); al usuario solo se muestra el número (`"5"`); el campo Box_DocumentoX queda deshabilitado.
- `id` del documento = `Guid.NewGuid().ToString()` (nunca visible).
- Precios: usa `signo_region` (de `AppState.RegionActiva`) en vez de signo de sucursal.
- Sub-registros de líneas (pedidos, trasacciones, entregas, corrección-líneas, inventario-líneas): también usan `Guid.NewGuid().ToString()` como id (antes `$"{docP}{i:D3}"`).
- FK de tercero: `Box_Tercero_Identificador` muestra `codigo` del tercero; se resuelve UUID con `ResolverTerceroId()` al guardar.
- Eliminado `VerificarId()` para documentos; la generación automática de UUID garantiza unicidad.

#### Formularios General (todas las secciones)
- `Seleccionar()` devuelve `fila.Codigo` (antes `fila.Id`).
- Títulos de pestañas usan `fila.Codigo` (ej. `"Artículo A123"` en vez de UUID).
- Columnas de DataGrid vinculadas a `Binding="{Binding Codigo}"` donde antes usaban `Id`.
- `ConstruirFila()`: poblada con `Codigo = Sql.XxxObj.ObtenerItem("codigo", id)?.ToString() ?? ""`.

#### Limpieza
- Eliminadas todas las llamadas redundantes `.ToString()` sobre propiedades `string` de `AppState` en: `PedidosGeneral`, `TraspasosGeneral`, `CorreccionesGeneral`, `MovimientosGeneral`, `MovimientosWindow`, `InventariosGeneral`.

### Sesión 2026-06-10 — rama `master`

#### Accesos rápidos en top bar (ConsolaMovimientos)
- Añadidos 4 botones en la barra superior (derecha del usuario): **Venta** (rojo `#E53935`), **Compra** (azul `#1E88E5`), **Salida** (naranja `#FB8C00`), **Entrada** (verde `#43A047`).
- Estilo `QuickBtn` con `CornerRadius="6"`, `Height="32"`, separador visual antes de `LblUsuario`.
- Venta/Compra abren `PedidosDetalle` (tipo rápido) forzando `TipoMovimiento`; Salida/Entrada abren `TraspasosDetalle`.
- **Si ya existe pestaña rápida** (`"nuevo-pedido"` / `"nuevo-traspaso"`): en lugar de abrir una nueva, se llama `CambiarTipoMovimiento(tipo)` en el detalle existente y se enfoca esa pestaña.
- `PedidosDetalle.CambiarTipoMovimiento(string tipo)`: sets `AppState.TipoMovimiento` + `CboMovimiento.SelectedIndex`.
- `TraspasosDetalle.CambiarTipoMovimiento(string tipo)`: sets `AppState.TipoMovimiento` + `CboMovimiento.SelectedIndex`.
- `PedidosGeneral.AbrirNuevoPedido` made public, acepta `tipoMovimiento` opcional.
- `TraspasosGeneral.AbrirNuevoTraspaso` made public, acepta `tipoMovimiento` opcional.

#### Configuracion — reorganización de layout y botones
- Grid 2 columnas: izquierda (GENERAL fila 0 + MI CUENTA fila 1), derecha (CONEXIÓN SQL SERVER RowSpan=2).
- **Botón "Guardar Cambios"** movido al card MI CUENTA; guarda solo nombres, apellidos, sucursal y periodo.
- **Botón "Cambiar Contraseña"** movido a la barra de acciones inferior.
- Si se cambia la **sucursal activa** al guardar → cierra sesión y reabre `LoginWindow`.
- `BtnConectarServidor`: seleccionar servidor diferente al activo → confirma, `EstablecerActivo`, luego `CerrarSesionYReabrirLogin()`.
- Editar servidor activo después de guardar → `CerrarSesionYReabrirLogin()`.
- Agregar/editar servidor no-activo → solo registra, no conecta.

#### PreciosGeneral — columna Código y título de pestaña
- Añadida columna "Código" en `GridPrecios` (entre Línea y Fecha).
- La columna muestra el **ID del registro de precio** (de la tabla precios), no el código del artículo.
- Título de pestaña de edición: `"Precio {id}"` en lugar del código del artículo.

#### InventariosGeneral — botón Actualizar
- Añadido botón **Actualizar** a la derecha de "Eliminar Inventario".
- Llama `CargarInventarios()` para refrescar la lista.

#### TraspasosGeneral — métricas de estado
- Métrica **"Estado pendientes"**: ahora cuenta solo `estado == "pendiente"` (antes incluía "pendiente revisar").
- Métrica **"Pendientes revisar"** (renombrada de "Entregados"): cuenta `estado == "pendiente revisar"`.

#### Detalles de formularios — esquinas redondeadas y layout
- **ProductosDetalle, RegionesDetalle, CategoriasDetalle, IndustriasDetalle**: código + descripción lado a lado (`Grid 160|16|*`).
- **SucursalesDetalle, TercerosDetalle, PreciosDetalle, FamiliasDetalle, ProductosDetalle, RegionesDetalle, CategoriasDetalle, IndustriasDetalle**: `ModernInput` con `ControlTemplate CornerRadius="6"`; estilos `BtnBase`/`BtnPrimario`/`BtnSecundario`.
- **ConfiguracionDbWindow**: trigger `IsEnabled=False` → `Opacity=0.45` en `BtnBase`.

#### Rama de trabajo
- Esta sesión trabajó directamente en `master` (por instrucción del usuario).
- Los commits son verificados (`user.email noreply@anthropic.com`).

---

### Sesión 2026-06-09 (parte 1) — rama `master`
> Nota: esta sesión trabajó directamente en `master` por instrucción del usuario.

#### Rediseño general de vistas (esquinas redondeadas)
- **TercerosGeneral, SucursalesGeneral, FamiliasGeneral, ProductosGeneral, IndustriasGeneral, CategoriasGeneral, InventariosGeneral, PreciosGeneral**: aplicado estilo unificado de esquinas redondeadas.
  - Estilo `Btn` con `ControlTemplate` + `CornerRadius="6"` + triggers hover/pressed.
  - Estilo `SearchInput` (TextBox) con `CornerRadius="6"`.
  - DataGrids envueltos en `<Border CornerRadius="6">`.
- **PreciosDetalle**: rediseñado siguiendo el estilo de TercerosDetalle.
- **ConfiguracionDbWindow**: UX "test before save" — botón "Probar conexión" antes de guardar, mensaje de estado inline.
- **LoginWindow**: abre diálogo de configuración en modo edición cuando ya existe un servidor configurado.
- **DatabaseConnection.cs**: suprimido warning IDE0130 con directiva `#pragma warning`.

#### PedidosDetalle — rediseño completo
> El primer rediseño fue revertido por el usuario al commit `0f302660`. Luego se aplicó el diseño final aprobado.

**Estructura XAML (3 filas):**
- **Row 0 — Encabezado** (`ThemeBgSurface`, borde inferior 1px, Padding 14,10):
  - Fila superior: ícono `LblIconoTipo` (V/C), `LblTitulo`, `LblDocBadge`, `BadgeEstado`, `BadgeCuenta`, botones Cancelar + Guardar.
  - Fila de campos: Doc. Pedido, Fecha, Hora, Tercero (id + botón `···` + descripción read-only), Referencia, ComboBox Tipo.
  - Fila secundaria: Emisión (`Box_Emision`), Edición (`Box_Edicion`), `Box_Estado` y `Box_Cuenta` como `Visibility="Collapsed"` (usados por code-behind, visualmente en badges).
- **Row 1 — TabControl** (3 tabs: Artículos, Cobros/Pagos, Entregas):
  - **Tab Artículos**: toolbar + `GridItems` + panel inferior de 7 columnas (`260/1/240/1/160/1/*`):
    - Col 0: `GridPrecios` (Historial de precios)
    - Col 2: `GridStock` (Stock actual)
    - Col 4: `GridCategorias` (lista de categorías — añadida en esta sesión)
    - Col 6: `Box_Observaciones`
  - **Tab Cobros/Pagos**: toolbar + `GridTrasacciones`.
  - **Tab Entregas**: toolbar + `GridEntregas`.
- **Row 2 — Barra de totales** (Grid con dos grupos):
  - **Izquierda**: `TxtTotalUnidades` (Total unidades) + `TxtUnidadesDiferentes` (Diferentes) — números alineados a la derecha dentro de cada tarjeta `MetricCard`.
  - **Derecha**: `TxtTotalImporte` (verde `#065F46`) + `TxtTotalCuenta` (azul `#1E40AF`) + `TxtTotalSaldo` (rojo `#991B1B`) — números alineados a la derecha.

**Code-behind destacado:**
- `ActualizarBadges()`: colorea dinámicamente `BadgeEstado`/`BadgeCuenta` con `SolidColorBrush(Color.FromRgb(...))` según valores de `Box_Estado`/`Box_Cuenta`. Actualiza `LblIconoTipo` (V/C) y `LblDocBadge`.
- `CargarTotalesCategoria()`: lee todas las categorías de `Sql.CategoriasObj`; suma cantidades de `_pedidos` por categoría (campo `"categoria"` del artículo); líneas sin `ArticuloId` o sin categoría reconocida → "Otros". Asigna a `GridCategorias.ItemsSource`.
- `ActualizarTotales()` llama: `CargarTotales()`, `CargarTotalesDivisas()`, `CargarEstadosCuenta()`, `CargarTotalesCategoria()`, y `CargarEstados()` si `tipoPedido=="normal"`.
- `CategoriaCantFila`: clase pública compartida por PedidosDetalle, CorreccionesDetalle, TraspasosDetalle e InventariosDetalle para filas de `GridCategorias`.

**Correcciones realizadas:**
- CS0101: `CategoriaFila` duplicada con `CategoriasGeneral` → renombrada a `CategoriaCantFila` en PedidosDetalle.
- Normalización `.ToLower()` en `PedidosGeneral.xaml.cs` al leer `estado`/`estadoC` desde DB (previene badges negros por case mismatch).
- `GridEntregas_CellEditEnding` y `GridTrasacciones_CellEditEnding`: leen manualmente de TextBox antes de que WPF haga commit (patrón para `DataGridTextColumn` numérico + `RefrescarGrid` via `Dispatcher.BeginInvoke`).
- `FechaDate` como propiedad calculada en `TrasaccionItemFila` y `EntregaItemFila` para que el DatePicker siempre tenga valor al editar.
- `"entrega parcial"` → `"pendiente parcial"` en todo PedidosDetalle.
- Badge colors: pendiente=rojo, pendiente parcial=amarillo, entregado/cancelado=verde.
- `"Pend. parcial"` → `"Pendiente parcial"` en botón de filtro de PedidosGeneral.

### Sesión 2026-06-09 (parte 3) — rama `master`

#### CorreccionesDetalle — Stock actual al lado de Observación
- Row 3 ampliado a 140 px; panel horizontal: `GridStock (260px) | separador 1px | Observación (*)`.
- `GridItems_SelectionChanged` → `CargarStock()` usa `StockCalculator.ContarStock` igual que TraspasosDetalle.
- Nueva clase `CorreccionStockFila`.

#### TraspasosDetalle — GridCategorias siempre cargado
- `CargarTotales()` itera primero `Sql.CategoriasObj` para crear todas las filas con cantidad 0, luego acumula desde `_items`. Así el grid muestra todas las categorías desde el inicio aunque no haya artículos.

#### Movimientos — convertido a UserControl + sección en sidebar
- `MovimientosGeneral` (nuevo UserControl): mismo contenido que `MovimientosWindow` pero embebible en pestañas.
  - `OpenAsTab(Window, codigoArticulo)` abre como pestaña con clave `movimientos-{codigo}`.
  - Si `codigoFijo != ""` (invocado desde ArticulosDetalle): `TxtCodigo.IsEnabled = false`, `BtnBuscar.IsEnabled = false`.
- `ConsolaMovimientos`: nuevo botón `📈 Movimientos` en sidebar entre Correcciones y el separador; sección `"movimientos"` registrada en los diccionarios de pestañas.
- `ArticulosDetalle.BtnVerMovimientos_Click`: reemplaza `new MovimientosWindow(...).ShowDialog()` → `MovimientosGeneral.OpenAsTab(...)`.
- `MovimientosWindow` permanece en el proyecto (no se eliminó) para no romper referencias pendientes.

### Sesión 2026-06-09 (parte 2) — rama `master`

#### TraspasosDetalle — rediseño completo + mejoras
**Estructura XAML (5 filas):**
- **Row 0 — Encabezado** (`ThemeBgSurface`, borde inferior): ícono dinámico (SA/EN) + título + `LblDocNum` + `BadgeEstado` + Guardar/Cancelar en fila superior; todos los campos en línea en fila inferior: Doc. Traspaso · Fecha · Hora · Sucursal (código + botón `···` + descripción readonly) · Referencia · Estado · Tipo; emisión/edición al pie.
- **Row 1 — Toolbar** (`ThemeBgSurface`, borde inferior): label "Artículos del traspaso" + botones (Importar artículos / Buscar artículo / Insertar / Nueva línea / Eliminar línea).
- **Row 2 — GridItems**: DataGrid a todo el ancho, sin borde exterior, `RowHeight=32`, `ColumnHeaderStyle` unificado.
- **Row 3 — Stock + Observaciones** (altura 140): panel horizontal — Stock (260px) | separador 1px | Observaciones (*); ambos con `Border CornerRadius="6"`.
- **Row 4 — Barra de totales** (`ThemeBgSurface`, borde superior): `MetricCard` Total unidades + `MetricCard` Diferentes + `MetricCard` Categorías (DataGrid `GridCategorias` 170×78px).

**Estilos en `UserControl.Resources`:**
- `BtnBase` con `ControlTemplate` + `CornerRadius="6"` + triggers hover/pressed.
- `ModernInput` (TextBox): ControlTemplate con `CornerRadius="6"`, `PART_ContentHost`.
- `MetricCard` (Border): `CornerRadius="8"`, `Padding="10,8"`.
- `LblMuted`, `SectionHeader`: igual al patrón de PedidosDetalle.

**Code-behind destacado:**
- `ActualizarBadgeEstado()`: badge de estado (pendiente=amarillo, pendiente revisar=rojo, entregado=verde); ícono dinámico (SA=naranja, EN=verde); `LblSucursalTipo` dinámico ("Sucursal destino"/"Sucursal origen"); `LblDocNum` actualizado. Llamado desde `CargarParaEditar`, `CargarParaNuevo`, `Campo_SelectionChanged` (when `Box_Estado`), `CboMovimiento_SelectionChanged`.
- `CargarTotales()`: ya no usa categorías fijas (Peq/Med/Gra/Otros); agrupa por categoría real de la DB y asigna `CategoriaCantFila` a `GridCategorias.ItemsSource`.
- **Validación sucursal activa**: `Box_Sucursal_Identificador_TextChanged` y `BtnBuscarSucursal_Click` bloquean seleccionar `AppState.SucursalActiva` como destino/origen (muestra advertencia y limpia el campo).

#### CorreccionesDetalle — rediseño completo
**Estructura XAML (5 filas):**
- **Row 0 — Encabezado**: ícono dinámico (EG/IN) + título + `LblDocNum` + `BadgeEstado` + Guardar/Cancelar; campos en línea: Doc. Corrección · Fecha · Hora · Movimiento · Motivo · Referencia.
- **Row 1 — Toolbar**: label + botones artículos.
- **Row 2 — GridItems**: DataGrid a todo el ancho.
- **Row 3 — Observación** (altura 90): `Box_Observacion` en borde redondeado.
- **Row 4 — Barra de totales**: MetricCard Unidades + Diferentes + GridCategorias.

**Code-behind:**
- `ActualizarBadge()`: ingreso=verde/"IN", egreso=rojo/"EG"; actualiza ícono, badge y `LblDocNum`. Llamado al cargar y en `Box_Movimiento_SelectionChanged`.
- `LblDocNum` se actualiza en vivo en `Campo_TextChanged` cuando `sender == Box_DocumentoC`.

#### InventariosDetalle — rediseño completo
**Estructura XAML (5 filas):**
- **Row 0 — Encabezado**: ícono fijo "IV" (azul `#1E40AF` / fondo `#DBEAFE`) + título + `LblDocNum` + badge fijo "Inventario físico" (azul) + Guardar/Cancelar; campos en línea: Doc. Inventario · Fecha · Hora.
- **Row 1 — Toolbar**: label + botones artículos.
- **Row 2 — GridItems**: DataGrid a todo el ancho.
- **Row 3 — Observaciones** (altura 90): `Box_Observacion` en borde redondeado.
- **Row 4 — Barra de totales**: MetricCard Unidades + **Diferentes** (nuevo) + **GridCategorias** (nuevo).

**Code-behind:**
- Nuevo `CargarTotalesCategoria()`: mismo patrón que CorreccionesDetalle — itera `Sql.CategoriasObj`, acumula por artículo, añade fila "Otros", asigna a `GridCategorias.ItemsSource`.
- `RefrescarGrid()` ahora también calcula `TxtUnidadesDiferentes` y llama `CargarTotalesCategoria()`.
- `LblDocNum` actualizado en `CargarUserform()` y en `Campo_TextChanged` cuando `sender == Box_DocumentoI`.

#### Sistema de estilos compartido (TraspasosDetalle → Correcciones → Inventarios)
Todos comparten el mismo modelo visual:
```
Encabezado surface   [ícono][título + doc]  [badge]  [Cancelar][Guardar]
                     [campo1][campo2]...[campoN]
─────────────────────────────────────────────────────────────────────────
Toolbar surface      Artículos de...         [btn][btn][btn][btn][peligro]
─────────────────────────────────────────────────────────────────────────
GridItems (*)        Lín. | Código | Descripción | Cantidad
─────────────────────────────────────────────────────────────────────────
Panel inferior       Observación / Stock + Observaciones
─────────────────────────────────────────────────────────────────────────
Totales surface      [MetricCard Unidades] [MetricCard Diferentes] [MetricCard Categorías]
```
## Comandos Importantes

### Clonar y abrir el proyecto
```bash
git clone <repo-url>
cd WpfAppVba
git checkout sistemaControl_1.5
# Abrir WpfAppVba/WpfAppVba.csproj en Visual Studio 2022
```

### Compilar
```bash
cd WpfAppVba
dotnet build WpfAppVba.csproj
```

### Ejecutar
```bash
cd WpfAppVba
dotnet run --project WpfAppVba.csproj
# O desde Visual Studio: F5 / Ctrl+F5
```

### Git (rama activa)
```bash
git checkout claude/brave-albattani-03ox62
git pull origin claude/brave-albattani-03ox62
git push -u origin claude/brave-albattani-03ox62
```

### Restaurar paquetes NuGet (si es necesario)
```bash
cd WpfAppVba
dotnet restore
```
