# Warrior

## De qué trata
Juego de acción y plataformas 2D en pixel art, con combate y progresión al estilo souls (hogueras, almas, frascos, jefes con dos fases).
Orden: Menú principal → Nivel Nieve (jefe: Sombra de los Humedales) → Nivel Cueva (jefe: Crimson Wraith).
Jefe secreto (solo en Desafíos): The Blind Huntress, en la escena `Bosque Cazadora`.

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
- `Jefe/`: `JefeSombra`, `JefeWraith` (en 2 archivos: `JefeWraith` y `JefeWraithPoderes`), `PoseViva` (respira, se inclina y se estira: vida a las poses sueltas), `ArenaJefe` (combate, música, recompensa), `PeligrosJefe` (ataques en el escenario).
- `Desafios/`: `Desafio` (estado), `ModoDesafio` (monta la arena en el nivel real), `TiendaTotem`/`TotemTienda`, `MenuDesafios`, `MenuLogros`, `Logros`, `Globales` (logros y tiempos en `globales.json`, aparte de las partidas). Fichas editables en `Resources/Desafios` (jefes y tienda) y `Resources/Logros`.
- `Cazadora/`: el jefe secreto. `JefeCazadora` (cerebro, en 3 archivos: nucleo, Ataques, Especiales), `CuerpoCazadora` (movimiento con aceleracion), `AnimCazadora` (cuerpo + efecto tenible), `OidoCazadora` (ruidos del player), `ArenaCazadora` (dialogo, musica por fase, reinicio), `IlusionCazadora`, `EfectosCazadora` (olas, marcas, estelas... con reserva), `UICazadora`, `DialogoCazadora`, `OscuridadCazadora`, `AmbienteBosque`, `ParallaxBosque`, `CamaraCazadora`, `DepuracionCazadora` (F10, solo Editor). Numeros en `Assets/Data/Jefes/Ajustes Cazadora`; sonidos en `Assets/Data/Sonidos/Sonidos Jefe Cazadora`. `Ronda10` genera sprites, fondo pixelado, prefab y escena.
- `Menu/`: `MenuPrincipal`, `Partida` (partidas guardadas en JSON).
- `UI/`: `EstiloMenu` (estilo común y sonidos de menús), `MenuPausa`, `PantallaCarga`, `PantallaMuerte`, `AvisoObjeto`.
- `NivelNieve/`, `NivelCueva/`: piezas de cada nivel (hoguera, cofres, pared falsa, zonas ocultas, hielo...).
- `Niveles/ConfigNivel.cs` + `Assets/Data/Niveles/Config Nivel *.asset`: tiles, parallax, música, sonidos y frases de cada nivel.
- `Niveles/EstatuaPista.cs` (estatuas con pista, F para leer) y `UI/CuadroPista.cs` (cuadro de texto con máquina de escribir).
- `RPG/SangreFx.cs` + `AjustesSangre` (sangre), `RPG/EfectoArma.cs` + `MapaHoja` (partículas del arma imbuida), `CaidaMuerte.cs` (muerte en el aire).
- `Assets/Resources/`: `AjustesProgreso.asset`, `RecursosRPG.asset`, `AjustesSangre.asset`, `MapaHoja.asset`.
- `Assets/Sprites/Tajos` (tajos recoloreados) y `Assets/Sprites/Sangre` (sangre en blanco): los genera `Ronda8`.
- `Assets/Editor/`: generadores y ajustes (`ActualizarProyecto`, `Ronda6`, `Ronda7`, `Ronda8`, `ConfigurarRecursosRPG`...) y pruebas automáticas (`PruebaNieve`).
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
- Mejoras de frasco separadas: Lágrima carmesí (curación) y Lágrima celeste (maná), que se gastan en la hoguera; Frasco de sangre/maná (+1 carga) al momento. La vieja Lágrima sagrada cuenta como una de cada; las partidas viejas se migran al cargar (versión 2).
- Los desafíos no usan partida: personaje nuevo, nada se guarda salvo logros y tiempos. En la tienda del tótem lo comprado se aplica al momento.
- La hoguera se usa con F y abre un menú; descansar ya no es automático.
- La pausa solo se abre con Esc (no hay botón en pantalla).
- Las pistas están en estatuas (F para leer); las leídas se guardan en la partida y salen en el "Libro de pistas" de la pausa.
- Sangrado: cuesta vida (25 con la vida inicial, sube con la vida máxima), te acumula sangrado a ti y en jefes tiene tope de activaciones. Números en AjustesProgreso.
- Sagrado: aturde con el mismo aturdimiento del parry, pero más corto; los jefes lo hacen al acabar su ataque.
- Tus manchas de sangre al morir se guardan en la partida; la de los enemigos se desvanece. Quién sangra: EnemyHealth → Sangre.
- Jefe secreto: se desbloquea con los desafios Normal de Wraith y Wetlands (se calcula con `globales.json`). Hasta entonces no sale en Desafios ni cuentan sus 2 logros. Marcas globales: `revelado_*`, `dialogo_cazadora`, `forzar_*`/`bloqueo_*` (pruebas).
- La Cazadora: todo su dano va en fraccion de tu vida maxima; los golpes normales nunca matan con la vida llena (solo instakills, su estocada tras parry o tener poca vida).
- Parry de la Cazadora: al contacto te aturde de verdad (sin control ni teclas guardadas) y ella reapunta a tu posicion real justo antes de la estocada; si sobrevives, sales despedido (`PlayerControler.Derribar`, invulnerable al caer). El escudo se rompe por golpes (10, o 5 con la espada imbuida de oscuridad), no por dano.
- Crimson Wraith (Ronda12): se mueve con aceleracion y frenada, avisos ligero/pesado, poderes nuevos (Guadana doble, Cosecha de sangre, Transfusion, Raices carmesi, Semillas del vacio, Zigzag, Frenesi) y "corazon expuesto" tras Nova, Lluvia y Frenesi (x1.5). Fase 1: fuego x1.3, hielo x1.2, oscuridad x0.7, sangrado x0.6. Fase 2: sagrado x1.8, fuego x1.5, hielo x0.8, oscuridad x0.3, sangrado x0.2 y lo cura. Prueba: `PruebaNieve.WraithPrueba`.
- Se conservan en el código el arco, el combate sin arma y la rueda de pociones, aunque estén apagados.

