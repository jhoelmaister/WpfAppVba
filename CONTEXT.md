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
- **Rama de desarrollo activa**: `claude/optimistic-dirac-5arp05`

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
| 📊 Dashboard | `DashboardGeneral` | — | — |
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
| 👤 Usuarios | `UsuariosGeneral` | `UsuariosDetalle` (pestaña) | — |
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
private          DashboardGeneral    _panelDashboard     = new(); // sección "dashboard"
private          UsuariosGeneral     _panelUsuarios      = new(); // solo admin; recreado en RecargarContexto
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
- **Rama de trabajo**: la sesión actual trabaja en `claude/cool-hopper-vo3mxo`. (La sesión previa de multi-empresa fue `claude/brave-albattani-03ox62`.)

## Historial de Cambios por Sesión

### Sesión 2026-06-18 (parte 2) — Fix de paquetes delta, bloqueo por actualización pendiente y auditoría de seguridad (login + contraseñas) (rama `master`)

> Esta sesión trabajó directamente en `master` (continuación de una sesión previa con autorización ya otorgada). Cuatro commits: `863f0c8`, `ce27f98`, `021c623`, `4518260`, `6027c12`.

#### Fix de paquetes delta en releases (Velopack)
- **Problema**: cada corrida de GitHub Actions arranca de un checkout limpio, así que `vpk pack` nunca encontraba el `.nupkg` de la versión previa y solo generaba el paquete **full** (descarga completa aunque no hubiera cambios).
- **`.github/workflows/release.yml`**: se agrega `vpk download github` (trae la última release publicada) **antes** de `vpk pack`, para que Velopack genere correctamente el delta.
- **`ActualizadorApp.cs` (`TamañoDescargaMB`)**: antes siempre leía `TargetFullRelease.Size` (mostraba el peso de la app entera en la barra de progreso aunque se estuviera descargando un delta mucho más chico). Ahora suma `DeltasToTarget` cuando hay deltas disponibles, y solo cae al tamaño full si no los hay.

#### Bloqueo de la app mientras hay una actualización pendiente
- **Login (`LoginWindow`)**: reutiliza el patrón de actualización de `ConsolaMovimientos`. Al cargar, si `_actualizador.HayActualizacionAsync()` detecta versión nueva, se oculta el formulario de login y se muestra un bloque de actualización obligatoria (descargar + reiniciar) hasta que el usuario actualice — no se puede iniciar sesión con una versión vieja.
- **`AppState.VersionPendiente`** (nuevo, `string?`): bandera global que se setea en `ConsolaMovimientos.BuscarActualizacionesAsync()` cuando hay una versión nueva disponible.
- **`FuncionesComunes`**: nuevo `HayActualizacionPendienteOAvisa(owner)`, integrado en `VerificarConexionParaGuardar` **y** `VerificarConexionParaActualizar` (decisión explícita del usuario: bloquear también los botones "Actualizar" de refresco de cada grid, no solo Guardar/Eliminar). Como estos dos guards ya se usaban en los 16 módulos + Configuración + CambiarContraseña, el bloqueo quedó centralizado sin tocar cada panel. La navegación entre secciones/pestañas sigue funcionando con normalidad; solo se bloquean escritura y refresco de datos.

#### Auditoría de seguridad + hashing de contraseñas + login sin volcado completo de `usuarios`
> A pedido del usuario se hizo una auditoría de seguridad de la app. Hallazgos principales: contraseñas en texto plano, descarga de toda la tabla `usuarios` (con contraseñas de todas las cuentas) antes de autenticar, `TrustServerCertificate=True` en `DatabaseConnection` (pendiente, no se tocó), sin protección anti fuerza bruta (pendiente), releases sin firmar (pendiente). Se implementaron los dos primeros puntos (prioridad alta):

- **`PasswordHasher.cs`** (nuevo, `WpfAppVba.Data`): `Hashear(contrasena)` / `Verificar(contrasena, valorAlmacenado)` con PBKDF2-SHA256, 100.000 iteraciones, salt aleatorio de 16 bytes, formato almacenado `"{iteraciones}.{saltBase64}.{hashBase64}"`. Comparación en tiempo constante (`CryptographicOperations.FixedTimeEquals`). `EsHash(valor)` distingue un hash de una contraseña vieja en texto plano.
- **`AppLoader.ValidarLogin(cuenta, contrasena)`** (nuevo): consulta puntual y parametrizada (`SELECT id, llave FROM usuarios WHERE cuenta = @cuenta AND estadof = 'normal'`) — ya **no** se compara en el cliente contra el volcado completo de la tabla `usuarios` (que traía las contraseñas de TODAS las cuentas a memoria antes de que el usuario se autentique). Si la contraseña almacenada es un hash, verifica con `PasswordHasher.Verificar`; si es texto plano y coincide, **migra automáticamente** (la re-hashea con un `UPDATE` directo) y deja loguear.
- **`AppLoader.ConectarUsuarios()`**: ya no se llama antes de loguear. Ahora se llama **después** de `ValidarLogin` exitoso (su doc-comment se actualizó para reflejar esto). Antes de loguear, `LoginWindow.ConectarBaseDatosAsync()` solo hace una sonda de conectividad (`DatabaseConnection.ConexionEstaActiva()`, un `SELECT 1`) en vez de cargar `usuarios`/`empresas` completas.
- **`LoginWindow.BtnIngresar_Click`**: el loop que recorría `Sql.UsuariosObj` comparando `cuenta`/`llave` en texto plano se reemplazó por `AppLoader.ValidarLogin(...)`; si autentica, recién ahí se llama `AppLoader.ConectarUsuarios()` para poblar la caché que usan las pantallas de administración de usuarios/empresas.
- **`CambiarContrasena.BtnGuardar_Click`**: la verificación de la contraseña actual usa `PasswordHasher.Verificar` (con fallback a comparación en texto plano para cuentas aún no migradas); la nueva contraseña se guarda siempre hasheada (`PasswordHasher.Hashear`).
- **Pendiente de la auditoría (NO implementado esta sesión)**: `TrustServerCertificate=True` en `DatabaseConnection.cs` (riesgo MITM), protección anti fuerza bruta en el login, firma de releases de Velopack, y mover los privilegios del usuario de SQL Server de la app a un rol mínimo (solo `SELECT/INSERT/UPDATE`, sin `DELETE` — la app nunca hace `DELETE FROM`, ver borrado lógico) y restringir el acceso remoto por IP/VPN en el panel del hosting (el usuario usa SmarterASP.NET, hosting compartido — no hay firewall de servidor propio, hay que buscar la opción de "Remote MSSQL Access"/IPs permitidas en su panel).

