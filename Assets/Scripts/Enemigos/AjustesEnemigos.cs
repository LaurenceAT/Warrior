using System;
using UnityEngine;

// Papel del enemigo en el combate: decide cuantos golpes aguanta, cuanto quita,
// su tamano, cuanto se deja aturdir y las almas.
public enum RolEnemigo { Debil = 0, Comun = 1, Pesado = 2, Elite = 3 }

// Como le sienta un elemento. La magnitud la pone su zona (en la 1 no hay
// resistencias; en la 3 son marcadas).
public enum Afinidad { Normal = 0, Debil = 1, Resiste = 2, Inmune = 3 }

// La curva de dificultad de los enemigos normales (no los jefes), en
// Resources/AjustesEnemigos. Se disena por GOLPES: cada enemigo dice cuantos
// golpes aguanta y que parte de tu vida quita, y la vida y el dano reales se
// calculan con el poder que se espera que tengas en su zona (nivel de la espada
// y niveles de vida). Si cambias la espada o la vida del player en
// AjustesProgreso, todo se recalcula solo al darle a Play.
[CreateAssetMenu(menuName = "Warrior/Ajustes de enemigos", fileName = "AjustesEnemigos")]
public class AjustesEnemigos : ScriptableObject
{
    [Header("Ajuste global (para afinar rapido tras probar)")]
    [Tooltip("Multiplica la vida de todos los enemigos normales.")]
    public float multiplicadorVida = 1f;
    [Tooltip("Multiplica el dano de todos los enemigos normales.")]
    public float multiplicadorDano = 1f;
    [Tooltip("Multiplica las almas que sueltan.")]
    public float multiplicadorAlmas = 1f;

    [Header("El player")]
    [Tooltip("Dano medio de un golpe de espada sin mejorar (media del combo en el suelo). " +
             "El informe de dificultad lo lee del prefab del player.")]
    public float danoGolpeJugador = 17.3f;
    [Tooltip("Alto visible del player en unidades (para los tamanos).")]
    public float alturaJugador = 1.04f;

    [Tooltip("Zona 1 = Nieve, zona 2 = Cueva, zona 3 = la siguiente (sin enemigos aun).")]
    public Zona[] zonas = { Zona.Nieve(), Zona.Cueva(), Zona.Tercera() };

    [Tooltip("Debil, Comun, Pesado y Elite, en ese orden.")]
    public Rol[] roles = { Rol.Debil(), Rol.Comun(), Rol.Pesado(), Rol.Elite() };

    [Header("Elites")]
    [Tooltip("Nombres propios para los elites (se elige uno si su ficha no trae el suyo).")]
    public string[] nombresElite =
    {
        "Vharok, el Desollado", "La Viuda de Ceniza", "Morgath Sin Ojos", "Sor Esquirla",
        "El Ultimo Centinela", "Ghul de la Brecha", "Aldren, Hueso Roto", "La Madre Carmesi",
    };

    [Serializable]
    public class Zona
    {
        public string nombre = "Zona";
        [Header("Poder esperado del player en esta zona")]
        [Tooltip("Mejoras de la espada (Piedras de forja) que deberia tener.")]
        public int nivelEspada;
        [Tooltip("Niveles subidos en Vida que deberia tener.")]
        public int nivelesVida;

        [Header("Golpes para matar (minimo y maximo aceptables; se usa el punto medio)")]
        public Vector2 golpesDebil = new Vector2(2, 2);
        public Vector2 golpesComun = new Vector2(3, 3);
        public Vector2 golpesPesado = new Vector2(5, 6);
        public Vector2 golpesElite = new Vector2(10, 12);

        [Header("Dano de su golpe principal (% de tu vida maxima esperada)")]
        public float danoDebil = 6f;
        public float danoComun = 10f;
        public float danoPesado = 20f;
        public float danoElite = 25f;

        [Header("Almas")]
        public int almasDebil = 30;
        public int almasComun = 55;
        public int almasPesado = 180;
        public int almasElite = 450;

        [Header("Elementos")]
        [Tooltip("Multiplicador contra lo que es debil.")]
        public float debilidad = 1.25f;
        [Tooltip("Multiplicador contra lo que resiste (1 = no resiste; 0.5 o menos tambien bloquea el estado).")]
        public float resistencia = 1f;
        [Tooltip("Velocidad a la que se le acumulan los estados (sangrado, congelacion, aturdimiento sagrado).")]
        [Range(0.2f, 1f)] public float acumulacionEstados = 1f;

