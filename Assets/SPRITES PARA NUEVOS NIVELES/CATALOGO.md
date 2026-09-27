# Catálogo de sprites para niveles nuevos

Este archivo explica qué contiene cada carpeta de `SPRITES PARA NUEVOS NIVELES`, para saber exactamente qué es cada cosa sin tener que abrir cada carpeta. Todas las carpetas fueron renombradas con el formato `Categoria_Nombre` (sin espacios ni símbolos raros) para que sea fácil de referenciar en el código.

## BACKGROUND (fondos parallax para niveles)
- `Fondo_BosqueGloomwood` — fondo parallax pintado a mano, tema bosque tenebroso.
- `Fondo_CastilloHielo` — fondo parallax de un castillo de hielo.
- `Fondo_Cueva` — fondo parallax de cueva.
- `Fondo_Bosque` — fondo parallax de bosque (viene con dos variantes internas, v1 y v2).

## BOSSES (jefes de nivel)
- `Boss_Apostol` — jefe con arma de fuego a distancia: tiene animaciones de idle, recarga, disparo, modo francotirador y caminar.
- `Boss_BringerOfDeath` — jefe "Bringer of Death", incluye spritesheet e sprites individuales.
- `Boss_CrimsonWraith` — jefe "The Crimson Wraith".
- `Boss_Cthulhu` — jefe estilo Cthulhu.
- `Boss_Daddy` — jefe de ataque a distancia: animaciones de carga de ataque, grito, disparo y caminar.
- `Boss_DemonSlime` — jefe slime demoníaco.
- `Boss_HoodedKnight` — caballero encapuchado: idle, ataque, power-up (con y sin efectos) y transición correr-idle.
- `Boss_MartialHero` — jefe tipo "héroe marcial" (artes marciales).
- `Boss_Necromancer` — jefe nigromante.
- `Boss_Nightborne` — jefe "Nightborne".
- `Boss_Impaler` — jefe "Impaler": tiene 6 ataques distintos, animación de muerte, idle, caminata y sprites de preview. Incluye el archivo fuente `.aseprite` por si se necesita editar.
- `Boss_BlindHuntress` — jefe "The Blind Huntress" (serie SHADOW): sprite sheet con idle, correr, saltar, caer, 3 ataques (normal, dash, especial), golpe recibido y muerte. Trae dos tamaños de sprite distintos, revisa cuál encaja mejor con tu personaje.
- `Boss_ShadowedWetlands` — jefe "The Shadowed Wetlands Boss" (serie SHADOW, v1.1): sprite sheet con idle, caminar, 2 ataques normales y una secuencia de ataque especial en 3 partes (previo/medio/final), más golpe recibido y muerte.

## EFECTOS DE ATAQUE DE LOS ENEMIGOS (VFX que usan los enemigos al atacar)
- `Efecto_Portal` — animación de portal.
- `Efecto_Halloween` — pack de efectos temáticos de Halloween.
- `Efecto_MagoSangre` — VFX de un mago de sangre.
- `Efecto_ExplosionesFuego` — explosiones de fuego.
- `Efecto_MagoFuego` — VFX de un mago de fuego.
- `Efecto_Necromante` — VFX de nigromante.
- `Efecto_Sacerdote` — VFX de un sacerdote/clérigo.
- `Efecto_HumoPolvo` — efectos de humo y polvo.
- `Efecto_Starcaller` — VFX de un invocador de estrellas.
- `Efecto_Warlock` — VFX de brujo/warlock.

## EFECTOS DE ATAQUE DEL PLAYER (VFX para los ataques del personaje)
- `Efecto_Tajos` — animaciones de tajos/slashes de espada.
- `Efecto_Guerrero` — animaciones de ataque estilo guerrero.
- `Efecto_CaballeroHielo` — VFX de un caballero de hielo.
- `Efecto_Impactos` — efectos de impacto genéricos (para cuando un golpe conecta).
- `Efecto_ProyectilDisparo` — efectos de disparo/proyectil "warped". Nota: este pack también trae una carpeta `Music` con pistas de música que no tienen que ver con el VFX (es contenido extra del pack original); si más adelante quieres esa música, está dentro de esta carpeta.
- `Efecto_ImpactosGenericosPixelFX` — paquete grande de efectos pixel-art genéricos (explosiones, chispazos, impactos, magia), organizados en 15 subcarpetas "Part 1" a "Part 15" con sprites numerados. Son genéricos: sirven tanto para ataques del jugador como para ataques de enemigos, úsalos donde encajen mejor visualmente.

## EFECTOS DE ENTORNO (VFX ambientales, no de combate)
- `Efecto_EntornoPack16` — pack de efectos genéricos de entorno (organizado en subcarpetas numeradas 1-9, cada una es un efecto distinto).
- `Efecto_Sangre` — pack de efectos de sangre.