#### Notas / pendientes de esta sesión
- **Sin build en el entorno cloud**: ninguno de estos cambios se compiló (no hay SDK de .NET en este entorno). Antes del próximo release, verificar en una máquina local: compilación, login con una cuenta existente (su contraseña en texto plano debe migrarse sola al hash en el primer login exitoso) y el flujo de "Cambiar Contraseña".

### Sesión 2026-06-18 — Selección/edición de grids, filtros multi-empresa en cascada, login con carga diferida y reubicación de Categorías (rama `claude/optimistic-dirac-5arp05`)

> Contexto: el usuario **eliminó las columnas `empresa` de `articulos`, `familias` y `precios`** en SQL Server; el filtrado de esas tablas por empresa pasa a ser por **cascada de relaciones** (no por columna propia). Además, varios ajustes de UX en grids editables, login y cierre de la consola.

#### ArticulosGeneral — grid de artículos
- **Color de selección propio**: nuevo brush `ThemeFilaSelGrid` (claro `#D7E5FF`, oscuro `#33426B`) en ambos temas; `Grid1.RowStyle` lo aplica con `Trigger IsSelected=True` (antes dependía del resaltado del sistema, que cambiaba de tono al perder el foco). El verde `ThemeFilaSeleccionada` (checkbox marcado en modo importar) gana sobre el azul.
- **Alto de fila fijo + sin negrita**: `Setter Height="34"` en el estilo de fila y se **quitó** `FontWeight=SemiBold` del trigger de `Seleccionado`. Ese cambio de FontWeight forzaba a WPF a re-medir la fila y, con la virtualización, cambiaba el alto de las filas al seleccionar/marcar (visible en modo importar: scroll + seleccionar + doble clic, más notorio en modo oscuro).
- **Nueva columna "Categoría"** entre Código y Descripción Completa (`ArticuloFila.Categoria` ← `articulos.Categoria` → `Categorias.descripcion`). El buscador (`TxtBuscar`) ahora también coincide y **filtra por la categoría**.
- **Actualizar incremental** (`BtnActualizar_Click`): recarga la caché y refresca **sin reconstruir todo**, preservando selección/foco/scroll y la expansión del árbol. `Tree1` se reconcilia por `Tag` (alta/baja/edición de nodos); `Grid1` hace diff por `Id` (actualiza en sitio, agrega, quita) reusando instancias. Helpers nuevos: `ConstruirListaArticulos`, `ConstruirArbolDeseado`/`CrearNodo`/`ReconciliarNodos`/`RestaurarSeleccionArbol`/`BuscarNodoPorTag`, `RefrescarGridIncremental`/`RestaurarSeleccionGrid`. Flag `_suspenderEventosArbol` para que la reconciliación del árbol no dispare recargas.
- **Se eliminó el nodo "Sin Clasificar"** del árbol y su filtro (todo artículo debe tener familia).

#### Edición de celdas — seleccionar todo + solo números
- **`GridFocusHelper.SeleccionarTodoEnEdicion(FrameworkElement?)`**: al entrar a editar, selecciona todo el texto; busca el `TextBox` dentro del `EditingElement` (sirve para `DataGridTextColumn` **y** `DataGridTemplateColumn`) y re-despacha `SelectAll` en `DispatcherPriority.Input` para que el clic del ratón no deshaga la selección. Aplicado en los `PreparingCellForEdit` de Inventarios, Correcciones, Traspasos y Pedidos (líneas + entregas).
- **Columna Cantidad unificada**: en `InventariosDetalle` y `CorreccionesDetalle` pasó de `DataGridTemplateColumn` a `DataGridTextColumn` con `CeldaNumero`/`CeldaNumeroEditar` (mismo aspecto que Pedidos/Traspasos).
- **`FuncionesComunes.RestringirACantidad(TextBox)`**: bloquea letras/símbolos al escribir **y al pegar** (`DataObject` pasting), permitiendo solo dígitos y separadores `,`/`.` (sin depender de la cultura). Aplicado a Cantidad en los 4 formularios y, además, a **Precio, Importe y Contable** en `PedidosDetalle` (GridItems).

