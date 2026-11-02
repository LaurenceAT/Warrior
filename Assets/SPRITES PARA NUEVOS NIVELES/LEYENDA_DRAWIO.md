# Leyenda de los dibujos de niveles (drawio)

El generador (`Assets/Editor/GenerarNieveDiagrama.cs`, menú Warrior > Nieve > Generar desde el diagrama)
lee el dibujo con EXACTAMENTE esta leyenda. Si encuentra una forma o un color que no está aquí,
avisa en la consola (`[NieveDiagrama]`) y no la usa.

## Reglas generales
- El dibujo NO está a escala. La escala está en la ficha `Assets/Data/Niveles/Extras Nieve`
  (ahora: 30 unidades del dibujo = 1 casilla a lo ancho; 50 = 1 casilla a lo alto).
- Rectángulos sin color = roca. El jugador camina por ENCIMA.
- Todo hueco sin suelo debajo es una caída mortal.
- Un bloque grueso (alto ≥ 150) sin nada debajo baja hasta el fondo del nivel (es el suelo de la montaña).
  Los finos (plataformas, escalones) se quedan flotando. Excepciones: listas `bajanAlFondo` / `noBajanAlFondo` de la ficha.
- Los textos sueltos y las flechas solo explican: el generador no los usa.
- El id de cada forma es su clave para lo que se guarda (atajo, agua congelada, sellos, cofres).
  Si borras una forma y la vuelves a dibujar, cambia su id y eso se olvida en las partidas.

## Formas y colores
| Forma | Color | Qué es | Pieza del juego |
|---|---|---|---|
| Rectángulo | sin color | Suelo o roca maciza | Casillas de nieve |
| Rectángulo | borde discontinuo (`dashed=1`) | Hueco: sala de aire (escondite) | Aire + pared de fondo |
| Rectángulo | azul `#0050ef` | Piso resbaladizo (franja sobre la roca) | Casillas de hielo + `SueloHielo` |
| Rectángulo | celeste `#1ba1e2` | Agua para congelar (caer es morir) | `AguaCongelable` |
| Rectángulo | rojo `#a20025` | Pared falsa (se atraviesa) | `ParedFalsa` |
| Proceso (rectángulo con barras) | gris | Pared que se rompe desde el otro lado | `DerrumbeAtajo` |
| Nube | cualquiera | Zona de ventisca | `ZonaVentisca` |
| Trapecio | cualquiera | Hoguera | `Hoguera` |
| Cilindro | cualquiera | Cofre (su contenido va en la ficha, por id) | `CofreAlmas` / `CofreMejora` |
| Figura (persona) | cualquiera | Enemigo. Texto: "cuerpo a cuerpo", "Volador", "Arquero" o "de Elite" | Enemigo de la ficha (por id) |

## Símbolos nuevos (para futuros dibujos)
| Forma (en drawio) | Qué es | Texto que lleva |
|---|---|---|
| Flecha doble vertical (`doubleArrow`) | Corriente de viento (sube y baja por turnos) | — |
| Rombo (`rhombus`) | Sello elemental | El elemento: Sagrado, Fuego o Escarcha |
| Triángulo (`triangle`) | Estatua con pista | Su id (por ejemplo `estatua_4`) |
| Elipse (`ellipse`), bajo un enemigo | Montículo de emboscada | — |
| Hexágono (`hexagon`) | Refugio de la ventisca (tras una roca) | — |

Mientras estos símbolos no estén en el dibujo, sus posiciones van en la ficha `Extras Nieve`.
