# Estructura de la base de datos — `edberBase`

> Generado a partir del script SQL Server provisto por el usuario (`script.sql`,
> fecha de script 24/07/2026). Refleja el estado real de la base, no una
> suposición del código — ante cualquier diferencia, este archivo debe
> actualizarse con el próximo `script.sql` que se comparta, junto con
> `SistemaGestion/EsquemaValidator.cs` y `VisorEmpresa/EsquemaValidator.cs`
> (que validan esta misma estructura al conectar un servidor).

## Convenciones generales

- **Motor**: SQL Server (compatibility level 160). Cada tabla es `PRIMARY KEY`
  sobre `id`, la mayoría `NONCLUSTERED` (algunas — `documentosL`, `facturas` —
  son `CLUSTERED`).
- **`id`** — `uniqueidentifier NOT NULL`, en todas las tablas. Default `newid()`
  vía constraint `DF_<tabla>_id` (excepto `usuarios`, que no tiene ese default
  explícito en el script — igual se genera el GUID desde la app).
- **`estadof`** — `nvarchar(100) NULL`, en todas las tablas. Estado lógico
  interno de la fila: `normal` / `nuevo` / `editado` / `ocultado` / `eliminado`.
  La app **nunca hace `DELETE` físico** — todo borrado es una actualización de
  `estadof` (ver `DataConsulta.ExportarItemsInterno`).
- **`codigo`** — presente en la mayoría de las tablas maestras y de documentos.
  Es el número/código visible para el usuario, regenerable por
  `CodigoRegenerator.RegenerarTodos()`:
  - Tablas maestras (`usuarios`, `familias`, `productos`, `categorias`,
    `industrias`, `terceros`, `sucursales`, `regiones`, `empresas`): `int`,
    numeración 1..N ordenada por `descripcion` (excepto `usuarios`, que no
    tiene esa columna y se ordena por `apellidos, nombres`).
  - Tablas de documentos (`documentosI/P/C`): `nvarchar`, signo de la
    sucursal + correlativo por sucursal.
  - `documentosT`: signo de la **empresa** (cascada `emitido` → `sucursales` →
    `empresas`) + correlativo por empresa.
  - `documentosL`: signo de la empresa (columna `empresa` directa) +
    correlativo por empresa.
- **Columnas `uniqueidentifier` que no son `id`** son claves foráneas
  (apuntan al `id` de otra tabla) — no hay `FOREIGN KEY` declaradas en el
  script, la integridad se maneja desde la app.
- **`appsheets`** es la única tabla que **no** pasa por la caché genérica de
  `SqlData`/`DataConsulta` (`ObtenerItem`/`EstablecerItem`/`OrdenarData`) — se
  accede con SQL directo desde `AppsheetsSync.cs`. Por eso no forma parte del
  manifiesto de `EsquemaValidator` (que valida solo las tablas cacheadas).

## Cambios respecto a la versión anterior de este documento (sesión 2026-07-24)

- **`secuencia`**: eliminada de **todas** las tablas (ya no existe en ningún
  `CREATE TABLE` del script). El código ya no depende de ella para nada — el
  orden de las tablas maestras pasó a `descripcion` (`apellidos, nombres` en
  `usuarios`).
- **`documentosP.emitido`**: eliminada. Era idéntica a `documentosP.sucursal`
  (ambas se seteaban con `AppState.SucursalActiva` al crear el pedido) — el
  código ya solo usa `sucursal`.
- **`articulos.estadoV`**: eliminada, consolidada en `articulos.estado`
  ("mostrar"/"ocultar" — filtro de visibilidad en las plantillas Excel/PDF de
  Precios/Inventarios). El código que antes leía/escribía `estadoV` ahora usa
  `estado`.
- **`usuarios.emision` / `usuarios.edicion`**: agregadas (antes `usuarios` no
  las tenía). `VisorEmpresa/UsuariosDetalle.xaml.cs` ya las escribía al crear
  un usuario nuevo — con esto la columna finalmente existe en la base.
- **`pedidos.forma` / `pedidos.contable`**: eliminadas, sin reemplazo — ya no
  se usan en ningún lado.
- **`documentosF` y `transaccionesF` eliminadas por completo.** Facturas dejó
  de ser un documento propio (con cabecera, tercero, fecha, estado, etc.) y
  pasó a ser una línea más colgada directamente de `documentosP`, igual que
  `pedidos`/`transacciones`/`entregas`. `facturas.documentoF` se renombró a
  `facturas.documentoP`. Las pantallas `FacturasGeneral`/`FacturasDetalle`
  (ambos proyectos) se eliminaron; ahora hay una pestaña "Facturas del
  pedido" dentro de `PedidosDetalle`, ubicada a la derecha de "Artículos del
  pedido".
