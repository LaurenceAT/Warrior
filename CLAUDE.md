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
- `Assets/Scenes`: `Menu Principal`, `Nivel Nieve`, `Nivel Cueva` (en ese orden en Build Settings). `Cueva_Antigua` es la copia de la cueva de antes (fuera de la build).
- `Assets/Scripts/PlayerControler.cs`: todo el jugador (movimiento, espada, bloqueo/parry, daño, frascos, F para interactuar). Archivo con finales de línea CRLF.
- `GatherInput.cs`: teclas. Q frasco de sangre, R frasco de maná, F interactuar, E rueda de imbuir. El arco está apagado.
- `PlayerHud.cs`: barras de vida/estamina/maná, anillo de imbuición, barras de estados.
- `RPG/`: `Progreso` (niveles y almas), `AjustesProgreso` (números editables), `Equipo` (mejoras de espada y frascos), `EstadosPlayer`, `EstadosEnemigo`, `ArmaImbuida`, `MenuHoguera`, `RecursosRPG` (sonidos e iconos por clave).
- `Jefe/`: `JefeSombra`, `JefeWraith` (en 2 archivos: `JefeWraith` y `JefeWraithPoderes`), `PoseViva` (respira, se inclina y se estira: vida a las poses sueltas), `ArenaJefe` (combate, música, recompensa), `PeligrosJefe` (ataques en el escenario).
- `Desafios/`: `Desafio` (estado, inventario por aplicar y "Reiniciar desafío"), `ModoDesafio` (monta la arena en el nivel real), `TiendaTotem`/`TotemTienda`, `IndicadorInventario`, `MenuDesafios`, `MenuLogros`, `Logros`, `Globales` (logros, tiempos, estadísticas y descubrimientos en `globales.json`, aparte de las partidas), `Bitacora` (lo descubierto de jefes y enemigos), `DepuracionBitacora` (F7, solo Editor). Fichas editables en `Resources/Desafios` (jefes: ataques, fases, historia por fragmentos; y tienda) y `Resources/Logros`.
- `Bestiario/`: `FichaBestiario` (`Resources/Bestiario`: retrato, descripción, patrones y derrotas para cada enemigo; retratos en `Assets/Sprites/Bestiario`) y `MenuBestiario`. `Ronda15` genera fichas, iconos y Bestiario (no pisa lo editado).
- `Enemigos/`: enemigos normales. `EnemigoBase` (IA comun: estados con nombre, aceleracion, giro con retraso, alerta "!", separacion, vuelta a su zona, salto comun con tope, reaccion a golpes por rol, duerme lejos de la camara), `AjustesEnemigos` (curva por golpes en `Resources/AjustesEnemigos`), `DefinicionEnemigo` (una ficha por enemigo en `Assets/Data/Enemigos`), `DepuracionEnemigos` (F9, solo Editor). Variantes de color en `Prefabs/Enemies/Variantes` y elites en `Prefabs/Enemies/Elites` (sin colocar). `Ronda13` los genera; menu Warrior > Informe de dificultad.
- `Cazadora/`: el jefe secreto. `JefeCazadora` (cerebro, en 3 archivos: nucleo, Ataques, Especiales), `CuerpoCazadora` (movimiento con aceleracion), `AnimCazadora` (cuerpo + efecto tenible), `OidoCazadora` (ruidos del player), `ArenaCazadora` (dialogo, musica por fase, reinicio), `IlusionCazadora`, `EfectosCazadora` (olas, marcas, estelas... con reserva), `UICazadora`, `DialogoCazadora`, `OscuridadCazadora`, `AmbienteBosque`, `ParallaxBosque`, `CamaraCazadora`, `DepuracionCazadora` (F10, solo Editor). Numeros en `Assets/Data/Jefes/Ajustes Cazadora`; sonidos en `Assets/Data/Sonidos/Sonidos Jefe Cazadora`. `Ronda10` genera sprites, fondo pixelado, prefab y escena.
- `Menu/`: `MenuPrincipal`, `Partida` (partidas guardadas en JSON).
- `UI/`: `EstiloMenu` (estilo común y sonidos de menús; opción con estados resaltada / marcada / bloqueada), `CapasMenu` (navegación por capas, grupos marcados, transiciones, punto de "nuevo"), `MenuPausa`, `PantallaCarga`, `PantallaMuerte`, `AvisoObjeto`.
- `NivelNieve/`, `NivelCueva/`: piezas de cada nivel (hoguera, cofres, pared falsa, zonas ocultas, hielo...).
- Cueva (generada por `Editor/CrearCuevaNueva.cs`, Ronda14; no se pinta a mano): roca en Tilemaps de 16 px del pack `Mapa_CuevaPixelFantasy` a 28 px por unidad, `OscuridadCueva` + `ZonaOscura` + `FuenteLuz`, `AntorchaCueva` (F), `SueloFalso`, `GrietaMortal`, `DerrumbeAtajo`, `AmbienteCueva`, `GuiaBrasas`, `EmisorCueva` (particulas reutilizables), `DepuracionCueva` (F8, solo Editor). Plano y valores en `SPRITES PARA NUEVOS NIVELES/DISENO_CUEVA.md`.
- `Niveles/ConfigNivel.cs` + `Assets/Data/Niveles/Config Nivel *.asset`: tiles, parallax, música, sonidos y frases de cada nivel.
- `Niveles/EstatuaPista.cs` (estatuas con pista, F para leer) y `UI/CuadroPista.cs` (cuadro de texto con máquina de escribir).
- `RPG/SangreFx.cs` + `AjustesSangre` (sangre), `RPG/EfectoArma.cs` + `MapaHoja` (partículas del arma imbuida), `CaidaMuerte.cs` (muerte en el aire).
- `Assets/Resources/`: `AjustesProgreso.asset`, `RecursosRPG.asset`, `AjustesSangre.asset`, `MapaHoja.asset`.
- `Assets/Sprites/Tajos` (tajos recoloreados) y `Assets/Sprites/Sangre` (sangre en blanco): los genera `Ronda8`.
- `Assets/Editor/`: generadores y ajustes (`ActualizarProyecto`, `Ronda6`, `Ronda7`, `Ronda8`, `ConfigurarRecursosRPG`...) y pruebas automáticas (`PruebaNieve`, `PruebaBitacora`).
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
- Los desafíos no usan partida: personaje nuevo, nada se guarda salvo logros, tiempos, estadísticas y lo descubierto. Lo del cofre y el tótem va a un inventario del desafío y se aplica en la hoguera ("Aplicar mejoras"); se conserva al morir y se pierde al reiniciar. En la partida normal no cambia nada (piedras y lágrimas en la hoguera, frascos al momento).
- Menús por capas (Desafíos, Bestiario, Logros, Libro de pistas): pasar el ratón resalta, clic/Enter selecciona (marco dorado) y clic derecho/Esc vuelve un paso. En la pausa el clic derecho no se usa (es el bloqueo) y Esc sigue cerrándola si no hay submenú.
- Fichas de jefe (Bitácora, global): empiezan con imagen y nombre; ataques al verlos, consejos al morir por ellos o al vencerlo, fases al llegar (las no alcanzadas no se ven), elementos al golpearlo con cada uno, historia por fragmentos, estadísticas tras el primer intento. Cuenta también en la partida normal. Jefes completados antes de la Ronda 15 lo tienen todo (marca `bitacora_v1`). Sin avisos en combate: resumen al morir o completar el desafío.
- Bestiario (global): visto al detectarte o golpearlo, descripción y almas al derrotarlo, patrones tras N derrotas, elementos al golpearlo. Desde el menú principal y la pausa de la partida normal.
- "Reiniciar desafío" (pausa, con confirmación) recarga la escena del jefe: todo de cero con la misma dificultad; lo descubierto, tiempos y logros se quedan. El diálogo de la Cazadora se trata como un reintento salvo `dialogoAlReiniciar` en su ficha.
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
- Cueva: la que se juega siempre se llama "Nivel Cueva" (logro "Descenso", portal, desafios y partidas dependen del nombre). Para cambiar: Warrior > Cueva > Usar la cueva antigua / nueva. Sin trampas: grietas mortales, suelos falsos, paredes ilusorias (solo almas), atajo, zonas oscuras. Los triggers que deben detectar al player van en la capa Items.
- Enemigos normales por golpes (Ronda13): la vida y el dano salen de golpes para matar y % de tu vida esperada en su zona (zona 1 Nieve, 2 Cueva, 3 futura). Roles Debil/Comun/Pesado/Elite. Ajuste global en AjustesEnemigos. Los enemigos no chocan entre si (se separan solos). Los jefes heredan de EnemigoBase, pero con `EsJefe` (JefeBase) todo eso se apaga: el parry no les quita la armadura ni los reinicia. El tono de las variantes lo hace el shader `Sprites/Flash` (_Tono, _Saturacion, _Brillo).
- Se conservan en el código el arco, el combate sin arma y la rueda de pociones, aunque estén apagados.

