"""
Extrae la voz que Sakura YA tiene, de su propio código.

Antes de escribir un solo ejemplo de entrenamiento hay que contestar a una pregunta: ¿cómo habla
Sakura? Y esa pregunta ya tiene respuesta escrita — en los cientos de frases que la aplicación le
dice a alguien cuando algo va bien, cuando algo falla, o cuando hace falta confirmar.

Inventarse esa voz sería empezar por el final: el modelo acabaría hablando como el que escribió el
conjunto de datos, no como la aplicación. Así que se saca de donde está.

No decide nada ni genera ejemplos: solo junta la evidencia y la deja ordenada para leerla.

    python training/mine_voice.py
"""

import io
import json
import os
import re
import sys
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE = os.path.join(ROOT, "src")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "voice")

# Cadenas de C# con al menos algo de acento o puntuación española. Es un filtro tosco a propósito:
# un identificador o una ruta rara vez lleva tilde, y una frase dirigida a alguien casi siempre sí.
SPANISH = re.compile(r'"((?:[^"\\]|\\.){12,240})"')
HAS_SPANISH = re.compile(r"[áéíóúñÁÉÍÓÚÑ¿¡]")

# Lo que NO es voz: rutas, claves de recurso, formatos, consultas.
NOISE = re.compile(
    r"^(https?://|[A-Za-z]:\\|\\\\|SELECT |[\w./\\-]+\.(cs|xaml|json|md|exe|dll|png|ico)$)"
    r"|^\{|^[A-Z_]{4,}$|^[a-z]+\.[a-z]+$",
    re.IGNORECASE,
)


# Una frase dicha a alguien empieza como una frase y termina como una frase. Este par de anclas
# quita casi toda la basura que colaba la primera versión: continuaciones de una cadena partida en
# dos líneas, trozos de código con acentos en un comentario, y plantillas sueltas.
STARTS = re.compile(r"^[A-ZÁÉÍÓÚÑ¿¡«]")
ENDS = re.compile(r"[.?!…»]$")

# Interpolación de C#: «{algo.Propiedad}» es un hueco de dato, no una palabra.
PLACEHOLDER = re.compile(r"\{[A-Za-z_][\w.()\[\]]*\}")


def is_voice(text: str) -> bool:
    """Una frase dicha a una persona, no un dato."""
    text = text.strip()

    if not HAS_SPANISH.search(text):
        return False
    if NOISE.search(text):
        return False
    if not STARTS.match(text) or not ENDS.search(text):
        return False

    # Saltos y tabuladores escritos como escape son formato, no habla.
    if "\n" in text or "\t" in text:
        return False

    # Una frase con más hueco que palabra es una plantilla.
    if len(PLACEHOLDER.findall(text)) > 2:
        return False

    return text.count(" ") >= 3


def collect() -> list[dict]:
    found: list[dict] = []

    for folder, _, files in os.walk(SOURCE):
        if os.sep + "bin" in folder or os.sep + "obj" in folder:
            continue

        for name in files:
            if not name.endswith(".cs"):
                continue

            path = os.path.join(folder, name)
            try:
                text = io.open(path, encoding="utf-8").read()
            except (UnicodeDecodeError, OSError):
                continue

            for match in SPANISH.finditer(text):
                phrase = match.group(1).replace('\\"', '"').strip()
                if is_voice(phrase):
                    found.append(
                        {
                            "text": phrase,
                            "file": os.path.relpath(path, ROOT).replace("\\", "/"),
                        }
                    )

    return found


def summarise(phrases: list[dict]) -> dict:
    """Unos cuantos rasgos medibles del tono, para poder comprobarlo después."""
    texts = [p["text"] for p in phrases]

    def share(predicate) -> float:
        return round(100 * sum(1 for t in texts if predicate(t)) / max(1, len(texts)), 1)

    words = Counter(
        word.lower()
        for text in texts
        for word in re.findall(r"[a-záéíóúñ]{4,}", text.lower())
    )

    return {
        "frases": len(texts),
        "largo_medio_en_palabras": round(
            sum(len(t.split()) for t in texts) / max(1, len(texts)), 1
        ),
        "porcentaje_que_tutea": share(lambda t: re.search(r"\b(tu|tú|tienes|puedes|quieres|te)\b", t, re.I) is not None),
        "porcentaje_con_pregunta": share(lambda t: "?" in t),
        "porcentaje_que_dice_no_puedo_o_no_encontre": share(
            lambda t: re.search(r"no (pude|puedo|encontré|hay)", t, re.I) is not None
        ),
        "porcentaje_con_exclamacion": share(lambda t: "!" in t),
        "palabras_mas_usadas": words.most_common(25),
    }


def main() -> int:
    if not os.path.isdir(SOURCE):
        print("No encuentro src/ — ejecútalo desde el repositorio.", file=sys.stderr)
        return 1

    phrases = collect()
    phrases.sort(key=lambda p: (p["file"], p["text"]))

    os.makedirs(OUT, exist_ok=True)

    with io.open(os.path.join(OUT, "phrases.jsonl"), "w", encoding="utf-8", newline="\n") as handle:
        for phrase in phrases:
            handle.write(json.dumps(phrase, ensure_ascii=False) + "\n")

    summary = summarise(phrases)
    with io.open(os.path.join(OUT, "summary.json"), "w", encoding="utf-8", newline="\n") as handle:
        json.dump(summary, handle, ensure_ascii=False, indent=2)

    print("frases recogidas:", summary["frases"])
    print("largo medio:", summary["largo_medio_en_palabras"], "palabras")
    print("tutea:", summary["porcentaje_que_tutea"], "%")
    print("con exclamación:", summary["porcentaje_con_exclamacion"], "%")
    print("dice que no pudo:", summary["porcentaje_que_dice_no_puedo_o_no_encontre"], "%")
    print("\nsalida en:", os.path.relpath(OUT, ROOT))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