#### Reubicación de Categorías a la derecha del grid
- En `InventariosDetalle`, `CorreccionesDetalle` y `TraspasosDetalle`: el panel **Categorías** se movió a una columna (220px) a la derecha de `GridItems` (se quitó de la barra de totales inferior, que conserva Total unidades / Diferentes).
- En `PedidosDetalle`: **Categorías** a la derecha de `GridItems` (pestaña Artículos, `GridCategorias`) y de `GridEntregas` (pestaña Entregas, `GridCategoriasE`); se quitaron ambas tarjetas de la barra de totales. La lógica `CargarTotalesCategoria*` ya existía; solo se reubicó el XAML.

#### ArticulosDetalle / FamiliasDetalle — validaciones y estado
- **Artículo: familia obligatoria y existente** — `ArticulosDetalle.Guardar()` no guarda si `ResolverFamiliaId()` es vacío (campo vacío o código inexistente). Cubre nuevo/insertar/editar.
- **Familia: producto obligatorio y existente** — `FamiliasDetalle.Guardar()` no guarda si `ResolverProductoId()` es vacío.
- **Estado por defecto** — `ArticulosDetalle.GuardarInsertar` ahora fija `estado="mostrar"` (igual que `GuardarNuevo`).
- **ID interno oculto** — el badge "ID" de `ArticulosDetalle` queda `Visibility="Collapsed"`; `Box_Identificador` se conserva (oculto) porque el code-behind guarda el GUID ahí.

#### Multi-empresa — filtros de carga de caché (sin columna `empresa` en articulos/familias/precios)
- `AppLoader.ConectarProductos`: productos, industrias, terceros, regiones, sucursales y categorías filtran **directo** por `empresa = empresa activa`. **familias** filtran por `producto IN (productos de la empresa)`, **artículos** por `familia IN (familias de la empresa)` (cascada de 2 niveles), y **precios** por `region IN (regiones de la empresa)` — sin tocar la columna `empresa` de esas tablas hijas.
- **empresa por defecto al crear** (`GuardarNuevo`): Productos, Industrias, Terceros, Regiones, Sucursales y Categorías establecen `empresa = AppState.EmpresaActiva`.
- **`AppsheetsSync`**: dejó de usar `articulos.empresa`; filtra los artículos de la empresa por cascada `familia → producto → empresa` (en INSERT y en UPDATE de marcado eliminado).

#### Login (LoginWindow) — carga diferida, robustez y UX
- **`AppLoader.ConectarUsuarios()`** (nuevo): antes de iniciar sesión solo carga `usuarios` (+`empresas`), lo mínimo para validar el login. El resto de catálogos se carga **tras loguear** con `ConectarProductos` (ya filtrado por empresa). `AppState.RegionActiva` se calcula **después** de `ConectarProductos` (cuando `sucursales` ya está en caché).
- **Bloqueo total de controles**: `HabilitarControles(bool)` habilita/deshabilita juntos usuario, contraseña (`PasswordBox`), caja de texto visible y **botón del ojo** (antes el ojo seguía activo y reactivaba el campo al alternar).
- **Caída de conexión post-login**: la carga de caché va en `try/catch`; si se cae la red, avisa en el label, cancela el inicio de sesión (limpia `SesionActiva`/`UsuarioActivo`) y **desbloquea todo** para reintentar, en vez de cerrarse.
- **Cursor del ojo**: al alternar mostrar/ocultar, el cursor del `PasswordBox` se reposiciona en el índice que tenía la caja visible (vía el método interno `PasswordBox.Select` por reflexión); antes saltaba al inicio.

#### ConsolaMovimientos — cierre con cambios sin guardar
- Al cerrar la ventana (o al cerrar sesión), si hay **cambios sin guardar** en alguna pestaña de detalle (nuevo/editar), pide confirmación **Sí/No listando exactamente los títulos** de las pestañas afectadas. Detecta cambios por reflexión: propiedad `HayCambios` (PedidosDetalle) o campo `_hayCambios` (resto de detalles); los paneles General/selectores cuentan como "sin cambios". Si no hay cambios, cierra sin preguntar. Flag `_cierreConfirmado` para no preguntar dos veces en el flujo de logout. Helpers: `PestañasConCambios`, `TituloPestaña`, `TieneCambiosSinGuardar`, `ConfirmarPerderCambios`.

#### Notas / pendientes de esta sesión
- **Sin build en el entorno cloud**: todos los cambios siguen patrones existentes pero **no se compilaron**; verificar localmente (especialmente: alto de fila fijo en modo importar/oscuro, select-all en columnas Cantidad de plantilla, restricción numérica, y el aviso de cierre con cambios).
- **Orden de despliegue para borrar columnas**: como `ConectarProductos` usa `SELECT *`, conviene desplegar primero esta versión y **luego** borrar en SQL las columnas `empresa` de `articulos`/`familias`/`precios` (una versión vieja corriendo sí fallaría si se borran antes).

### Sesión 2026-06-15 — Resiliencia ante red inestable (Bolivia), guards de conexión y rediseño/escalado del Dashboard (rama `claude/eloquent-hopper-gi9nlv`)

> Contexto: la conexión a SQL Server en Bolivia es inestable. El objetivo de la sesión fue evitar congelamientos, guardados corruptos y registros "fantasma" cuando la red se cae, más mejoras de UI en el Dashboard. **No se introdujo SQLite ni caché en disco** (se evaluó y descartó por riesgo de duplicar correlativos entre usuarios y romper la generación de códigos, que debe seguir siendo en vivo contra SQL Server).

