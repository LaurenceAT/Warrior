# Warrior

## De qué trata
Juego de acción y plataformas 2D en pixel art, con combate y progresión al estilo souls (hogueras, almas, frascos, jefes con dos fases).
Orden: Menú principal → Nivel Nieve (jefe: Sombra de los Humedales) → Nivel Cueva (jefe: Crimson Wraith).

## Herramientas
- Unity 6000.0.71f1, C#, URP 2D, Cinemachine 3, Input System (nuevo), Tilemap + Tilemap Extras (RuleTile), TextMesh Pro.
- Repositorio: remoto `Warrior` (github.com/LaurenceAT/Warrior), rama `master`.
- Casi toda la interfaz (HUD, menús, pantallas) se construye por código, no en escenas.

## Organización
- `Assets/Scenes`: `Menu Principal`, `Nivel Nieve`, `Nivel Cueva` (en ese orden en Build Settings).
- `Assets/Scripts/PlayerControler.cs`: todo el jugador (movimiento, espada, bloqueo/parry, daño, frascos, F para interactuar). Archivo con finales de línea CRLF.
- `GatherInput.cs`: teclas. Q frasco de sangre, R frasco de maná, F interactuar, E rueda de imbuir. El arco está apagado.
- `PlayerHud.cs`: barras de vida/estamina/maná, anillo de imbuición, barras de estados.
- `RPG/`: `Progreso` (niveles y almas), `AjustesProgreso` (números editables), `Equipo` (mejoras de espada y frascos), `EstadosPlayer`, `EstadosEnemigo`, `ArmaImbuida`, `MenuHoguera`, `RecursosRPG` (sonidos e iconos por clave).
- `Jefe/`: `JefeSombra`, `JefeWraith`, `ArenaJefe` (combate, música, recompensa), `PeligrosJefe` (ataques en el escenario).
- `Menu/`: `MenuPrincipal`, `Partida` (partidas guardadas en JSON).
- `UI/`: `EstiloMenu` (estilo común y sonidos de menús), `MenuPausa`, `PantallaCarga`, `PantallaMuerte`, `AvisoObjeto`.
- `NivelNieve/`, `NivelCueva/`: piezas de cada nivel (hoguera, cofres, pared falsa, zonas ocultas, hielo...).
- `Niveles/ConfigNivel.cs` + `Assets/Data/Niveles/Config Nivel *.asset`: tiles, parallax, música, sonidos y frases de cada nivel.
- `Assets/Resources/`: `AjustesProgreso.asset` y `RecursosRPG.asset`.
- `Assets/Editor/`: generadores y ajustes (`ActualizarProyecto`, `Ronda6`, `ConfigurarRecursosRPG`...) y pruebas automáticas (`PruebaNieve`).
- `Assets/SPRITES PARA NUEVOS NIVELES/`: assets nuevos del usuario (sprites, iconos, sonidos). No borrar.
- `Assets/Sprites/PJ_Knight/`: sprites del personaje. No borrar.

## Decisiones importantes
- Todo lo ajustable se edita en el Inspector: niveles en Config Nivel, números del RPG en AjustesProgreso, sonidos en `Assets/Data/Sonidos`.
- La música y las frases del jefe se cambian en Config Nivel (la ArenaJefe las toma de ahí al empezar).
- El maná se recupera con el frasco de maná (1 carga) y al descansar en la hoguera (se llena); no se recarga al golpear.
- Fondo de la nieve: pixeles del mismo tamaño que los del personaje (Config Nivel → Pixeles Por Unidad Fondo = 28). El ambiente (viñeta, aurora, viento, nieve en planos, primer plano) está en el objeto `AmbienteNieve` de la escena; `Ronda7` lo monta.
- El daño de los jefes es físico o mágico; cada resistencia reduce solo su tipo, con un tope del 55 %.
- Solo los ataques especiales de los jefes aplican estados; los básicos solo hacen daño.
- El sangrado sustituyó al ácido (mismo número de elemento, 4, para no romper datos guardados).
- La hoguera se usa con F y abre un menú; descansar ya no es automático.
- Se conservan en el código el arco, el combate sin arma y la rueda de pociones, aunque estén apagados.

## Hecho y funcionando
- Menú principal con partidas guardadas (nivel, hoguera, tiempo), opciones, controles y ambiente de fuego.
- Portales entre niveles con carga asíncrona, agarre de cornisa, pared falsa y zonas ocultas.
- Niveles pintables con Tile Palette (paletas "Nieve - Suelo/Decoracion", "Cueva - Roca/Decoracion").
- Sistema RPG: almas, subir niveles en la hoguera, resistencias, imbuir la espada (5 elementos).
- Estados en jugador y enemigos, barras que crecen al subir de nivel, anillo de imbuición.
- Mejoras: Piedras de forja (espada) y Lágrimas sagradas (frascos), en cofres ocultos y como recompensa de jefes.
- Pruebas automáticas en batchmode pasando (sistemas, pociones, jefe, cueva, menú, ronda6, nivel).

## Pendiente
- Pintar zonas ocultas en la cueva (hoy no hay ninguna pintada).
- Solo hay 3 Piedras y 2 Lágrimas en el juego (el máximo de mejoras es +5 cada una); faltan más cofres.
- El cofre de mejora de la cueva está junto al Mimic, en la cueva secreta: por confirmar si al usuario le gusta ese sitio.
- Probar a mano en Unity lo de la última ronda (menú de hoguera, cofres, estados) y ajustar números si hace falta.

## Problemas conocidos
- La consola muestra avisos de `AnimadorHoja` (unos 33): por confirmar la causa.
- El progreso también se guarda en PlayerPrefs; al probar escenas sueltas en el editor pueden quedar niveles de otra prueba.
- Para las pruebas en batchmode, Unity tiene que estar cerrado.

## Reglas para trabajar conmigo
- No soy experto: explícame las cosas de forma sencilla.
- Antes de modificar un script, dime qué vas a cambiar.
- No borres archivos sin preguntarme (enséñame la lista primero).
- Respuestas cortas y directas; no repitas código que no cambió.
- Si te falta información, pregúntame en vez de inventar.
- Háblame en español.
- Haz commit y sube a GitHub solo cuando te lo pida.
- No trabajes en la nube: todo en local.