## Hecho y funcionando
- Menú principal con partidas guardadas (nivel, hoguera, tiempo), opciones, controles y ambiente de fuego.
- Portales entre niveles con carga asíncrona, agarre de cornisa, pared falsa y zonas ocultas.
- Nieve pintable con Tile Palette (paletas "Nieve - Suelo/Decoracion"); la cueva se genera (ver Organización).
- Sistema RPG: almas, subir niveles en la hoguera, resistencias, imbuir la espada (5 elementos).
- Estados en jugador y enemigos, barras que crecen al subir de nivel, anillo de imbuición.
- Mejoras: Piedras de forja (espada) y Lágrimas sagradas (frascos), en cofres ocultos y como recompensa de jefes.
- Estatuas con pistas, libro de pistas, decoración extra en la Nieve (objeto `DecoracionExtra`).
- Menú de Desafíos (2 jefes, Normal/Difícil, tótem-tienda, cofre) y de Logros (9, ocultos). Prueba: `PruebaNieve.DesafioPrueba` y `DesafioDificilPrueba`.
- Ronda 15: navegación por capas, fichas de jefe desbloqueables, Reiniciar desafío, inventario del desafío y Bestiario. Pruebas: `PruebaBitacora.Menus`, `DesafioCompleto` (incluye 10 reinicios seguidos) y `Bestiario`.
- Enemigos normales con fichas, curva por golpes, salto comun, movimiento con estados, elites y variantes. Pruebas: `PruebaNieve.FichasPrueba`, `GolpesPrueba`, `SaltosPrueba`, `MovimientoPrueba`, `VariantesPrueba`.
- The Blind Huntress (jefe secreto, 3 barras, instakills, oscuridad, ilusiones). Prueba: `PruebaNieve.CazadoraPrueba`.
- Crimson Wraith con poderes nuevos y resistencias por fase; los jefes ya no se reinician con parry/sagrado/congelado. Pruebas: `WraithPrueba`, `JefesAturdirPrueba` / `JefesAturdirCuevaPrueba`.
- Cueva renovada (7 zonas, 3 grietas, 2 suelos falsos, 2 secretos de almas, atajo, 2 zonas oscuras con antorchas, élite Aldren colocado); se completa de principio a fin. Pruebas: `CuevaNuevaPrueba`, `RecorridoCuevaPrueba` (18 saltos clave con los controles), `EnemigosCuevaPrueba`, `VistasCuevaPrueba`.
- Tajos sincronizados con el ataque; colores propios; efectos en el arma; muerte en el aire; sangre.
- Pruebas automáticas en batchmode pasando (sistemas, pociones, jefe, cueva, menú, desafíos, Cazadora, ronda6, nivel, ronda8, portal).