#### Resiliencia de escritura (`DataConsulta.ExportarItems` + `DatabaseConnection`)
- **Transacción atómica por tabla**: `ExportarItems` envuelve sus INSERT + UPDATE en una sola `SqlTransaction` (todo-o-nada). En rollback restaura el `estadof` original en memoria para conservar los cambios pendientes y poder reintentar. ⚠️ La atomicidad es **por tabla**, NO por documento completo (un pedido se guarda en 4 tablas vía `OrdenarTablas` → `DocumentosPObj`, `PedidosObj`, `TrasaccionesObj`, `EntregasObj`, cada una con su propia transacción). La atomicidad multi-tabla quedó **pendiente** (limitación conocida).
- **Inserts idempotentes**: el INSERT pasó de `VALUES (...)` a `INSERT ... SELECT ... FROM (VALUES …) AS v(cols) WHERE NOT EXISTS (SELECT 1 FROM tabla WHERE id = v.id)`, para que un reintento tras un ACK perdido no duplique filas por `id`. Los UPDATE ya son idempotentes.
- **Descarte de inserts no persistidos (anti-fantasma)**: si `ExportarItems` falla definitivamente (tras reintentos), `DescartarInsertsNoPersistidos()` quita de la caché en memoria las filas en estado `"nuevo"` que no llegaron a SQL, para no dejar registros que se ven en la app pero no existen en la base. (Las ediciones que fallan quedan pendientes, no se revierten — limitación conocida.)
- **`SqlRetry`** (nueva clase, `WpfAppVba.Data`): `SqlRetry.Ejecutar(Func/Action, intentos=3, baseMs=400)` reintenta con backoff exponencial **solo** ante errores SQL transitorios (números `-2, 53, 64, 121, 233, 1205, 10053/4/60/1, 11001, 40197, 40501, 40613`). Es **síncrono** (`Thread.Sleep`), así que un reintento añade una pausa breve en el hilo de UI. Envuelve: `ObtenerDatos` (lecturas), `ExportarItems` (la transacción completa) y los helpers en vivo (`CodigoExiste`, `SiguienteCodigoInt`, `SiguienteNumeroDoc`, `SiguienteNumeroDocPorEmpresa`, `MaxFecha`, `Maximo`, `VerificarId`, `IndicesNoNormales`).
- **`DatabaseConnection.ConnectionString`**: se agregó `Connect Retry Count=3;Connect Retry Interval=10;Pooling=true;` (resiliencia de conexiones idle ante microcortes).
- **`DatabaseConnection.Sondear(int timeoutSeg=2)`** (nuevo): sonda rápida con una conexión **propia y desechable** (timeout corto), que NO toca la conexión compartida; la usa el timer de estado de conexión.

#### Estado de conexión (label en top bar) + guards de guardado/actualización
- **`ConexionEstado`** (nueva clase estática, `WpfAppVba`): solo mantiene estado, sin UI. `EnLinea` (bool, optimista en `true`), evento `Cambio`, `Intervalo` (15 s). `Iniciar(Dispatcher)` arranca un `DispatcherTimer` que sondea en segundo plano (`DatabaseConnection.Sondear(2)` vía `Task.Run`) y marshaliza el cambio al hilo de UI. `Revisar()` fuerza una sonda inmediata.
- **Label en la top bar** de `ConsolaMovimientos`: pill `PillConexion` + `LblConexion` ("●  En línea" verde `#2E7D32` / "●  Sin conexión" rojo `#C62828`), en la columna central. Se suscribe a `ConexionEstado.Cambio` en el ctor (`ConexionEstado.Iniciar(Dispatcher)`) y se da de baja en `ConsolaMovimientos_Closing`.
- **`FuncionesComunes`**: `HayConexionOAvisa(owner, mensaje)` (capa 1: lee `ConexionEstado.EnLinea` instantáneo; capa 2 con cortocircuito `&&`: `TieneConexion()` real / `SELECT 1`); `VerificarConexionParaGuardar(owner)` ("Sin conexión. No se pueden guardar los cambios.") y `VerificarConexionParaActualizar(owner)` ("…no se pueden actualizar los datos.").
- **Guard al GUARDAR** (`if (!FuncionesComunes.VerificarConexionParaGuardar(Window.GetWindow(this))) return [false];`) al inicio de `Guardar()` de los 14 `*Detalle` (Articulos, Categorias, Correcciones, Empresas, Familias, Industrias, Inventarios, Pedidos, Precios, Productos, Regiones, Sucursales, Terceros, Usuarios), `Configuracion.BtnGuardar_Click`/`BtnGuardarTema_Click` y `CambiarContrasena.BtnGuardar_Click`. (Se convirtieron a cuerpo de bloque los `Guardar()` que eran expresión.)
- **Guard al ELIMINAR/cambiar estado**: en los `BtnEliminar_Click` de los 15 paneles `*General` y en `PreciosGeneral.BtnCambiarEstado_Click`. **NO** se guarda `ConsolaMovimientos.MarcarInactivo()` (presencia best-effort en logout, con su propio try/catch).
- **Guard al ACTUALIZAR**: en los 16 `BtnActualizar_Click` (paneles General + Dashboard), con el mensaje de actualizar.

#### Offline: arreglo del congelamiento de Configuración
- `Configuracion.PoblarSucursales` / `ActualizarPeriodos` / `ActualizarFechaInicio`: si `ConexionEstado.EnLinea == false` usan el **caché en memoria** (`Sql.SucursalesObj` filtrado por empresa; `MaxFecha` solo se intenta online) en vez de consultar SQL Server. Antes, abrir la pestaña Configuración sin conexión consultaba SQL en el hilo de UI → con `SqlRetry` reintentaba y reventaba con `SqlException` (congelamiento ~10 s). Ahora abre al instante offline.

