# Diseño de la nieve renovada (Nivel Nieve)

Plano de la nieve nueva. Coordenadas en unidades del mundo (1 casilla del tileset = 1 u; el player mide 1,04).
Superficie = parte de arriba de la roca. Todo se genera con `Assets/Editor/Ronda19.cs` (menú Warrior > Nieve).

La nieve antigua se conserva en `Assets/Scenes/Nieve_Antigua.unity` (fuera de la lista de escenas).
La que se juega se llama siempre "Nivel Nieve" (de ese nombre dependen el logro "Aliento helado",
las partidas, el jefe vencido y el desafío de la Sombra). Para cambiar: Warrior > Nieve > Usar la nieve antigua / nueva.

## Medidas del player (para dimensionar)
| Movimiento | Valor | Regla del nivel (con margen) |
|---|---|---|
| Salto | sube 1,7 u (fuerza 10, gravedad x3) | escalones de 1 u sin doble salto |
| Doble salto | sube 3,4 u en total | escalones de 2 u como máximo en el camino principal |
| Distancia de salto | 3,4 u andando, 5,8 u corriendo | grietas mortales de 2 u |
| Otros | carrera y salto por paredes, agarre de cornisa | la atalaya y el túmulo se suben con repisas de 2 u |

## Zonas (con nombre: servirán para el mapa)
| # | Zona | x | Superficie | Qué hay | Mecánica / peligro (uno por sala) | Enemigos |
|---|---|---|---|---|---|---|
| 1 | Campamento del Paso | -6 – 34 | 0 | Entrada, **hoguera 1** (10), estatua de la espada | — (enseña a imbuir) | Rata (sola) |
| 2 | Bosque Nevado | 34 – 112 | 2 → 3 → 7 → 6 → 3 | Hielo de enseñanza (56–68), grieta de enseñanza (74–76), loma con la **Cueva del Ermitaño** (pared ilusoria en x 84) | Hielo resbaladizo (seguro) · grieta mortal · **montículo de enseñanza** (x 92) | Murciélago (solo), 2 ratas (una flanquea), rata emboscada |
| 3 | Claro de la Hoguera | 106 – 122 | 3 | **Hoguera 2** (116), **primer sello** (sombra, x 105) con estatua que lo explica y una cueva con almas detrás | Sello de enseñanza (sin enemigos) | — |
| 4 | Desfiladero del Viento | 122 – 196 | 3 | Ventisca en contra, 3 grietas (132, 150, 168), **refugios** tras rocas antes de cada grieta | Ventisca con ráfagas avisadas | Murciélago, Ojo vigía (avisa a los cercanos), 2 ratas (una se retira y vuelve) , murciélago |
| 5 | Lago Helado | 196 – 240 | 3 | Lago congelable (escarcha), **Túmulo del Lago** (226–232) con la salida del **atajo**, **hoguera 3** (236) | Sello de escarcha (lago) | Ojo |
| 6 | Cuevas de Hielo | 240 – 282 | 3 (galería) / -3 (galería inferior) | Muro de hielo (fuego), **hielo quebradizo de enseñanza** (256–259, cae seguro a la galería inferior), galería inferior con el pasadizo al túmulo (**atajo** a la hoguera 3) | Sello de fuego · hielo quebradizo | Hechicero; 2 ratas emboscadas abajo |
| 7 | Ruinas de la Meseta | 282 – 330 | 3 / meseta 10 / atalaya 20 | Hielo quebradizo **con pista** sobre una grieta (286–288), escalones, meseta con hielo, **Atalaya** (repisas hasta 20) con el cofre de mejora tras un **bloque de hielo** (fuego) | Hielo quebradizo con pista · hielo resbaladizo arriba | Rata + arquera en alto (combinación), élite **Morgath** en la meseta, arquera, Ojo vigía |
| 8 | Cripta de la Meseta | 300 – 329 (bajo la meseta) | 3 | **Sala de la emboscada** tras el **sello de sombra** (cara este de la meseta, x 329): 3 ratas salen de montículos; cofre de 800 almas + frasco (el de siempre) y estatua | Emboscada controlada (sello sagrado) | 3 ratas emboscadas |
| 9 | Antesala de la Sombra | 330 – 362 | 3 | **Hoguera 4** (350), estatua del guerrero, zona de jefe (362,6) | — | Hechicero |
| 10 | Arena de la Sombra | 362 – 398 | 3 (techo 20) | Igual que antes: medidas, colisiones, aparición, cámara, zona de jefe | El jefe | Sombra de los Humedales |

