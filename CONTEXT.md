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
- **Rama de desarrollo activa**: `sistemaControl_1.5`

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
- **Rama de trabajo**: todos los cambios van a `sistemaControl_1.5`. Los commits se generan primero en una rama de sesión (`claude/...`) y luego se pasan a `sistemaControl_1.5` por fast-forward.

## Historial de Cambios por Sesión

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
- **CONTEXT.md**: actualizado para reflejar que `CorreccionesDetalle` y `ArticulosDetalle` ya estaban migrados a pestañas en sesiones anteriores.

### Sesión actual (2026-06-09) — rama `master`
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
  - `TxtTotalPeq`, `TxtTotalMed`, `TxtTotalGra`, `TxtTotalOtros`: `Visibility="Collapsed"`, presentes para compatibilidad con `CargarTotales()` en code-behind.

**Estilos en `UserControl.Resources`:**
| Clave | Tipo | Descripción |
|-------|------|-------------|
| `BtnBase` | Button | CornerRadius=6, height=28, hover opacity 0.9, pressed 0.75 |
| `BtnPrimario` | Button (BasedOn BtnBase) | Background #3B82F6, Foreground White |
| `BtnSecundario` | Button (BasedOn BtnBase) | ThemeBtnSecBg/Fg, BorderThickness=1 |
| `BtnPeligro` | Button (BasedOn BtnBase) | Background #EF4444 |
| `BtnAmarillo` | Button (BasedOn BtnBase) | Background #F59E0B |
| `ModernInput` | TextBox | ControlTemplate con CornerRadius=6, PART_ContentHost |
| `MetricCard` | Border | CornerRadius=8, Padding=10,8, ThemeBgSurface |
| `SectionHeader` | TextBlock | FontSize=10, SemiBold, ThemeTextoSec |
| `LblMuted` | TextBlock | FontSize=10, ThemeTextoSec |
| `Lbl` | TextBlock | FontSize=11, ThemeTextoSec, Margin 0,0,5,0 |
| `RightCell` | TextBlock | HorizontalAlignment=Right |

**Code-behind destacado:**
- `ActualizarBadges()`: colorea dinámicamente `BadgeEstado`/`BadgeCuenta` con `SolidColorBrush(Color.FromRgb(...))` según valores de `Box_Estado`/`Box_Cuenta`. Actualiza `LblIconoTipo` (V/C) y `LblDocBadge`.
- `CargarTotalesCategoria()`: lee todas las categorías de `Sql.CategoriasObj`; suma cantidades de `_pedidos` por categoría (campo `"categoria"` del artículo); líneas sin `ArticuloId` o sin categoría reconocida → "Otros". Asigna a `GridCategorias.ItemsSource`.
- `ActualizarTotales()` llama: `CargarTotales()`, `CargarTotalesDivisas()`, `CargarEstadosCuenta()`, `CargarTotalesCategoria()`, y `CargarEstados()` si `tipoPedido=="normal"`.
- `CategoriaCantFila`: clase de fila para `GridCategorias` (renombrada desde `CategoriaFila` para evitar conflicto CS0101 con `CategoriasGeneral.xaml.cs`).

**Correcciones realizadas en esta sesión:**
- CS0101: `CategoriaFila` duplicada con `CategoriasGeneral` → renombrada a `CategoriaCantFila` en PedidosDetalle.
- Alineación de la barra de totales: pasó de un solo `StackPanel HorizontalAlignment="Right"` a un `Grid` con grupo izquierdo (contadores) y grupo derecho (importes).
- Texto de `TxtTotalUnidades` y `TxtUnidadesDiferentes`: `HorizontalAlignment="Right"` dentro de su tarjeta.

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
git checkout sistemaControl_1.5
git pull origin sistemaControl_1.5
git push -u origin sistemaControl_1.5
```

### Restaurar paquetes NuGet (si es necesario)
```bash
cd WpfAppVba
dotnet restore
```