#### AppSheets — solo "sincronizar todo"
- Se **eliminó** `SincronizarAppsheetsDialog.xaml/.cs` y el método `AppsheetsSync.SincronizarSucursalActiva()` (y la lógica `suc != null` de los helpers `InsertarFaltantes`/`MarcarEliminados`, que ahora siempre cubren todas las sucursales de la empresa vía `CROSS JOIN`).
- `Configuracion.BtnSincronizarAppsheets_Click` ahora llama **directamente** a `AppsheetsSync.SincronizarTodasLasSucursales()` (sin diálogo). `ArticulosGeneral` ya usaba esa función.

#### Dashboard — rediseño de gráficos + escalado tipo zoom
- **Reorganización a grilla 2 columnas × 3 filas** (`DashboardGeneral.xaml`): Col 1 = "Cantidad por mes y categoría" (filas 1-2, `RowSpan`) + "Suma por artículo" (fila 3); Col 2 = "Total por categoría" (fila 1) + "Total por familia" (filas 2-3, `RowSpan`).
- **Escalado global tipo zoom**: TODO el contenido (segmentadores + KPIs + 3 gráficos) está envuelto en un **`Viewbox` (`Stretch="Uniform"`)** sobre un tamaño de diseño fijo `<Grid Width="1600" Height="860">`. Al minimizar/maximizar el conjunto se escala proporcionalmente; nada se recorta. (`Uniform` = sin deformar, puede dejar franja; alternativa `Fill` = llena sin franja pero deforma. Se evaluó un slider de zoom manual con `ScaleTransform`+`ScrollViewer` pero el usuario lo **revirtió**; quedó solo el escalado automático del Viewbox.)
- **Scrolls internos selectivos** (respaldo si los datos exceden el diseño): "por mes" tiene **scroll horizontal** (su `PanelMeses` pasó de `UniformGrid` a `StackPanel Orientation="Horizontal"` para poder desbordar; el eje Y queda fijo fuera del scroll); "por categoría" y "por familia" tienen **scroll vertical**; la tabla "por artículo" conserva el scroll interno del `DataGrid`.

#### Limitaciones conocidas (pendientes, NO implementadas)
- **Atomicidad de documentos multi-tabla**: cabecera + líneas de un pedido/traspaso se guardan en transacciones separadas; un corte entre tablas puede dejar un documento incompleto en SQL. Requiere una transacción ambiental compartida en `DatabaseConnection` (cambio mayor en el acceso a datos).
- **Revertir ediciones al fallar**: el anti-fantasma solo descarta INSERTs nuevos; una edición que falla deja la caché mostrando el valor nuevo aunque SQL tenga el viejo (hasta el próximo guardado/Actualizar). Requiere snapshot del valor previo.
- **`SqlRetry` es síncrono**: los reintentos pausan brevemente el hilo de UI. Eliminarlo requeriría pasar el acceso a datos a async.

### Sesión 2026-06-15 — Dashboard bars, sección Usuarios y ícono global (rama `claude/confident-curie-t2etpj`)

#### Dashboard — barras de familia proporcionales al ancho
- **`DashboardGeneral.xaml`**: `PanelFamilias` ScrollViewer cambiado a `HorizontalScrollBarVisibility="Disabled"` para que las columnas star tengan ancho finito.
- **`DashboardGeneral.xaml.cs`** `RenderFamilias()`: las barras ahora usan un `innerGrid` con 2 columnas star (`total` | `maxTotal - total`); la primera columna contiene un `segsGrid` con los segmentos por categoría (star proporcional a su valor). El `track` Border usa `HorizontalAlignment=Stretch` para ocupar todo el ancho del card. El total numérico queda a la derecha pegado a la barra (`barRow` con col 1 = Auto).

#### Nueva sección Usuarios (solo admin)
- **`UsuariosGeneral.xaml/.cs`** (nuevo UserControl):
  - Lista con DataGrid: Línea / Código / Cuenta / Nombres / Apellidos / Tipo / Estado.
  - Buscador por cuenta/nombres/apellidos; botones Nuevo/Editar/Eliminar/Actualizar.
  - Guarda contra eliminar al usuario activo (`fila.Id == AppState.UsuarioActivo`).
  - `UsuarioFila`: Linea, Id, Codigo, Cuenta, Nombres, Apellidos, Tipo, EstadoU.
- **`UsuariosDetalle.xaml/.cs`** (nuevo UserControl):
  - Cards: IDENTIFICACIÓN (Código read-only + Cuenta), DATOS PERSONALES (Nombres + Apellidos), ACCESO (CmbTipo [admin/user] + TxtEstadoU read-only TextBlock), EMPRESA Y SUCURSAL (CmbEmpresa + CmbSucursal dependiente).
  - Sin campo contraseña ni tema (eliminados del formulario).
  - `TxtEstadoU` es un `TextBlock` dentro de `Border` con `ThemeBgReadOnly` — no editable.
  - `EmpresaItem` / `SucursalItem` como clases internas privadas.
  - `PoblarSucursales(empresaId)` hace consulta directa filtrada por empresa.
  - `GuardarNuevo`: setea `estadoU = "inactivo"`, `estadof = "normal"`.
  - `GuardarEditar`: NO modifica `estadoU` (gestionado automáticamente).
