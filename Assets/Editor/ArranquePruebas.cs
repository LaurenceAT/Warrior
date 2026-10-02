using System.IO;
using UnityEditor;

// Las pruebas automaticas (linea de comandos) llaman a Proteger() antes de darle a
// Play: desde el primer fotograma todo se guarda en PruebaNieve/PartidasArranque
// (por ejemplo, el logro de llegar a una escena), nunca en el progreso real.
// Despues cada prueba pone su propia carpeta, como siempre.
public static class ArranquePruebas
{
    public static void Proteger()
    {
        string c = Path.Combine(Directory.GetCurrentDirectory(), "PruebaNieve", "PartidasArranque");
        if (Directory.Exists(c)) Directory.Delete(c, true);
        Directory.CreateDirectory(c);
        SessionState.SetString(Partida.ClaveCarpetaArranque, c);
    }
}