## Hecho y funcionando
- Menú principal con partidas guardadas (nivel, hoguera, tiempo), opciones, controles y ambiente de fuego.
- Portales entre niveles con carga asíncrona, agarre de cornisa, pared falsa y zonas ocultas.
- Niveles pintables con Tile Palette (paletas "Nieve - Suelo/Decoracion", "Cueva - Roca/Decoracion").
- Sistema RPG: almas, subir niveles en la hoguera, resistencias, imbuir la espada (5 elementos).
- Estados en jugador y enemigos, barras que crecen al subir de nivel, anillo de imbuición.
- Mejoras: Piedras de forja (espada) y Lágrimas sagradas (frascos), en cofres ocultos y como recompensa de jefes.
- Estatuas con pistas, libro de pistas, decoración extra en los dos niveles (objeto `DecoracionExtra`).
- Menú de Desafíos (2 jefes, Normal/Difícil, tótem-tienda, cofre) y de Logros (9, ocultos). Prueba: `PruebaNieve.DesafioPrueba` y `DesafioDificilPrueba`.
- The Blind Huntress (jefe secreto, 3 barras, instakills, oscuridad, ilusiones). Prueba: `PruebaNieve.CazadoraPrueba`.
- Tajos sincronizados con el ataque; colores propios; efectos en el arma; muerte en el aire; sangre.
- Pruebas automáticas en batchmode pasando (sistemas, pociones, jefe, cueva, menú, ronda6, nivel, ronda8).

## Pendiente
- Pintar zonas ocultas en la cueva (hoy no hay ninguna pintada).
- Solo hay 3 Piedras y 2 Lágrimas en el juego (el máximo de mejoras es +5 cada una); faltan más cofres.
- El cofre de mejora de la cueva está junto al Mimic, en la cueva secreta: por confirmar si al usuario le gusta ese sitio.
- Probar a mano en Unity lo de la última ronda (menú de hoguera, cofres, estados) y ajustar números si hace falta.
- Faltan sprites de cristales y huesos para la cueva, y un sonido propio de máquina de escribir (se usa el "tic" de los menús).
- Faltan sonidos propios de compra y de logro (se usan `mejorar_equipo` y `objeto_obtenido`) y un icono de candado (dibujado por código).
- Optimización para itch.io: diagnóstico hecho, fase 2 aplazada (ver memoria).
- Cazadora: faltan sonidos propios de rugido y voz (van sustitutos); latido, cristal y escritura ya son los de `Sonidos_nuevos` (Ronda11) y probarla a mano para ajustar numeros.

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
