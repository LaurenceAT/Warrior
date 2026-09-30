# Diseño de la cueva nueva (Nivel Cueva)

Plano de la cueva renovada. Coordenadas en unidades del mundo (el player mide 1,04).
Cuadrícula: casillas de 16 px del pack `Mapa_CuevaPixelFantasy` a 28 px por unidad
(igual que el player): 1 casilla = 4/7 u = 0,571 u.

La cueva antigua se conserva en `Assets/Scenes/Cueva_Antigua.unity` (fuera de la lista de escenas).
El generador es `Assets/Editor/CrearCuevaNueva.cs` (menú Warrior > Cueva).

## Medidas del player (para dimensionar)
| Movimiento | Valor | Regla del nivel (con margen) |
|---|---|---|
| Salto | sube ~1,7 u | escalones de 1,14 u (2 casillas) sin doble salto |
| Doble salto | sube ~3,4 u en total | repisas separadas ≤ 2,6 u en vertical |
| Distancia de salto | 3,4 u andando, 5,8 u corriendo | huecos mortales de 2,3 u (4 casillas) |
| Otros | carrera y salto por paredes, agarre de cornisa | el pozo se puede subir de las dos formas |

## Zonas
| Zona | Dónde (x, suelo) | Qué hay | Peligro (uno por sala) |
|---|---|---|---|
| 1. Entrada húmeda | x 0–38, y 0 | Entrada/reaparición, Slime (primero, solo), Mago en un escalón, **secreto 1** tras una pared ilusoria a la izquierda (almas) | Grieta de enseñanza: 2,3 u de ancho, con niebla que sube (x 21) |
| 2. Pozo de raíces | x 38–52, y 0→20 | Repisas en zigzag, Cacodemonio arriba. **Atajo**: pasillo a la galería inferior (y 8), cerrado por un derrumbe que solo se abre desde el otro lado | — |
| Hoguera 1 | (55, 20) | Descanso y subida de nivel | — |
| 3A. Galería de columnas | x 52–76, y 20 | Mago (x 70), Cacodemonio en lo alto | **Suelo falso de enseñanza** (x 62): cede y cae a la galería inferior, segura |
| 3B. Galería de las grietas | x 76–90, y 20 | Slime | Grieta mortal (x 80) con niebla; la cámara la enseña al acercarte |
| 3C. Borde de la sima | x 90–106, y 20 | Estatua nueva con pista | **Suelo falso con pista** (grietas, tono distinto) sobre una grieta mortal (x 99) |
| 4. Galería inferior (oscura) | x 55–106, y 4 | Mimic y su cofre (Piedra de forja, mismo sitio), élite **Aldren, Hueso Roto**, 2 antorchas apagadas (F), abre el atajo | Oscuridad (círculo de luz) |
| 5. La sima (oscura) | x 106–116, y 20→4 | 4 repisas en zigzag (se baja y se vuelve a subir), antorcha apagada, Cacodemonio, **secreto 2** tras una pared ilusoria en la repisa alta (y 15,4), la galería inferior sale a su fondo (y 4) | Oscuridad |
| 6. Antesala | x 116–140, y 4 | Hoguera 2 (127, 4), estatua del jefe (134), Slime, niebla del jefe (140) | — |
| 7. Arena del Crimson Wraith | x 140–176, y 4–22 | Igual que antes (medidas, colisiones, cámara, música); aspecto nuevo | El jefe |

## Recorrido
Entrada → pozo (subida) → hoguera 1 → galería de columnas → (el suelo falso lleva, si caes, a la
galería inferior) → grietas → borde de la sima → la sima (bajada a oscuras) → antesala → arena.
Camino secundario: la galería inferior (Mimic, cofre, élite, antorchas), que sale a mitad de la sima.
Atajo: desde la galería inferior se abre el derrumbe hacia el pozo (paso a y 4); desde la hoguera 1 se
baja por el pozo y se va directo a la galería inferior y a la antesala.

Duración: recorrido principal de unas 210 u (antes unas 225 u, un 7 % más corto), más la galería
inferior opcional (~55 u).

