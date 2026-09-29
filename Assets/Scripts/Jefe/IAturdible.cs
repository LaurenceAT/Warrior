// Enemigos que deciden ellos mismos cuando quedarse aturdidos (los jefes): el
// aturdimiento del sagrado se les pide y lo guardan para despues de su ataque,
// con su misma animacion de aturdido tras un parry. Devuelven false si ahora no
// puede ser (transformandose, agarrando, ya aturdidos...).
public interface IAturdible
{
    bool PedirAturdimiento(float segundos);
}