## Recorrido
Campamento → bosque (hielo, grieta, loma) → claro (hoguera 2, primer sello) → desfiladero (ventisca, 3 grietas
con refugios) → lago (se congela con escarcha) → hoguera 3 → cuevas (muro de hielo con fuego; el suelo
quebradizo te deja caer a la galería inferior) → ruinas (meseta y atalaya) → bajada a la antesala → hoguera 4 → jefe.

Caminos secundarios: la Cueva del Ermitaño, la galería inferior (y el atajo), la Atalaya, la Cripta.
Atajo: desde la galería inferior se sigue por un pasadizo bajo la orilla y se sube al Túmulo del Lago; su
derrumbe solo cede desde dentro y deja la salida junto a la hoguera 3.

Duración: el camino principal mide lo mismo de ancho (0 a 362, la arena no se mueve); con las subidas y bajadas
nuevas (loma, galerías, meseta) es algo más largo, en torno a un 10-15 %. Los caminos secundarios suman unos 120 u.

## Mecánicas elegidas
1. **Sellos elementales** (sustituyen y amplían el "portal oscuro"): se guardan en la partida al abrirse.
2. **Hielo quebradizo**: cruje, suelta polvo de hielo y cede. El primero cae a un sitio seguro; el segundo, con grietas marcadas, sobre una grieta.
3. **Ventisca con refugios**: tras las rocas marcadas el viento no empuja (se ve un remanso sin nieve volando).
4. **Montículos que esconden emboscadas**: el montículo tiembla y suelta nieve antes de que salga el enemigo.
Se conservan el hielo resbaladizo y el lago congelable.

## Sellos (de más claro a más sutil)
| Sello | Dónde | Imbuición | Señales | Recompensa |
|---|---|---|---|---|
| 1. Sombra del claro (enseñanza) | Claro, x 105, junto a la hoguera 2 | Sagrado | Estatua con mensaje claro, color dorado, runa dorada al lado, texto al golpearlo con otro elemento | Cueva con 250 almas |
| 2. Lago helado | x 200–217 | Escarcha | Estatua (insinúa), vaho azul, runa azul en la orilla | El camino (puente de hielo) |
| 3. Muro de hielo | x 246 | Fuego | Estatua (marcas de quemaduras), runa naranja y grietas naranjas en la roca | El camino |
| 4. Bloque de la Atalaya | Atalaya, x 303, a 20 de altura | Fuego | Se ve brillar desde la meseta; runa naranja grabada en las ruinas; estatua que dice "mira arriba" | Cofre de mejora (Lágrima carmesí + celeste, el de siempre) |
| 5. Sombra de la Cripta (el portal oscuro) | Cara este de la meseta, x 329 | Sagrado | Brillo dorado en el suelo, runa dorada en la ruina, se ve desde la meseta antes de bajar | La Cripta: emboscada, 800 almas + frasco, estatua |

Sin la imbuición correcta, el sello parpadea y suena (solo el primero dice algo con texto).
Colores, runas, sonidos y textos de reacción: `Resources/PistasSellos` (editable).

## Secretos
| Secreto | Tipo | Pista | Recompensa |
|---|---|---|---|
| Cueva del Ermitaño (x 85–95) | Pared ilusoria | Grieta fina y polvo en la cara de la loma, una lápida delante | 300 almas + estatua (historia) |
| Sombra del claro | Sello (enseñanza) | Estatua junto a la hoguera | 250 almas |
| Galería inferior y atajo | Camino que se revela con el hielo quebradizo | El suelo cruje antes de ceder | Atajo a la hoguera 3 + estatua (historia) |
| Atalaya | Sello de fuego en lo alto | Brillo naranja visible desde abajo, estatua | Cofre de mejora |
| Cripta de la Meseta | Sello sagrado + sala de emboscada | Brillo dorado, runa | 800 almas + frasco + estatua |

## Enemigos (antes 17, ahora 23)
| Comportamiento | Dónde |
|---|---|
| Emboscada (montículo que tiembla y suelta nieve) | Loma del bosque (x 92, enseñanza), galería inferior (2), Cripta (3) |
| Combinación (cuerpo a cuerpo + arquera en alto) | Entrada de las ruinas (rata + arquera en el escalón alto) |
| Flanqueo (salta por encima para atacar por detrás si otro ya te ataca de frente) | Bosque (rata de x 81), desfiladero (rata de x 182) |
| Alerta en cadena (con tope de 2 avisados) | Ojos vigía (desfiladero y ruinas) |
| Reacción a imbuiciones (la rata de escarcha se crece si usas escarcha y se vuelve cauta con fuego) | Todas las ratas de escarcha |
| Retirada y regreso (con poca vida huye, avisa a otros y vuelve) | Desfiladero (rata de x 177) |
| Élite | Morgath, en la meseta de las ruinas |
