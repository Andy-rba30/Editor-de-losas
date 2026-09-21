# ARBA.Losas — Dividir y unir losas en Revit 2027

Add-in de la suite **ARBA** que añade a la pestaña **ARBA**, panel **Losas**, tres botones:

| Botón | Qué hace |
|---|---|
| **Dividir losa** | Parte una losa (`Floor`) en N losas independientes según líneas de corte dibujadas al vuelo o seleccionadas (líneas de modelo, de detalle o rejillas). |
| **Unir losas** | Fusiona dos o más losas compatibles en una sola, cerrando microgaps menores que la tolerancia de unión. |
| **Configuración** | Opciones de ambos comandos, guardadas en `%AppData%\ARBA\Losas\settings.json`. |

Los comandos también quedan en **Complementos ▸ Herramientas externas**.

## Cómo funciona

La geometría se resuelve fuera de Revit, en `ARBA.Losas.Geometry` (sin referencias a Revit):

1. El contorno de la losa se lee del boceto (`Sketch.Profile`, arcos sin teselar; si no hay boceto, de la cara superior con `HostObjectUtils.GetTopFaces` + `GetEdgesAsCurveLoops`) y se traduce a `Polygon2` (contorno + huecos). Líneas y arcos se conservan exactos; elipses y splines se teselan como polilínea y se avisa.
2. Los arcos se teselan con la tolerancia configurada (1 mm por defecto) y cada vértice recuerda de qué curva viene y con qué parámetro.
3. Cada línea de corte genera dos semiplanos que se intersecan con cada región vigente usando **Clipper2** (`Paths64`, escala 1e6 sobre unidades internas). Un semiplano puede devolver varias regiones; todas valen. Los huecos quedan en la región que los contiene o se parten.
4. Los vértices de salida se mapean a los de entrada por proximidad (Clipper redondea). Donde un corte cruza un arco, el punto se proyecta sobre el **arco verdadero** y ese mismo punto se usa como extremo del arco recortado y de la línea de corte en las dos losas vecinas, así sus bordes coinciden.
5. Limpieza: vértices más cercanos que la tolerancia de curva corta de Revit (`Application.ShortCurveTolerance`) se fusionan, los tramos colineales se unen y las regiones más pequeñas que el umbral se descartan y se reportan.
6. Cada región vuelve a Revit como `CurveLoop`: los tramos que proceden de un arco original se crean con la curva original recortada (`Clone` + `MakeBound`), los tramos de corte como `Line`. `Floor.Create(doc, loops, tipo, nivel, estructural, null, 0)`.
7. Se copian los parámetros editables de la original, se re-alojan las barras (si se pidió), se restablecen las uniones de geometría y se borra la original, todo dentro de un `TransactionGroup` que se asimila en una sola entrada de deshacer.

## Requisitos

- **Revit 2027** (corre sobre **.NET 10**; los add-ins deben compilarse contra `net10.0-windows`).
- Para compilar: **SDK de .NET 10** (`global.json` fija `10.0.100` con `rollForward: latestFeature`). Los paquetes `Nice3point.Revit.Api.RevitAPI` / `RevitAPIUI` 2027.* (solo ensamblados de referencia, `ExcludeAssets="runtime"`) y `Clipper2` 2.0.0 se descargan de NuGet: **no hace falta Revit instalado para compilar**.
- IDE: **Visual Studio 2026** (18.x) o Rider/VS Code con el SDK 10. Visual Studio 2022 no soporta `net10.0`.
- x64 únicamente.

## Compilación

```
dotnet build -c Release
dotnet test
```

En Linux/macOS el proyecto Revit compila igualmente gracias a `EnableWindowsTargeting`; `dotnet test` ejecuta las pruebas de geometría (xUnit) en cualquier sistema.