## ENEMIGOS (enemigos comunes, no jefes)
- `Enemigo_ArcaneArcher` — arquero arcano.
- `Enemigo_Cacodemonio` — cacodemonio.
- `Enemigo_EvilWizard2` — mago malvado (pack 2).
- `Enemigo_EvilWizard3` — mago malvado (pack 3).
- `Enemigo_MonsterPack1` — pack de criaturas/monstruos fantásticos (versión 1.3 del asset original).
- `Enemigo_MonsterPack2` — segundo pack de criaturas/monstruos fantásticos.

## HOGUERA O CHECKPOINT
- `Checkpoint_TorreLunaSangre` — torre/obelisco tipo checkpoint con animación de idle.

## ICONOS (íconos de interfaz / UI) — categoría nueva
- `Iconos_RPGPack` — paquete enorme (más de 1000 íconos) organizado en 16 subcarpetas por tipo: armas, armaduras, **pociones y consumibles** (dos subcarpetas distintas: `03-potions-consumables` y `10-consumables-potions`), gemas y materiales, joyería/accesorios, herramientas, hechizos de magia, botín/tesoro, comida, pesca, plantas/cultivos, materiales, objetos de mazmorra, efectos de estado y runas/encantamientos. **Importante:** aquí ya tienes íconos de pociones listos para usar en el sistema de curación tipo "Estus" que se implementó en el nivel de la cueva (antes no había sprite de poción disponible).

## MAPAS (tilesets/piezas de nivel)
- `Mapa_CuevaVegetacion` — tileset de cueva con vegetación (trae un set de cueva y otro de vegetación por separado).
- `Mapa_Cementerio` — tileset de cementerio.
- `Mapa_CuevaPixelFantasy` — tileset de cueva (fondos en capas, set principal de bloques/nivel y props), ideal para expandir el nivel de cueva actual.
- `Mapa_BosqueGandalf` — set de fondos y props de bosque/nieve/campamento: árboles (abedules, pinos, sauces), decoraciones de jardín, tiendas de campaña, antorchas, altar de alquimia, estatua de ángel, nubes, aves y una hoja de nieve/ventisca animada.

## SONIDOS
- `Sonidos_Efectos` — efectos de sonido sueltos (no música). Adentro:
  - `Sonidos_Espada` — 2 sonidos de arma: lanzamiento tipo shuriken y silbido de hoja (whoosh).
  - `Sonidos_Magia` — sonidos de hechizos organizados en 8 packs por elemento: ácido, oscuro, fuego, sagrado, hielo, rayo, agua y viento (cada uno en varias calidades de audio, ogg y wav).
  - `Sonidos_FantasyGeneral` — paquete general de sonidos de fantasía (pisadas en distintos materiales, ambientes de fondo, ataques de espada y arco), en formatos OGG y WAV.
  - `Sonidos_CombateEspada` — sonidos de combate cuerpo a cuerpo con espada: choques de espadas, estocadas, silbidos de golpe y gruñidos de esfuerzo/daño.
- `Sonidos_MusicaBosses` — música para peleas de jefes. Adentro:
  - `Sonidos_BossFightPack` — 10 pistas de música de pelea de jefe.
  - `Sonidos_BossBattlePlaylist2` — 6 pistas más de música de pelea (playlist 2).
  - `Sonidos_BossBattlePlaylist3` — 6 pistas más de música de pelea (playlist 3).
  - `Sonidos_BossThemesNuevos` — 6 pistas nuevas de música de jefe ("Boss Theme 1" a "6").

## TOTEM
- `Totem_ObeliscoVolador` — sprite de un obelisco/tótem flotante.

## TRAMPAS
- `Trampas_TrampasYArmas` — pack grande de trampas y elementos de nivel: picos, bombas, plataformas móviles/que caen, ventiladores, shurikens, lanza-flechas, plataformas de madera/ladrillo, y más (piezas nombradas + piezas numeradas 00-32 sin nombre específico, revísalas visualmente si necesitas una en particular).

---

**Nota sobre la limpieza:** se eliminaron archivos que no aportan nada al juego (cupones de descuento, notas de "gracias por tu compra", PDFs promocionales de "mira más productos", archivos de sistema de Mac como `.DS_Store` y `__MACOSX`, y gifs de vista previa usados solo para mostrar el producto en la tienda). Se conservaron los archivos de licencia (`License.txt`, `Contact.txt`, `public-license.pdf`, etc.) por si los necesitas para dar crédito al autor.

**Última actualización:** se agregaron 12 paquetes nuevos descargados por el usuario (3 jefes, 2 tilesets de mapa, 1 pack de efectos genéricos, 4 packs de sonido, 1 pack de música de jefe y 1 pack de íconos), y se creó la categoría `ICONOS` que no existía antes.
