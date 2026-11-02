# Diseño de la nieve del diagrama (Nivel Nieve)

Plano del nivel que se genera desde el dibujo `nivel nieve.drawio` (leyenda: `LEYENDA_DRAWIO.md`).
Generador: `Assets/Editor/GenerarNieveDiagrama.cs`, menú Warrior > Nieve > Generar desde el diagrama.
Lo que no está en el dibujo: ficha `Assets/Data/Niveles/Extras Nieve`. Corriente de viento: `Assets/Data/Niveles/Corriente Viento`.

Siempre parte de `Assets/Scenes/Nieve_Ronda19.unity` (la nieve de la Ronda 19, fuera de la lista de escenas)
y la guarda como "Nivel Nieve". Se quedan igual: fondo (parallax), ambiente, cámara, interfaz, arena de la Sombra
(zona de jefe y niebla) y portal. Para cambiar de versión: Warrior > Nieve > Usar la nieve Ronda 19 / del diagrama.

## Escala (D1)
- 30 unidades del dibujo = 1 casilla a lo ancho; 50 = 1 casilla a lo alto (el dibujo exagera las alturas).
- `x = 362 + (xDibujo − 9220) / 30`, `y = 3 − (yDibujo + 80) / 50`, redondeando a casillas.
- x 9220 del dibujo cae en x 362: la entrada de la arena de la Sombra, que no se mueve.
- Por debajo de y −5 todo el aire lleva pared de cueva detrás (el parallax no llega tan abajo).

## Medidas del player
| Movimiento | Valor |
|---|---|
| Salto | sube 1,7 u (fuerza 10, gravedad ×3) |
| Doble salto | sube 3,4 u en total |
| Carrera | 8,5 u/s (andando 5) |
| Otros | agarre de cornisa, carrera y salto por paredes |

## Zonas (coordenadas del mundo; entre paréntesis, las del dibujo)
| # | Zona | x | Suelo | Qué hay |
|---|---|---|---|---|
| 1 | Zona inicial | 41 – 69 (−400 – 440) | −5 | Inicio, estatuas de la espada y de la cripta, rata, rata en montículo |
| 2 | Zanja | 69 – 72 (440 – 520) | — | Caída mortal (3 u) |
| 3 | Hoguera 1 "Paso de la Zanja" | 78 (700) | −4 | Estatua de la sombra del claro |
| 4 | Escondite 4 | 72 – 82, bajo la hoguera | −14 | Pared falsa (x 82), sello de enseñanza (Sagrado), cofre de 300 almas |
| 5 | Bajada | 89 – 93 (1040 – 1160) | — | Caída mortal bajo el hueco; se cae al camino bajo |
| 6 | Camino alto | 93 – 122 (1200 – 2030) | −6 / −3 | Rata que flanquea + arquera en el escalón |
| 7 | Pared que se rompe y atajo | 122 – 124 | −3 | Solo cede desde la derecha (clave `atajo_Nivel Nieve_d_…Q11n-1`); detrás, estatua de los picadores y cofre de 500 almas |
| 8 | Camino bajo helado | 92 – 136 (1160 – 2440) | −15 / −18 | Hielo (x 111–136), rata en montículo, hechicero, estatua de la ladera |
| 9 | Vacío con corriente de viento | 136 – 152 (2440 – 2910) | — | Caída mortal; la corriente sube y baja por turnos |
| 10–11 | Pasillo y arena del minijefe | 160 – 208, bajo los bloques | −20 / −22 | Pared falsa (x 168), sello de la cripta (Sagrado), estatua de la grieta sombría; arena opcional (x 179–208, 6 u de alto) con el **Martial Hero**: zona de jefe y muro de niebla propios, música de jefe de la nieve, solo almas, bandera `minijefe_Nivel Nieve_martial` (no cuenta como la Sombra) |
| 12 | Plataformas resbaladizas | 134 – 165 | −10 / −13 / −6 | Ojo vigía y murciélago |
| 13 | Hoguera 2 "Bajo el Viento" | 192,7 (4140) | −3 | Muro de hielo (Fuego) antes, estatuas del muro y de la ventisca |
| 14 | Agua para congelar | 233 – 251 (5360 – 5880) | −3 | Lago (escarcha); clave `sello_Nivel Nieve_d_…zN3-6` |
| 15 | Ventisca | 208 – 304 (4600 – 7470) | −3 → −10 | Escalones flotantes, 4 refugios, 2 murciélagos y 2 ojos |
| 16 | Zona del élite | 320 – 337, plataforma alta | 4 | Morgath, bloque de hielo (Fuego), cofre con Lágrima carmesí |
| 17 | Escondite 17 | 320 – 331 | −3 | Pared falsa (x 331), cofre con Frasco de sangre (+1 carga) |
| 18 | Hoguera 3 "Antesala de la Sombra" | 350,3 (8870) | 1 | Escalera de 5 repisas desde el suelo del élite (−10); estatua del guerrero |
| 19 | Arena de la Sombra | 362 – 398 | 3 | Igual que antes |

## Ajustes de escala (tabla D1, en la ficha `rocasExtra`)
| Ajuste | Por qué |
|---|---|
| Suelo del élite alargado 3 u a la izquierda | El último salto medía 7,9 u con viento en contra; ahora 5 u |
| 5 repisas de 2 u en la pared de la hoguera 3 | El bloque sube 11 u; así no depende de la estamina |
| Plataforma 15 alargada (repisa delante de la pared falsa) | Para llegar al escondite 17 desde la escalera |
| Plataforma 14 alargada | Acerca la plataforma del élite a la última repisa |
| Pared del principio | Cierra el nivel por la izquierda |

## Corriente de viento (D3, ficha `Corriente Viento`)
| Fase | Duración | Efecto (solo en el aire) |
|---|---|---|
| Sopla hacia arriba | 3,5 s | Lleva al player a +6 u/s |
| Pausa (aviso) | 0,5 s | Nada; los copos casi se paran |
| Sopla hacia abajo | 2 s | Lleva al player a −10 u/s |
| Pausa (aviso) | 0,5 s | Nada |
En el suelo no hace nada. Al empezar cada soplido suena `ventisca_rafaga` (más agudo hacia arriba, más grave hacia abajo).
No toca el PlayerControler: ajusta la velocidad vertical del cuerpo del player.

## Claves guardadas (nunca coinciden con las de la Ronda 19)
- Formas del dibujo: `d_` + id de la forma (cofres, atajo, agua).
- Sellos de la ficha: `dx_` + nombre (`dx_ensenanza`, `dx_muro`, `dx_bloque`, `dx_cripta`).