| Proyecto | Marco | Contenido |
|---|---|---|
| `ARBA.Losas.Geometry` | net10.0 | `Vec2`, `Segment2`, `Arc2`, `Contour`, `Polygon2`, `SlabSplitter`, `SlabMerger`. Cero referencias a Revit. |
| `ARBA.Losas.Geometry.Tests` | net10.0 | xUnit: rectángulos, L, U, huecos, arcos, tangencias, colineales, limpieza, uniones. |
| `ARBA.Losas.Revit` | net10.0-windows | Add-in: cinta, comandos, ventana WPF (en código), traducción `CurveLoop` ↔ `Polygon2`, transacciones. |

## Instalación

Carpeta de add-ins por usuario de Revit 2027: `%AppData%\Autodesk\Revit\Addins\2027\`

```
%AppData%\Autodesk\Revit\Addins\2027\
├── ARBA.Losas.addin
└── ARBA.Losas\
    ├── ARBA.Losas.Revit.dll
    ├── ARBA.Losas.Geometry.dll
    └── Clipper2Lib.dll
```

- Compilando en **Debug en Windows** el target `CopyToRevit` copia estos archivos solo (propiedad `DeployToRevit`, `true` por defecto en ese caso). Para forzarlo o evitarlo: `dotnet build -p:DeployToRevit=true` / `-p:DeployToRevit=false` (CI).
- Instalación manual: copia los archivos de `ARBA.Losas.Revit\bin\Release\net10.0-windows\` a la estructura de arriba.
- Para todos los usuarios, en Revit 2027 la ruta cambió a `C:\Program Files\Autodesk\Revit\Addins\2027\` (`ProgramData` ya no se lee en 2027). Ahí `settings.json` sigue siendo escribible porque vive en `%AppData%\ARBA\Losas\`.

Revit carga los add-ins al arrancar: si estaba abierto, ciérralo y vuelve a abrirlo.

## Uso

**Dividir losa**: pulsa el botón, revisa las opciones (modo de líneas de corte, extender, eliminar original, copiar parámetros, uniones, barras, tolerancias), acepta y selecciona la losa. En modo *dibujar*, marca puntos encadenados en una vista de planta (cada punto cierra una línea con el anterior; la barra de estado lleva el conteo) y termina con **Esc**. En modo *seleccionar*, elige líneas de modelo, de detalle o rejillas rectas y pulsa Finalizar. Al terminar se muestra el resumen: losas creadas y borradas, barras re-alojadas y advertencias.

**Unir losas**: selecciona dos o más losas y pulsa Finalizar. La losa de mayor área es la maestra de parámetros. Si las losas no se tocan se crea una losa por región y se avisa.

## Opciones (`settings.json`)

| Opción | Por defecto | Efecto |
|---|---|---|
| `cutLineMode` | `draw` | `draw` (PickPoint encadenado) o `select` (líneas/rejillas existentes). |
| `extendCutLines` | `true` | Cada línea se extiende hasta cubrir la losa. Si es `false` la línea se usa tal cual y solo divide donde cruza la región de lado a lado; si no, se avisa. |
| `deleteOriginal` | `true` | Borrar la(s) original(es). Si es `false` se avisa de la superposición y se silencia solo el aviso de Revit "losas solapadas". |
| `copyParameters` | `true` | Copiar parámetros editables (ver abajo). |
| `keepJoins` | `true` | Reproducir en las losas nuevas las uniones de geometría de la original, par a par, con el motivo de cada fallo en las advertencias. |
| `keepRebar` | `false` | Con barras en la losa: `false` aborta; `true` re-aloja cada barra (`Rebar.SetHostId`) en la losa nueva que contiene el centro de su caja envolvente, sin tocar su geometría. |
| `tessellationToleranceMm` | `1` | Flecha máxima al teselar arcos. |
| `joinToleranceMm` | `1` | Microgaps entre losas menores que esto se cierran al unir. |
| `minAreaM2` | `0.01` | Regiones más pequeñas se descartan (slivers) y se reportan. |
| `clipperScale` | `1e6` | Factor de escala a enteros de Clipper2 sobre unidades internas. |

Parámetros que se copian: desfase respecto al nivel, estructural, comentarios, nota clave, fase de creación y demolición, workset (solo en modelos compartidos), parámetros compartidos y de proyecto, y cualquier otro editable. La marca se numera: `Marca-1`, `Marca-2`… No se copian área, perímetro, volumen, elevaciones, nivel, tipo ni identidad (lista en `ParameterCopier.Skipped`).

## Limitaciones (versión 1)

Dentro del alcance: losas de espesor constante, horizontales (un nivel + desfase), con cualquier contorno cerrado (incluidos arcos) y con huecos de boceto, que se preservan.

Fuera del alcance (el comando aborta con un mensaje que indica la condición y el ElementId):

- Losas con **edición de forma** (`SlabShapeEditor.IsEnabled`; se indica cuántos puntos y aristas tienen modificados).
- Losas **inclinadas**: se comprueba con las caras superiores (`HostObjectUtils.GetTopFaces`): si alguna no es `PlanarFace` o su normal no es paralela a Z se aborta; además se comprueba el parámetro de pendiente.
- Losas de **espesor variable** (tipo con capa variable).
- `RoofBase`, `Part` y losas dentro de familias: el filtro de selección solo admite `Floor` de la categoría Suelos, y el comando no funciona en documentos de familia.
- Las líneas de corte deben ser **rectas**; rejillas curvas y arcos se omiten con aviso.
- Las curvas del boceto que no son línea ni arco (elipses, splines) quedan como polilínea en las losas nuevas (se avisa).
- El armado **no se recorta ni se regenera**: el flujo ARBA es dividir primero y armar después con Acero-automatico. Áreas y caminos de refuerzo, mallas, huecos por cara, bordes de losa y familias alojadas en la losa se pierden al borrar la original: se listan como advertencia.

### Condiciones de aborto de *Dividir losa*

- Losa con forma editada, inclinada, de espesor variable o en una familia.
- Losa con barras y "Conservar barras" desactivado: *"La losa X tiene N barras. Divide antes de armar o elimina el acero"*.
- Boceto con un loop que no cierra.
- Ninguna línea de corte, o líneas que no dividen la losa en más de una región (tangentes a un vértice, colineales con un borde, fuera de la losa, o que no la cruzan de lado a lado en modo sin extender).
- `Floor.Create` rechaza alguna región (se indica cuál y su área); en ese caso se deshace todo el grupo de transacciones.

### Condiciones de aborto de *Unir losas*

- Menos de dos losas.
- Alguna losa con forma editada, inclinada, de espesor variable o en una familia.
- Losas de distinto tipo, distinto nivel, distinto desfase (tolerancia 1 mm), distinto flag estructural o no coplanares (Z del boceto).
- Barras con "Conservar barras" desactivado (misma regla que en dividir).
- La unión no produce ninguna región válida, o `Floor.Create` rechaza alguna.

## Advertencias que no abortan

Se acumulan y se muestran en el resumen final: curvas teseladas, líneas de corte omitidas, cortes que no dividieron, slivers descartados, barras que no se pudieron re-alojar (con su ElementId), uniones que fallaron (con el motivo), parámetros que no se pudieron copiar, elementos dependientes que se perderán, regiones desconectadas en la unión, microgaps cerrados por inflado/desinflado y los avisos propios de Revit durante las transacciones.

## Pruebas

`ARBA.Losas.Geometry.Tests` (xUnit, sin Revit): rectángulo con una y dos líneas, L cortada por la esquina, polígono con hueco (corte que no lo toca y corte que lo atraviesa), contorno con arco (área conservada, punto de corte sobre el arco verdadero y compartido por las vecinas, cortes secuenciales), círculo completo, línea que no atraviesa en modo sin extender, tangente a un vértice, corte colineal con un borde, corte por el vértice de un hueco, U cortada dos veces, limpieza de tramos cortos, sliver descartado, procedencia de las curvas, y uniones de rectángulos adyacentes, separados, solapados, en anillo, con microgap menor y mayor que la tolerancia, con bordes desiguales, con arco y con hueco cubierto.