- **`ConsolaMovimientos.xaml`**: botón `👤 Usuarios` (`BtnNav_Usuarios`) con `Visibility="Collapsed"` antes de Configuración; `MinWidth="960"` `MinHeight="620"`; `Closing="ConsolaMovimientos_Closing"`.
- **`ConsolaMovimientos.xaml.cs`**:
  - En constructor: `if (AppState.EsAdmin) BtnNav_Usuarios.Visibility = Visibility.Visible;`
  - `_panelUsuarios` (mutable, recreado en `RecargarContexto()`); sección `"usuarios"` en ambos diccionarios.
  - `MarcarInactivo()`: guarda `estadoU = "inactivo"` para `AppState.UsuarioActivo`; con guard `if (string.IsNullOrEmpty(...)) return;` para evitar doble ejecución.
  - `ConsolaMovimientos_Closing`: llama `MarcarInactivo()`.
  - `BtnCerrarSesion_Click`: llama `MarcarInactivo()` antes de limpiar `AppState.UsuarioActivo` (doble llamada en Closing es segura por el guard).
- **`LoginWindow.xaml.cs`**: al autenticar, setea `estadoU = "activo"` y llama `ExportarItems()` antes de abrir `ConsolaMovimientos`.

#### Ícono de aplicación en todas las ventanas
- **`WpfAppVba.csproj`**: `<ApplicationIcon>icono.ico</ApplicationIcon>` + `<Resource Include="icono.ico"/>`.
- **`ConsolaMovimientos.xaml`**: `Icon="icono.ico"` en el elemento `Window`.
- **`App.xaml.cs`**: en el constructor, se crea `BitmapFrame.Create(new Uri("pack://application:,,,/icono.ico", UriKind.Absolute))` y se asigna a cada ventana vía el `EventManager.RegisterClassHandler` existente de `Window.Loaded` (mismo handler que aplica el modo oscuro). Esto garantiza que el ícono aparece en la barra de tareas y título de TODAS las ventanas (LoginWindow, ConsolaMovimientos, diálogos).
  - Nota: la aproximación anterior con `<Style TargetType="Window">` en `App.xaml` no funciona porque WPF no aplica estilos implícitos de Application.Resources a subclases de Window.

