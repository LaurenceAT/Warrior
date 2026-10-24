#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

// Sala de pruebas (SOLO Editor): lo que se puede invocar en la sala. Para anadir
// un jefe o un enemigo basta con una entrada nueva aqui (Assets/Data/SalaPruebas).
//   - Enemigo: nombre + prefab, arena "Ninguna".
//   - Jefe con la arena de siempre (ArenaJefe): arena "Generica" y su barra,
//     musica y fase 2 (se copian de su escena al generar el catalogo).
//   - La Cazadora: arena "Cazadora" con su ficha de ajustes.
public class CatalogoPruebas : ScriptableObject
{
    public enum TipoArena { Ninguna, Generica, Cazadora }

    [Serializable]
    public class Entrada
    {
        public string nombre;
        public GameObject prefab;
        [Tooltip("Ninguna = enemigo normal (aparece suelto). Generica = jefe con ArenaJefe. Cazadora = ArenaCazadora.")]
        public TipoArena arena = TipoArena.Ninguna;

        [Header("Arena generica (ArenaJefe)")]
        public string titulo;
        [Tooltip("Musica del jefe (si la musica de la sala esta encendida en Ajustes Sala Pruebas).")]
        public ConfigNivel config;
        [Range(0f, 1f)] public float marcaFase = 0.5f;
        public bool barraPorFase;
        public Color colorBarraFase2 = new Color(0.25f, 0.6f, 0.95f, 1f);
        public string nombreFase2;
        public Color luzFase2 = new Color(1f, 0.55f, 0.55f, 1f);
        public string textoFase2;
        public Color particulasFase2A = new Color(1f, 0.3f, 0.2f, 0.9f);
        public Color particulasFase2B = new Color(1f, 0.7f, 0.3f, 0.7f);
        public bool particulasCaen;
        public string bannerVictoria = "ENEMIGO CAÍDO";

        [Header("Cazadora (ArenaCazadora)")]
        public AjustesCazadora ajustesCazadora;

        [Header("Pruebas")]
        [Tooltip("Vida (fraccion de la barra) a la que se baja para pasar a la fase 2. 0 = vaciar la barra. " +
                 "Si el jefe sabe saltar de fase solo (la Cazadora), se usa eso.")]
        [Range(0f, 1f)] public float vidaFase2;
        [Tooltip("Numeros de Dificil (multVida, multDano, multVelocidad). Vacio = los de Ajustes Sala Pruebas.")]
        public FichaJefe fichaDesafio;
        [Tooltip("Sus ataques suponen suelo plano: al elegirlo sale el aviso de abajo.")]
        public bool asumeSueloPlano;
        [TextArea(1, 3)] public string avisoTerreno;

        public bool EsJefe => arena != TipoArena.Ninguna;
    }

    public List<Entrada> entradas = new List<Entrada>();
}
#endif
