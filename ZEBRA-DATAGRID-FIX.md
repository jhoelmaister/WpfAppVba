# Fix: zebra (filas alternadas oscuro/claro) corrido en grillas con selección/marcado

## Síntoma
En el módulo de **Importar**, las filas de la grilla aparecían con el patrón de
colores alternados (zebra) corrido/desfasado para toda la lista. El problema
solo se daba ahí porque es el único lugar donde, además del zebra, existen
triggers propios que pintan la fila de **azul** (seleccionada) o **verde**
(marcada con checkbox).

## Causa real
El zebra dependía del mecanismo automático e interno de WPF basado en
`AlternationIndex`. Ese mecanismo convive mal con triggers propios que cambian
el color de fondo de la fila (azul/verde). Al activarse/desactivarse esos
triggers, el color de "respaldo" (el zebra) no siempre se recalculaba bien
para esa fila, y el patrón quedaba corrido para el resto de la lista.

## Solución
Dejar de depender del mecanismo interno de WPF (`AlternationIndex`) y calcular
el zebra a partir de un dato real y confiable de la fila:

1. **Propiedad `FilaPar`** (`Linea % 2 == 0`) agregada al modelo de fila
   (ej. `ArticuloFila`). `Linea` ya se recalcula siempre de forma secuencial
   (1, 2, 3...) cada vez que se refresca la grilla, así que es un dato 100%
   confiable — no depende de qué contenedor visual (`DataGridRow`) le tocó a
   la fila al reciclarse.
2. **`RowStyle` del DataGrid**: se agrega un color base + un `DataTrigger`
   que pinta la fila según `FilaPar`, declarado **antes** que los triggers de
   selección/marcado (azul/verde), para que estos últimos sigan ganando
   cuando corresponde (los triggers declarados después tienen prioridad en
   WPF cuando varios aplican a la misma propiedad).

Con esto, el color de cada fila depende únicamente del dato (`Linea`) y de si
está seleccionada/marcada — nunca de cómo WPF recicla los contenedores
internamente, que era la fuente del desfase.

## Para futuros problemas similares
Si en cualquier otra grilla aparece un zebra corrido/desfasado, sobre todo en
pantallas donde la fila también puede pintarse de otro color por selección,
marcado con checkbox, o cualquier otro `Trigger`/`DataTrigger` de fondo:

- **No confiar en `AlternationIndex`** si la grilla tiene otros triggers de
  color de fondo en las filas. `AlternationIndex` es manejado por WPF según
  el reciclado de contenedores visuales, no según el dato real de la fila —
  por eso se desincroniza.
- Replicar el mismo patrón: agregar una propiedad calculada en el modelo
  (basada en un índice secuencial confiable, como `Linea`) y usarla en un
  `DataTrigger` del `RowStyle`, declarado antes que los triggers de
  selección/marcado.