## Peligros nuevos (sin trampas)
- Zonas de muerte: 3 grietas (x 21, 80 y 99), siempre con niebla que sube, partículas y borde claro.
- Suelos falsos: el de enseñanza (x 62, cae a un lugar seguro) y el de la sima (x 99, con grietas y tono distinto).
- Paredes ilusorias: 2 (secreto 1 en la entrada y secreto 2 en la sima), con salas que solo dan almas.
- Atajo: derrumbe entre la galería inferior y el pozo (se rompe a golpes desde la galería inferior).
- Originales: **brasas guía** (chispas que flotan despacio siguiendo el camino bueno en las zonas oscuras)
  y **eco del abismo** (la cámara baja un poco al acercarte a una grieta para que la veas).

## Oscuridad
Galería inferior y sima: fundido de 0,75 s al entrar, círculo de luz alrededor del player, luz de
antorchas y hogueras. La arena del jefe no es oscura. Los enemigos, el player y los efectos se dibujan
por encima de la oscuridad (siempre legibles).

## Fondo y ambiente
Parallax con los fondos del pack (de lejos a cerca: background1, 2, 3, 4a/4b) con tinte por capa y
por zona, bruma baja, polvo flotando y goteo visual. Sonido de ambiente "Cave.ogg" del pack de sonidos.
No hay cristales, agua, lava ni sonido de goteo en los assets: no se usan.

## Resultado (comprobado con pruebas automáticas)
| | Antes | Ahora |
|---|---|---|
| Enemigos | 9 (3 slimes, 2 magos, 3 cacodemonios, 1 Mimic) | 10 (los mismos + la élite Aldren, Hueso Roto) |
| Hogueras | 2 (55,20 y 127,4) | 2 (mismo sitio) |
| Cofres | 1 de mejora (Piedra de forja) | 1 de mejora (mismo objeto) + 2 de almas en los secretos (350 y 500) |
| Estatuas | 1 (pista del jefe) | 2 (la del jefe + una nueva con pista de los suelos falsos y las paredes huecas) |
| Trampas | 10 estalactitas, 4 fuegos, 2 fosos de pinchos | ninguna |
| Peso de la escena | 2,7 MB | 5,3 MB (roca real en tiles, en vez de dibujarla al abrir la escena) |

Saltos comprobados con los controles de verdad: escalón del mago, las 3 grietas, las 8 repisas del pozo,
la salida a la galería y la subida entera de la sima (18 de 18).

## Dónde se ajusta cada cosa
- **Oscuridad:** objeto `CuevaNueva/Oscuridad` (radio de luz, fundido, intensidad, suavidad).
- **Antorchas:** `CuevaNueva/Antorchas` (radio de su luz en `FuenteLuz`).
- **Suelos falsos:** `CuevaNueva/Peligros/SueloFalso_*` (retraso, reaparición, temblor, pista).
- **Grietas:** `CuevaNueva/Peligros/Grieta_*` (niebla, velo del abismo, motas).
- **Derrumbe del atajo:** `CuevaNueva/Peligros/DerrumbeAtajo` (golpes, lado que abre).
- **Ambiente:** `CuevaNueva/Ambiente` (tinte y bruma de cada zona, polvo, gotas).
- **Brasas guía:** `CuevaNueva/ZonasOscuras/Brasas_*` (camino, ritmo).
- **Cámara:** `Nivel/ZonasCamara` (pozo, sima, eco de cada grieta) y `CuevaNueva/ZonasOscuras` (encuadre de la galería inferior).
- **Depuración (solo Editor):** F8 en Play (ir a cada zona u hoguera, ver peligros, quitar la oscuridad).

## Cambiar entre la cueva nueva y la antigua
Menú **Warrior > Cueva > Usar la cueva antigua** / **Usar la cueva nueva**. Intercambia los archivos:
la que se juega siempre se llama "Nivel Cueva" (así el portal, el logro "Descenso", los desafíos y las
partidas guardadas siguen funcionando); la otra queda como `Cueva_Antigua` o `Cueva_Nueva`, fuera de la
lista de escenas. **Warrior > Cueva > Generar cueva nueva** la vuelve a montar desde la antigua.