## Pendiente
- Solo hay 3 Piedras y 2 Lágrimas en el juego (el máximo de mejoras es +5 cada una); faltan más cofres.
- El cofre de mejora de la cueva sigue junto al Mimic (ahora en la galería inferior, oscura): por confirmar si al usuario le gusta ese sitio.
- Probar a mano la cueva nueva (recorrido, oscuridad, suelos falsos) y ajustar tintes, bruma y radios de luz; decidir si se borra `Cueva_Antigua` cuando ya no haga falta.
- Probar a mano el Wraith renovado y la nueva dificultad de los enemigos (la Cueva es más dura) y ajustar números.
- Faltan sprites de cristales, huesos, agua o lava para la cueva y un sonido de goteo (la cueva nueva no los usa), y un sonido propio de máquina de escribir (se usa el "tic" de los menús).
- Faltan sonidos propios de compra, de logro y de "volver atrás" en los menús (se usan `mejorar_equipo`, `objeto_obtenido` y `menu_cancelar`). El candado ya es el del pack (`14-dungeon-objects/icon_12`).
- Colocar la élite de la Nieve (Morgath) y las variantes de color al rediseñar la Nieve (en la Cueva ya está Aldren).
- Optimización para itch.io: diagnóstico hecho, fase 2 aplazada (ver memoria).
- Cazadora: faltan sonidos propios de rugido y voz (van sustitutos); latido, cristal y escritura ya son los de `Sonidos_nuevos` (Ronda11) y probarla a mano para ajustar numeros.
- Revisar y ajustar los textos de las fichas de jefe (ataques, consejos, fases) y del Bestiario (borradores de la Ronda 15).
## Problemas conocidos
- La consola muestra avisos de `AnimadorHoja` (unos 33): por confirmar la causa.
- Avisos de compilación CS0114/CS0108: los jefes tapan `Update`/`LateUpdate`/`FixedUpdate`/`Dano` de `EnemigoBase` (sin efecto: para los jefes esa parte está apagada).
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
