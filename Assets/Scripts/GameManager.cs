using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
//texto
using TMPro;
//PAUSA
using UnityEngine.InputSystem;
//PASAR NIVEL
using UnityEngine.SceneManagement;

// Controlador global del juego (singleton): respawn del player, checkpoints,
// recolección de diamantes, pausa y cambio de nivel.
public class GameManager : MonoBehaviour
{
    // INSTANCIA GLOBAL DEL GAME MANAGER (PATRÓN SINGLETON)
    public static GameManager Instance;

    [Header("Player Settings")]
    // REFERENCIAS Y CONFIGURACIÓN DEL RESPAWN DEL JUGADOR
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerRespawnPoint;
    [SerializeField] private float respawnPlayerDelay;
    [SerializeField] private PlayerControler playerControler;
    public PlayerControler PlayerControler { get => playerControler; }

    //PAUSA
    [Header("Pause Settings")]
    [SerializeField] private GameObject pausePanel;
    private bool isPaused;
    public bool IsPaused => isPaused;

    [Header("Configuracion del Respawn")]
    // VARIABLES PARA EL SISTEMA DE CHECKPOINTS
    public bool hasCheckPointActive;
    public Vector3 checkpointRespawnPosition;

    // REFERENCIA A LA CÁMARA DE CINEMACHINE
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("Diamond Manager")]
    // VARIABLES PARA EL SISTEMA DE RECOLECCIÓN DE DIAMANTES
    [SerializeField] private int _diamondCollected;
    public int DiamondCollected { get => _diamondCollected; }
    [SerializeField] private int totalDiamonds;
    public int TotalDiamonds { get => totalDiamonds; }
    // True cuando ya se recogieron todos los diamantes del nivel (requisito para salir).
    public bool AllDiamondsCollected => _diamondCollected >= totalDiamonds;
    [Header("Diamond UI")]
    [SerializeField] private TMP_Text diamondsText;

    #region Unity Lifecycle

    // Inicializa el singleton y obtiene la referencia a la cámara de Cinemachine.
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
    }

    // Calcula el total de diamantes de la escena y actualiza la UI.
    private void Start()
    {
        // Solo cuenta diamantes activos: desactivar uno en el Inspector lo saca del total.
        Diamond[] diamonds = FindObjectsByType<Diamond>(FindObjectsSortMode.None);
        totalDiamonds = diamonds.Length;

        UpdateDiamondUI();
    }

    // Escucha la tecla Escape para pausar/reanudar el juego.
    private void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    #endregion

    #region Respawn del Player

    // Inicia el proceso de reaparición del jugador, usando el checkpoint activo si existe.
    public void RespawnPlayer()
    {
        // SI HAY UN CHECKPOINT ACTIVO, ACTUALIZA EL PUNTO DE REAPARICIÓN
        if (hasCheckPointActive)
            playerRespawnPoint.position = checkpointRespawnPosition;

        StartCoroutine(RespawnPlayerCoroutine());
    }

    // Genera un nuevo player tras un tiempo de espera y actualiza las referencias (cámara incluida).
    IEnumerator RespawnPlayerCoroutine()
    {
        yield return new WaitForSeconds(respawnPlayerDelay);

        GameObject newPlayer = Instantiate(playerPrefab, playerRespawnPoint.position, Quaternion.identity);
        newPlayer.name = "Player";

        Debug.Log("Respawn en: " + playerRespawnPoint.position);

        // ACTUALIZA LA REFERENCIA DEL PLAYER ACTUAL
        playerControler = newPlayer.GetComponent<PlayerControler>();

        // ACTUALIZA EL OBJETIVO DE LA CÁMARA AL NUEVO PLAYER
        cinemachineCamera.Follow = newPlayer.transform;
    }

    #endregion

    #region Diamantes

    // Aumenta la cantidad de diamantes recolectados y refresca la UI.
    public void AddDiamond()
    {
        _diamondCollected++;
        UpdateDiamondUI();
    }

    // Actualiza el texto de la UI con el progreso de diamantes recolectados.
    private void UpdateDiamondUI()
    {
        diamondsText.text = $"{_diamondCollected} / {totalDiamonds}";
    }

    #endregion

    #region Pausa

    // Detiene el tiempo del juego y muestra el panel de pausa.
    public void PauseGame()
    {
        Time.timeScale = 0f;
        isPaused = true;

        pausePanel.SetActive(true);
    }

    // Reanuda el tiempo del juego y oculta el panel de pausa.
    public void ResumeGame()
    {
        Time.timeScale = 1f;
        isPaused = false;

        pausePanel.SetActive(false);
    }

    #endregion

    #region Nivel

    // Carga la siguiente escena en el build; si no hay más niveles, vuelve al menú principal.
    public void LoadNextLevel()
    {
        int currentScene = SceneManager.GetActiveScene().buildIndex;
        int nextScene = currentScene + 1;

        if (nextScene < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextScene);
        }
        else
        {
            // Si no hay más niveles, vuelve al menú principal.
            SceneManager.LoadScene(0);
        }
    }

    #endregion
}