#### Distribución / Instalador
- Publicación self-contained con archivo único:
  ```
  dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=none
  ```
  Resultado: 7 archivos en `bin\Release\net8.0-windows\win-x64\publish\` (1 exe ~162 MB + 6 DLLs nativas de WPF inevitables).
- Instalador creado con **Inno Setup 6.7.3** (asistente Script Wizard): idiomas Inglés + Español; modo administrativo; acceso directo en escritorio y menú Inicio; output `Setup_SistemaGestion.exe`. Script `.iss` guardado para futuras actualizaciones (recompilar con F9 tras nuevo publish).

---

### Sesión 2026-06-14 — AppSheets (sincronización), orden por `secuencia` y panel Dashboard (rama `claude/affectionate-albattani-r2uilg`)

#### Tabla `appsheets` + sincronización desde Configuración
- La tabla `appsheets` la creó el usuario en SQL Server. Estructura final: `secuencia` INT IDENTITY, `estadof` NVARCHAR(255), `emision` DATETIME, `id` UNIQUEIDENTIFIER DEFAULT NEWID(), `sucursal`, `articulo`, `usuario`, `empresa` (todas UNIQUEIDENTIFIER). **La columna `indice` se eliminó** (no tenía función).
- Nueva clase **`AppsheetsSync`** (estilo `CodigoRegenerator`, SQL directo en transacción). Métodos:
  - `SincronizarSucursalActiva()` — solo la sucursal activa.
  - `SincronizarTodasLasSucursales()` — todas las sucursales `estadof='normal'` de la empresa activa (INSERT vía `CROSS JOIN sucursales`).
  - Reglas por sucursal: **INSERT** una fila por cada artículo activo (`estadof='normal'`, empresa activa) que aún no tenga fila `'normal'` en `appsheets` (rellena `articulo`, `sucursal`, `empresa`, `usuario`, `emision=GETDATE()`, `estadof='normal'`); **UPDATE `estadof='eliminado'`** las filas `'normal'` cuyo artículo ya no esté activo.
- **Configuración**: botón **`🧾 Sincronizar AppSheets`** a la izquierda de "Cambiar Contraseña". Abre `SincronizarAppsheetsDialog` (Window) con 3 opciones: *Sincronizar todo* (todas las sucursales) / *Sincronizar sucursal activa* / *Cancelar*.
- **Disparo automático**: al **agregar** (`ArticulosDetalle.GuardarNuevo`/`GuardarInsertar`) o **eliminar** (`ArticulosGeneral.BtnEliminar_Click`) un artículo se ejecuta `SincronizarTodasLasSucursales()` vía el helper estático `ArticulosGeneral.SincronizarAppsheetsTrasCambio()` (si falla solo avisa, no revierte el cambio del artículo). Editar un artículo NO dispara sync.

#### Orden de recarga de caché por `secuencia`
- `AppLoader.ConectarProductos`: las consultas que ordenaban `ORDER BY id ASC` ahora usan `ORDER BY secuencia ASC` (empresas, usuarios, familias, productos, Categorias, industrias, terceros, sucursales, regiones). `articulos` sigue por `familia, indice`; `precios` por `fecha`.

#### Nuevo panel **Dashboard** (sidebar)
- Nuevo `DashboardGeneral` (UserControl), sección `"dashboard"`, botón `📊 Dashboard` al tope del sidebar de `ConsolaMovimientos` (registrado en diccionarios, `MostrarPanel`, `RecargarContexto`, `BtnNav_Dashboard_Click`, `_panelDashboard`).
- Lee del caché ya filtrado (`PedidosObj`/`TraspasosObj`) por sucursal + período activos. Venta/compra por `documentosP.movimiento`; entrada/salida por `origen`/`destino` vs sucursal activa en `documentosT`; categoría/familia/descripción/código vía `articulos` → `CategoriasObj`/`FamiliasObj`. Todo en **cantidad** (unidades). Botón **Actualizar**.
- **Segmentador de selección única** (RadioButton, GroupName): Ventas/Compras/Entradas/Salidas; cada chip muestra su cantidad y filtra el resto; por defecto Ventas.
- **KPIs** (a la derecha de los segmentadores, misma fila): Total unidades (tipo activo), Movimientos, Artículos distintos.
- **Gráfico por mes**: barras **agrupadas por categoría** (una barra por categoría con valor encima), solo meses con datos, eje Y con escala (redondeo "lindo" `NiceCeil`) + cuadrícula horizontal; el total del mes va debajo del nombre del mes. Leyenda de colores por categoría. **Altura dinámica** según el alto disponible (`PanelPlot_SizeChanged`, cache `_porMesCache`).
- **Por categoría**: barras horizontales que llenan el ancho (proporción star), eje X + cuadrícula, color propio por categoría (paleta estable `_colorCat`).
- **Por familia**: una barra **apilada por categoría** por familia, con los totales por categoría entre el título y la barra; longitud proporcional al total de la familia (ancho fijo `AnchoBarra`).
- **Lista de artículos**: DataGrid Código · Familia · Descripción · Cantidad · % + fila Total.
- **Layout sin scroll general**: el contenido es un `Grid` con filas proporcionales (`Auto` / `1*` / `1.4*`); el gráfico y las tablas se estiran por resolución y solo las listas internas tienen scroll. Disposición inferior: col 1 = categoría (arriba) + artículos (abajo); col 2 = familia (ambas filas).
- Gráficos dibujados con primitivas WPF (sin dependencias NuGet nuevas).

### Sesión 2026-06-12/13 — Multi-empresa, borrado lógico, lógica de índices, períodos y limpieza (rama `claude/cool-hopper-vo3mxo`)

#### Multi-empresa: formularios y empresa activa
- Nueva sección **Empresas** en el sidebar, entre Precios y Configuración (botón `🏢 Empresas`).
- `EmpresasGeneral` (UserControl): lista Línea/Código/Descripción/Signo; CRUD "Nueva/Editar/Eliminar Empresa" + Actualizar; patrón estándar (`_iniciado`, `Cerrando`/`IntentarCerrar`, actualización incremental, `OpenAsTab` selector clave `seleccionar-empresa|{contexto}`). Usa `Sql.EmpresasObj`.
- `EmpresasDetalle` (UserControl): Código (int, `SiguienteCodigoInt()`, read-only al editar), Descripción, Signo (NVARCHAR(4) mayúscula), Observación (TextBox plano en `Border`, texto al tope como `TercerosDetalle`). Valida `CodigoExiste()`. Persiste `codigo, descripcion, signo, observacion, fecha, emision, edicion, usuario, usuarioE`. La columna `codigo` (INT) de `empresas` la agregó el usuario en SQL.
- `ConsolaMovimientos`: panel `_panelEmpresas` (+ sección `"empresas"` en los diccionarios, caso en `MostrarPanel`, `BtnNav_Empresas_Click`). Los paneles General pasaron de `readonly` a mutables.
- **Configuración**: nuevo `CmbEmpresa` ("EMPRESA ACTIVA") a la izquierda de `CmbSucursal`. `CmbSucursal` es **dependiente** de la empresa (se repuebla por consulta directa `_sucursalesEmpresa`, ya que el caché está filtrado por la empresa activa). `ActualizarFechaInicio`/`ActualizarPeriodos` leen de `_sucursalesEmpresa`.
- **Guardar en Configuración ya NO cierra sesión**: si cambia empresa/sucursal/periodo recarga cachés (`ConectarProductos` si cambió empresa, `ConectarBases`, `ActualizarBase`, `ConectarDocumentos`) y llama `ConsolaMovimientos.RecargarContexto()` (cierra las pestañas dinámicas y **recrea los paneles General**), manteniendo el foco en Configuración; luego `CargarDatos()` repuebla sus combos. La empresa se persiste en `usuarios.empresa`. Se eliminó `CerrarSesionYReabrirLogin`.
- **No se puede guardar sin sucursal**: si `CmbSucursal` está vacío (empresa sin sucursales) → `MessageBox` de advertencia y `return` (reemplaza un comportamiento intermedio que dejaba `usuarios.sucursal` en NULL).
- **TOP BAR**: nuevo `LblEmpresa` ("Empresa: {desc}") a la derecha de `LblSucursal`, seteado en `ActualizarInfoUsuario()`.

#### Borrado lógico global (sin DELETE físico)
- `DataConsulta.ExportarItems` ya NO ejecuta `DELETE FROM`. Las filas con `estadof` = `"eliminado"`/`"ocultado"` se persisten con `UPDATE` del `estadof` (se filtran al recargar, que solo trae `estadof='normal'`). Se quitó el bloque DELETE y `deleteIds`. Afecta a todos los `.Eliminar()`/`.Ocultar()`. Nota: las tablas acumulan filas con estadof≠normal (no se borran nunca).

#### Lógica de índices: no reutilizar índices de filas eliminadas
- **Guardado diferencial de líneas** de documentos (antes borraban TODAS las líneas y las recreaban): nueva (id de fila vacío) → insertar; existente → `EstablecerItem` sobre su mismo id (UPDATE); quitada (estaba al abrir y ya no está) → `Eliminar()` (estadof "eliminado"). Cada detalle captura los ids originales (`_xxxOrig`) al abrir para editar y los limpia en modo nuevo. Aplicado en **PedidosDetalle** (pedidos/transacciones/entregas), **TraspasosDetalle**, **CorreccionesDetalle**, **InventariosDetalle**. Se quitaron `EliminarLineas`/loops `idsEliminar`.
- **Índice por posición sin reutilizar eliminados**: cada línea visible se numera por su posición en la grilla, **saltando los índices reservados** por filas eliminadas (que conservan su índice). Reservados = filas `estadof <> 'normal'` en SQL (`DataConsulta.IndicesNoNormales(filtroColumna, filtroValor)`) + las que se eliminan en este guardado. Ej.: A(índice 1, eliminado) + B(índice 2, normal), insertar C sobre B → A=1 (eliminado), C=2, B=3. (Reemplaza un intento previo `SiguienteIndice` = MAX+1, ya retirado.)
- **`articulos`** (su `indice` es la posición dentro de la familia): Eliminar (`ArticulosGeneral`) ya NO corre los índices, solo oculta (índice reservado, puede quedar hueco). Insertar (`ArticulosDetalle.GuardarInsertar`) usa el nuevo helper estático `ArticulosGeneral.RenumerarFamilia(famId)` (renumera activos por orden saltando reservados). El índice sugerido (`RecalcularIndicePorFamilia`) considera también los reservados.
- Tablas con columna `indice` cubiertas por la regla: pedidos, transacciones, entregas, traspasos, correcciones, inventarios y articulos. (Modo editar de un artículo: mantiene su índice; reposicionar manualmente ahí aún no aplica el salto-de-reservados — pendiente menor.)

#### Apertura / períodos
- **Períodos desde la máxima fecha de inventario**: `Configuracion.ActualizarPeriodos` calcula el año inicial de `CmbPeriodo` como el año de la MÁXIMA fecha de inventario de la sucursal (`DocumentosIObj.MaxFecha("sucursal", id)`, consulta directa a SQL); si la sucursal no tiene inventarios usa el año de `sucursal.fecha`. Nuevo helper `DataConsulta.MaxFecha`. Evita elegir un período anterior al inventario (que recargaba documentos previos a esa fecha vía `ActualizarBase`). Ej.: sucursal con `fecha`=01/01/2024 e inventario 19/02/2026 → el desplegable solo muestra 2026.
- **Fix `uniqueidentifier`**: `AppLoader.ConectarBases`/`ConectarDocumentos` usan el GUID nulo `00000000-0000-0000-0000-000000000000` cuando `SucursalActiva` está vacía (antes `sucursal = ''` contra una columna `uniqueidentifier` lanzaba *"Conversion failed when converting from a character string to uniqueidentifier"* al guardar sin sucursal y también en el login siguiente).

#### Eliminación de la tabla `stocks`
- El usuario eliminó la tabla `stocks` en SQL Server. Se quitó `SqlData.StocksObj`, su `Conectar("stocks", ...)` en `AppLoader.ConectarProductos`, y `AppState.ActualizarStocks()` + sus 3 llamadas. **No** se tocó el cálculo de stock (`StockCalculator.ContarStock/ContarStock2`, `GridStock`, columnas "Stock", avisos de stock insuficiente), que usa apertura + documentos y no dependía de la tabla.

#### Otros (UI y limpieza)
- **MovimientosGeneral**: la columna "Movimiento" muestra `código-tipo` del documento (de `Documentos[P/T/C]Obj.ObtenerItem("codigo", id)`) en vez del `id(UUID)`. Se **eliminó** `MovimientosWindow.xaml/.cs` (Window legacy sin referencias); las clases `MovimientoDato`/`MovimientoFila` se movieron a `MovimientosGeneral.xaml.cs`.
- **Diseño esquinas redondeadas**: `EmpresasGeneral` y `RegionesGeneral` al estilo unificado (botones `ControlTemplate CornerRadius=6`, `SearchInput` redondeado, `DataGrid` en `<Border CornerRadius="6">`).
- **Signo en maestros**: `RegionesDetalle` y `SucursalesDetalle` tienen `Box_Signo` (SIGNO, MaxLength 4, mayúscula) a la derecha de DESCRIPCIÓN; cargan/guardan `regiones.signo`/`sucursales.signo`.

#### Helpers nuevos en `DataConsulta`
- `IndicesNoNormales(filtroColumna, filtroValor)` → índices de filas `estadof <> 'normal'` del documento (para no reutilizarlos).
- `MaxFecha(filtroColumna, filtroValor)` → máxima fecha de filas en estado normal (consulta directa).
- (`SiguienteIndice` se introdujo y luego se retiró; reemplazado por la regla de posición + `IndicesNoNormales`.)

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
git checkout claude/confident-curie-t2etpj
git pull origin claude/confident-curie-t2etpj
git push -u origin claude/confident-curie-t2etpj
```

### Restaurar paquetes NuGet (si es necesario)
```bash
cd WpfAppVba
dotnet restore
```
