# Une las capturas de antes y despues en una cuadricula 2x2 con etiquetas.
from PIL import Image, ImageDraw, ImageFont
import sys

def cuadricula(arriba_izq, arriba_der, abajo_izq, abajo_der, col1, col2, salida):
    imgs = [Image.open(p).convert("RGB").crop((0, 70, 1280, 640)) for p in (arriba_izq, arriba_der, abajo_izq, abajo_der)]
    w, h = imgs[0].size
    esc = 0.5
    w2, h2 = int(w * esc), int(h * esc)
    margen, cab, lado = 14, 46, 120
    lienzo = Image.new("RGB", (lado + 2 * w2 + 3 * margen, cab + 2 * h2 + 3 * margen), (247, 247, 245))
    d = ImageDraw.Draw(lienzo)
    f = ImageFont.truetype("C:/Windows/Fonts/segoeuib.ttf", 26)
    for i, im in enumerate(imgs):
        x = lado + margen + (i % 2) * (w2 + margen)
        y = cab + margen + (i // 2) * (h2 + margen)
        lienzo.paste(im.resize((w2, h2), Image.LANCZOS), (x, y))
    for j, t in enumerate((col1, col2)):
        cx = lado + margen + j * (w2 + margen) + w2 // 2
        d.text((cx, cab // 2 + 6), t, fill=(44, 44, 44), font=f, anchor="mm")
    for j, t in enumerate(("Básico", "Reforzado")):
        cy = cab + margen + j * (h2 + margen) + h2 // 2
        d.text((lado // 2 + 6, cy), t, fill=(44, 44, 44), font=f, anchor="mm")
    lienzo.save(salida)

base = "imagenes/"
cuadricula(base + "antes_h1.png", base + "antes_h2.png", base + "despues_h1.png", base + "despues_h2.png",
           "Posición inicial", "10 m a la derecha", "pdf/comparar_horizontal.png")
cuadricula(base + "antes_v1.png", base + "antes_v2.png", base + "despues_v1.png", base + "despues_v2.png",
           "Abajo", "6 m más arriba", "pdf/comparar_vertical.png")
print("ok")