- **`facturas.forma` renombrada a `facturas.estado`** (`nvarchar(100)`),
  valores "con deuda"/"sin deuda". Una factura "con deuda" suma su importe al
  Saldo del pedido; "sin deuda" no. Nuevo botón **"Facturar pedido"** en esa
  pestaña: agrupa las líneas de artículos del pedido por categoría y genera
  una línea de factura por categoría (concepto = descripción de la categoría,
  importe = suma de esa categoría) con estado "sin deuda" — se puede volver a
  presionar para recalcular; no toca las líneas "con deuda" agregadas a mano.
- **`documentosP.estadoA`**: agregada (`nvarchar(100)`, "sin factura"/"con
  factura"). Es un estado más para el pedido, igual que `estado` (entrega) y
  `estadoC` (cuenta): se recalcula solo, sin intervención manual, según si el
  importe total facturado (`Importe facturado`, suma de las líneas de la
  pestaña "Facturas del pedido") es mayor que cero. Se muestra como badge
  "Estado de factura" en el encabezado de `PedidosDetalle`. En
  `PedidosGeneral`, reemplaza a la columna "Referencia" en `Grid1` (badge
  "Factura") y suma un nuevo filtro lateral ("Todos"/"Con factura"/"Sin
  factura").
- **Permiso de eliminar en `documentosP`/`documentosT`/`documentosI`/
  `documentosC`**: además del administrador, ahora también puede
  eliminar/ocultar un documento el usuario que figura en su columna
  `usuario` (el creador). Para que esto sea confiable se corrigió un bug en
  Pedidos e Inventarios: su `GuardarEditar` sobrescribía `usuario` en cada
  edición (perdiendo el creador original) — ahora, igual que ya hacían
  Traspasos y Correcciones, solo se actualiza `usuarioE` al editar y
  `usuario` queda fijo desde que se crea el documento.

## Tablas

### `appsheets`
Sincronizada con `articulos` para la integración externa AppSheets (no forma
parte de la caché `SqlData`, ver `AppsheetsSync.cs`).

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| estadof     | nvarchar(100)       | sí   |
| emision     | datetime            | sí   |
| id          | uniqueidentifier    | NO   |
| sucursal    | uniqueidentifier    | sí   |
| articulo    | uniqueidentifier    | sí   |
| usuario     | uniqueidentifier    | sí   |
| empresa     | uniqueidentifier    | sí   |

### `articulos`
Catálogo de artículos.

| Columna      | Tipo               | Null | Nota |
|--------------|---------------------|------|------|
| descripcion  | nvarchar(255)       | sí   | |
| indice       | int                 | sí   | orden dentro de la familia (auto, `RecalcularIndicePorFamilia`) |
| modelo       | nvarchar(255)       | sí   | |
| observacion  | nvarchar(255)       | sí   | |
| estado       | nvarchar(100)       | sí   | "mostrar"/"ocultar" — visibilidad en plantillas (ex `estadoV`) |
| estadof      | nvarchar(100)       | sí   | |
| emision      | datetime            | sí   | |
| edicion      | datetime            | sí   | |
| codigo       | nvarchar(100)       | sí   | |
| id           | uniqueidentifier    | NO   | |
| categoria    | uniqueidentifier    | sí   | FK → categorias |
| familia      | uniqueidentifier    | sí   | FK → familias |
| industria    | uniqueidentifier    | sí   | FK → industrias |
| usuario      | uniqueidentifier    | sí   | FK → usuarios (creó) |
| usuarioE     | uniqueidentifier    | sí   | FK → usuarios (editó) |

### `categorias`
Catálogo de categorías (nombre de tabla en SQL es `categorias`, minúscula; el
manifiesto de `EsquemaValidator` la referencia como `Categorias` — coinciden
igual porque la comparación es sin distinguir mayúsculas).

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| descripcion | nvarchar(255)       | sí   |
| estadof     | nvarchar(100)       | sí   |
| emision     | datetime            | sí   |
| edicion     | datetime            | sí   |
| codigo      | int                 | sí   |
| id          | uniqueidentifier    | NO   |
| usuario     | uniqueidentifier    | sí   |
| usuarioE    | uniqueidentifier    | sí   |
| empresa     | uniqueidentifier    | sí   |

### `correcciones`
Líneas de `documentosC` (correcciones de stock).

| Columna     | Tipo               | Null | Nota |
|-------------|---------------------|------|------|
| indice      | int                 | sí   | |
| cantidad    | float               | sí   | |
| estadof     | nvarchar(100)       | sí   | |
| id          | uniqueidentifier    | NO   | |
| documentoC  | uniqueidentifier    | sí   | FK → documentosC |
| articulo    | uniqueidentifier    | sí   | FK → articulos |

### `documentosC`
Cabecera de correcciones de stock.

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| fecha       | datetime            | sí   |
| emision     | datetime            | sí   |
| edicion     | datetime            | sí   |
| referencia  | nvarchar(255)       | sí   |
| estadof     | nvarchar(100)       | sí   |
| movimiento  | nvarchar(255)       | sí   |
| observacion | nvarchar(255)       | sí   |
| motivo      | nvarchar(255)       | sí   |
| codigo      | nvarchar(100)       | sí   |
| id          | uniqueidentifier    | NO   |
| sucursal    | uniqueidentifier    | sí   |
| usuario     | uniqueidentifier    | sí   | creó (fijo, no se toca al editar) — determina quién puede eliminar/ocultar el documento además del admin |
| usuarioE    | uniqueidentifier    | sí   | editó por última vez |

### `documentosI`
Cabecera de inventarios.

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| observacion | nvarchar(255)       | sí   |
| fecha       | datetime            | sí   |
| emision     | datetime            | sí   |
| edicion     | datetime            | sí   |
| estadof     | nvarchar(100)       | sí   |
| codigo      | nvarchar(100)       | sí   |
| id          | uniqueidentifier    | NO   |
| sucursal    | uniqueidentifier    | sí   |
| usuario     | uniqueidentifier    | sí   | creó (fijo, no se toca al editar) — determina quién puede eliminar/ocultar el documento además del admin |
| usuarioE    | uniqueidentifier    | sí   | editó por última vez |
| referencia  | nvarchar(255)       | sí   |

### `documentosL`
Cabecera de listas de precios.

| Columna     | Tipo               | Null | Nota |
|-------------|---------------------|------|------|
| id          | uniqueidentifier    | NO   | |
| codigo      | nvarchar(100)       | sí   | |
| fecha       | datetime            | sí   | |
| emision     | datetime            | sí   | |
| edicion     | datetime            | sí   | |
| estadof     | nvarchar(100)       | sí   | |
| observacion | nvarchar(255)       | sí   | |
| usuario     | uniqueidentifier    | sí   | |
| usuarioE    | uniqueidentifier    | sí   | |
| referencia  | nvarchar(255)       | sí   | |
| region      | uniqueidentifier    | sí   | |
| estado      | nvarchar(100)       | sí   | |
| empresa     | nvarchar(255)       | sí   | ⚠ `nvarchar`, no `uniqueidentifier` — funciona porque las consultas comparan contra literales de texto |

### `documentosP`
Cabecera de pedidos (ventas/compras).

| Columna     | Tipo               | Null | Nota |
|-------------|---------------------|------|------|
| fecha       | datetime            | sí   | |
| estado      | nvarchar(100)       | sí   | |
| tipo        | nvarchar(100)       | sí   | |
| emision     | datetime            | sí   | |
| edicion     | datetime            | sí   | |
| referencia  | nvarchar(255)       | sí   | |
| estadof     | nvarchar(100)       | sí   | |
| movimiento  | nvarchar(255)       | sí   | |
| observacion | nvarchar(255)       | sí   | |
| estadoC     | nvarchar(100)       | sí   | "pendiente"/"cancelado"/"pendiente parcial" — estado de cuenta |
| estadoA     | nvarchar(100)       | sí   | "sin factura"/"con factura" — se recalcula solo según si el pedido tiene líneas en la pestaña Facturas |
| codigo      | nvarchar(100)       | sí   | |
| id          | uniqueidentifier    | NO   | |
| sucursal    | uniqueidentifier    | sí   | sucursal emisora (única columna de sucursal; `emitido` se eliminó por ser duplicada) |
| usuario     | uniqueidentifier    | sí   | creó (fijo, no se toca al editar) — determina quién puede eliminar/ocultar el documento además del admin |
| usuarioE    | uniqueidentifier    | sí   | editó por última vez |
| tercero     | uniqueidentifier    | sí   | cliente/proveedor |

### `documentosT`
Cabecera de traspasos entre sucursales.

| Columna     | Tipo               | Null | Nota |
|-------------|---------------------|------|------|
| fecha       | datetime            | sí   | |
| estado      | nvarchar(100)       | sí   | |
| emision     | datetime            | sí   | |
| edicion     | datetime            | sí   | |
| referencia  | nvarchar(255)       | sí   | |
| estadof     | nvarchar(100)       | sí   | |
| observacion | nvarchar(255)       | sí   | |
| codigo      | nvarchar(100)       | sí   | |
| id          | uniqueidentifier    | NO   | |
| origen      | uniqueidentifier    | sí   | |
| destino     | uniqueidentifier    | sí   | |
| emitido     | uniqueidentifier    | sí   | sucursal que emitió (ver cascada `emitido → sucursales → empresas` en `CodigoRegenerator`) |
| usuario     | uniqueidentifier    | sí   | creó (fijo, no se toca al editar) — determina quién puede eliminar/ocultar el documento además del admin |
| usuarioE    | uniqueidentifier    | sí   | editó por última vez |

### `empresas`

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| id          | uniqueidentifier    | NO   |
| descripcion | nvarchar(255)       | sí   |
| signo       | nvarchar(4)         | sí   |
| observacion | nvarchar(255)       | sí   |
| fecha       | datetime            | sí   |
| emision     | datetime            | sí   |
| edicion     | datetime            | sí   |
| usuario     | uniqueidentifier    | sí   |
| usuarioE    | uniqueidentifier    | sí   |
| estadof     | nvarchar(100)       | sí   |
| codigo      | int                 | sí   |

### `entregas`
Líneas de entrega de un pedido.

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| indice      | int                 | sí   |
| cantidad    | float               | sí   |
| fecha       | datetime            | sí   |
| estadof     | nvarchar(100)       | sí   |
| id          | uniqueidentifier    | NO   |
| documentoP  | uniqueidentifier    | sí   |
| articulo    | uniqueidentifier    | sí   |

### `facturas`
Línea colgada directamente de `documentosP` (como `pedidos`/`transacciones`/
`entregas`) — gestionada desde la pestaña "Facturas" de `PedidosDetalle`.

| Columna     | Tipo               | Null | Nota |
|-------------|---------------------|------|------|
| id          | uniqueidentifier    | NO   | |
| indice      | int                 | sí   | |
| concepto    | nvarchar(255)       | sí   | |
| importe     | float               | sí   | |
| estadof     | nvarchar(100)       | sí   | |
| documentoP  | uniqueidentifier    | sí   | FK → documentosP (antes `documentoF` → `documentosF`, eliminada) |
| categoria   | uniqueidentifier    | sí   | FK → categorias |
| estado      | nvarchar(100)       | sí   | "con deuda"/"sin deuda" — "con deuda" suma al Saldo del pedido; "sin deuda" no (p. ej. las líneas que genera el botón "Facturar pedido") |

### `familias`

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| descripcion | nvarchar(255)       | sí   |
| estadof     | nvarchar(100)       | sí   |
| observacion | nvarchar(255)       | sí   |
| emision     | datetime            | sí   |
| edicion     | datetime            | sí   |
| codigo      | int                 | sí   |
| id          | uniqueidentifier    | NO   |
| producto    | uniqueidentifier    | sí   |
| usuario     | uniqueidentifier    | sí   |
| usuarioE    | uniqueidentifier    | sí   |

### `industrias`

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| descripcion | nvarchar(255)       | sí   |
| estadof     | nvarchar(100)       | sí   |
| emision     | datetime            | sí   |
| edicion     | datetime            | sí   |
| codigo      | int                 | sí   |
| id          | uniqueidentifier    | NO   |
| usuario     | uniqueidentifier    | sí   |
| usuarioE    | uniqueidentifier    | sí   |
| empresa     | uniqueidentifier    | sí   |

### `inventarios`
Líneas de `documentosI`.

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| cantidad    | float               | sí   |
| estadof     | nvarchar(100)       | sí   |
| id          | uniqueidentifier    | NO   |
| documentoI  | uniqueidentifier    | sí   |
| articulo    | uniqueidentifier    | sí   |

### `pedidos`
Líneas de `documentosP`.

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| indice      | int                 | sí   |
| cantidad    | float               | sí   |
| importe     | float               | sí   |
| tipo        | nvarchar(100)       | sí   |
| estadof     | nvarchar(100)       | sí   |
| id          | uniqueidentifier    | NO   |
| documentoP  | uniqueidentifier    | sí   |
| articulo    | uniqueidentifier    | sí   |

### `precios`
Líneas de `documentosL`.

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| precio      | float               | sí   |
| estadof     | nvarchar(100)       | sí   |
| id          | uniqueidentifier    | NO   |
| articulo    | uniqueidentifier    | sí   |
| documentoL  | uniqueidentifier    | sí   |

### `productos`

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| descripcion | nvarchar(255)       | sí   |
| estadof     | nvarchar(100)       | sí   |
| emision     | datetime            | sí   |
| edicion     | datetime            | sí   |
| codigo      | int                 | sí   |
| id          | uniqueidentifier    | NO   |
| usuario     | uniqueidentifier    | sí   |
| usuarioE    | uniqueidentifier    | sí   |
| empresa     | uniqueidentifier    | sí   |

### `regiones`

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| descripcion | nvarchar(255)       | sí   |
| estadof     | nvarchar(100)       | sí   |
| emision     | datetime            | sí   |
| edicion     | datetime            | sí   |
| codigo      | int                 | sí   |
| id          | uniqueidentifier    | NO   |
| usuarioE    | uniqueidentifier    | sí   |
| usuario     | uniqueidentifier    | sí   |
| signo       | nvarchar(4)         | sí   |
| empresa     | uniqueidentifier    | sí   |

### `sucursales`

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| nit         | nvarchar(255)       | sí   |
| descripcion | nvarchar(255)       | sí   |
| direccion   | nvarchar(255)       | sí   |
| telefono    | nvarchar(255)       | sí   |
| observacion | nvarchar(255)       | sí   |
| estadof     | nvarchar(100)       | sí   |
| emision     | datetime            | sí   |
| edicion     | datetime            | sí   |
| fecha       | datetime            | sí   |
| codigo      | int                 | sí   |
| id          | uniqueidentifier    | NO   |
| region      | uniqueidentifier    | sí   |
| usuario     | uniqueidentifier    | sí   |
| usuarioE    | uniqueidentifier    | sí   |
| signo       | nvarchar(4)         | sí   |
| empresa     | uniqueidentifier    | sí   |
| tipo        | nvarchar(100)       | sí   |

### `terceros`
Clientes/proveedores.

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| nit         | nvarchar(255)       | sí   |
| descripcion | nvarchar(255)       | sí   |
| telefono    | nvarchar(255)       | sí   |
| contacto    | nvarchar(255)       | sí   |
| direccion   | nvarchar(255)       | sí   |
| contacto2   | nvarchar(255)       | sí   |
| telefono2   | nvarchar(255)       | sí   |
| observacion | nvarchar(255)       | sí   |
| estadof     | nvarchar(100)       | sí   |
| emision     | datetime            | sí   |
| edicion     | datetime            | sí   |
| codigo      | int                 | sí   |
| id          | uniqueidentifier    | NO   |
| usuario     | uniqueidentifier    | sí   |
| usuarioE    | uniqueidentifier    | sí   |
| empresa     | uniqueidentifier    | sí   |

### `transacciones`
Movimientos contables de `documentosP`.

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| fecha       | datetime            | sí   |
| descripcion | nvarchar(255)       | sí   |
| indice      | int                 | sí   |
| importe     | float               | sí   |
| forma       | nvarchar(255)       | sí   |
| estadof     | nvarchar(100)       | sí   |
| id          | uniqueidentifier    | NO   |
| documentoP  | uniqueidentifier    | sí   |

### `traspasos`
Líneas de `documentosT`.

| Columna     | Tipo               | Null |
|-------------|---------------------|------|
| indice      | int                 | sí   |
| cantidad    | float               | sí   |
| estadof     | nvarchar(100)       | sí   |
| id          | uniqueidentifier    | NO   |
| documentoT  | uniqueidentifier    | sí   |
| articulo    | uniqueidentifier    | sí   |

### `usuarios`

| Columna     | Tipo               | Null | Nota |
|-------------|---------------------|------|------|
| cuenta      | nvarchar(255)       | sí   | login |
| llave       | nvarchar(255)       | sí   | hash de contraseña (`PasswordHasher`) |
| nombres     | nvarchar(255)       | sí   | |
| apellidos   | nvarchar(255)       | sí   | |
| estadof     | nvarchar(100)       | sí   | |
| tipo        | nvarchar(100)       | sí   | rol (admin / otros) |
| temaC       | nvarchar(255)       | sí   | tema visual elegido |
| codigo      | int                 | sí   | |
| id          | uniqueidentifier    | NO   | |
| sucursal    | uniqueidentifier    | sí   | |
| empresa     | uniqueidentifier    | sí   | |
| emision     | datetime            | sí   | agregada en esta sesión |
| edicion     | datetime            | sí   | agregada en esta sesión |

Sin columnas `descripcion`, `usuario` ni `usuarioE` (a diferencia del resto de
las tablas maestras).