        public float VidaJugador => AjustesProgreso.Get().vidaBase + AjustesProgreso.Get().vidaPorNivel * Mathf.Max(0, nivelesVida);
        public float DanoJugador => Get().danoGolpeJugador * Equipo.MultiplicadorEspadaEn(nivelEspada);

        public Vector2 Golpes(RolEnemigo r) =>
            r == RolEnemigo.Debil ? golpesDebil : r == RolEnemigo.Comun ? golpesComun : r == RolEnemigo.Pesado ? golpesPesado : golpesElite;
        public float Dano(RolEnemigo r) =>
            r == RolEnemigo.Debil ? danoDebil : r == RolEnemigo.Comun ? danoComun : r == RolEnemigo.Pesado ? danoPesado : danoElite;
        public int Almas(RolEnemigo r) =>
            r == RolEnemigo.Debil ? almasDebil : r == RolEnemigo.Comun ? almasComun : r == RolEnemigo.Pesado ? almasPesado : almasElite;

        public static Zona Nieve() => new Zona { nombre = "Nieve", nivelEspada = 0, nivelesVida = 1 };

        public static Zona Cueva() => new Zona
        {
            nombre = "Cueva", nivelEspada = 1, nivelesVida = 4,
            golpesDebil = new Vector2(3, 3), golpesComun = new Vector2(4, 4), golpesPesado = new Vector2(7, 8), golpesElite = new Vector2(15, 15),
            danoDebil = 8f, danoComun = 14f, danoPesado = 26f, danoElite = 35f,
            almasDebil = 60, almasComun = 110, almasPesado = 320, almasElite = 800,
            debilidad = 1.25f, resistencia = 0.75f, acumulacionEstados = 0.8f,
        };

        public static Zona Tercera() => new Zona
        {
            nombre = "Zona 3", nivelEspada = 3, nivelesVida = 7,
            golpesDebil = new Vector2(3, 4), golpesComun = new Vector2(5, 5), golpesPesado = new Vector2(10, 10), golpesElite = new Vector2(20, 20),
            danoDebil = 10f, danoComun = 18f, danoPesado = 32f, danoElite = 45f,
            almasDebil = 100, almasComun = 180, almasPesado = 500, almasElite = 1300,
            debilidad = 1.5f, resistencia = 0.5f, acumulacionEstados = 0.6f,
        };
    }

    [Serializable]
    public class Rol
    {
        public string nombre = "Rol";
        [Tooltip("Alto aceptable respecto al player (1 = igual de alto). Lo usa el informe.")]
        public Vector2 tamano = new Vector2(0.9f, 1.2f);
        [Tooltip("Golpes seguidos que hacen falta para interrumpirlo (1 = cada golpe lo para).")]
        [Min(1)] public int golpesParaAturdir = 1;
        [Tooltip("Tras aturdirlo, segundos en los que los golpes ya no lo interrumpen (sin aturdimiento infinito).")]
        public float esperaAturdir = 0.8f;
        [Tooltip("Cuanto lo empujan los golpes (1 = normal).")]
        public float retroceso = 1f;

        public static Rol Debil() => new Rol { nombre = "Debil", tamano = new Vector2(0.65f, 0.95f), golpesParaAturdir = 1, esperaAturdir = 0.5f, retroceso = 1.2f };
        public static Rol Comun() => new Rol { nombre = "Comun", tamano = new Vector2(0.9f, 1.2f), golpesParaAturdir = 1, esperaAturdir = 0.8f, retroceso = 1f };
        public static Rol Pesado() => new Rol { nombre = "Pesado", tamano = new Vector2(1.25f, 1.6f), golpesParaAturdir = 3, esperaAturdir = 1.5f, retroceso = 0.45f };
        public static Rol Elite() => new Rol { nombre = "Elite", tamano = new Vector2(1.3f, 1.8f), golpesParaAturdir = 4, esperaAturdir = 2f, retroceso = 0.3f };
    }

    public Zona ZonaN(int n) => zonas != null && zonas.Length > 0 ? zonas[Mathf.Clamp(n - 1, 0, zonas.Length - 1)] : Zona.Nieve();
    public Rol RolDe(RolEnemigo r) => roles != null && roles.Length > (int)r ? roles[(int)r] : Rol.Comun();

    private static AjustesEnemigos cache;
    public static AjustesEnemigos Get()
    {
        if (cache == null) cache = Resources.Load<AjustesEnemigos>("AjustesEnemigos");
        if (cache == null) cache = CreateInstance<AjustesEnemigos>();
        return cache;
    }
}
