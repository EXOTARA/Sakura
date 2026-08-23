# Un modelo que hable como Sakura

Afinar un modelo abierto de 8B para que suene como Sakura, corra en el equipo de casa y se pueda
cargar en Ollama como un modelo más.

## Qué es esto y qué no es

**No acerca a Sakura a un modelo de frontera.** Afinar cambia el estilo y el comportamiento en un
dominio; no cambia cuánto razona un modelo. Un 8B afinado sigue siendo un 8B. Si alguien lee esto
esperando que Sakura pase a razonar como los modelos grandes, la respuesta está medida en la
conversación del 2026-08-23: entrenar algo así son ~10²⁵ operaciones, decenas de millones de dólares
y años de un equipo entero. Esto no es una versión pequeña de aquello — es otra cosa.

**Lo que sí da:** que la voz sea suya. Hoy, con un modelo genérico detrás, Sakura habla como el
modelo que le toque. Con esto habla como ella.

## Qué NO va a hacer este modelo

**No va a interpretar órdenes que tocan el sistema.** Eso ya lo hace `SpanishCommandLexicon` y sus
analizadores, que son deterministas: la misma frase da siempre la misma acción, y se puede probar.
Sustituirlos por un modelo de 8B cambiaría algo predecible por algo probable, justo en el camino que
borra archivos y cambia ajustes. Un «Sakura propia» que un día entiende mal «borra la carpeta» no es
una función nueva, es una regresión de seguridad.

El modelo se queda en la conversación abierta. Ahí es donde hoy suena a cualquiera.

## El modelo base

**Qwen3-8B**, 8.200 millones de parámetros, **Apache-2.0** (verificado en su ficha, no de memoria).
La licencia importa: la regla del proyecto es solo MIT/Apache/BSD, y SignPath exige que no haya
componentes propietarios. Llama y Gemma tienen licencias propias que no son OSI — sirven para probar
en casa, no para distribuir.

El equipo donde se entrena: RTX 5060 Ti, **16 GB de VRAM**, 32 GB de RAM. Da de sobra para QLoRA
sobre un 8B.

## La voz, medida

`mine_voice.py` saca de `src/` todas las frases que la aplicación le dice a alguien. **571 frases.**
No se inventa la personalidad: se lee de donde ya está escrita.

Lo que dicen los números:

| Rasgo | Medido |
|---|---|
| Largo medio | 9 palabras |
| Con signo de exclamación | **0 %** |
| Dice abiertamente que no pudo | 9 % |

Y lo que dicen las frases, que es más útil que los números:

> «Puse el texto en el portapapeles pero al releerlo había otra cosa, así que no lo doy por hecho.»
>
> «Esa acción no sé deshacerla desde aquí.»
>
> «Windows aceptó el cambio pero el plan activo sigue siendo otro.»
>
> «Falta la parte que solo puedes comprobar tú usándola.»

Cuatro rasgos que el conjunto de entrenamiento tiene que conservar:

1. **Nunca grita.** Cero exclamaciones en 571 frases no es casualidad, es carácter.
2. **Dice lo que no pudo, y por qué.** No se disculpa de más ni lo esconde.
3. **Separa lo que hizo de lo que cree.** «Lo puse, pero al releerlo había otra cosa» es una
   distinción que casi ningún asistente hace.
4. **Ofrece el paso siguiente cuando existe**, y se calla cuando no.

## Fases

- [x] **1. La voz.** `mine_voice.py` → `voice/phrases.jsonl` y `voice/summary.json`.
- [ ] **2. El conjunto de datos.** Pares instrucción→respuesta en esa voz, cubriendo lo que Sakura
      hace de verdad. Es el grueso del trabajo y de lo que depende el resultado.
- [ ] **3. El entorno.** PyTorch + PEFT/TRL. **Requiere instalar cosas** (ver más abajo).
- [ ] **4. Entrenar.** QLoRA sobre Qwen3-8B. Horas, no días.
- [ ] **5. Convertir y cargar.** Fundir el LoRA, cuantizar a GGUF, `ollama create sakura`.
- [ ] **6. Evaluar.** La fase que todo el mundo salta. Sin ella no se puede saber si mejoró o
      empeoró — y afinar puede empeorar.

## Lo que hace falta decidir antes de la fase 3

**Dónde se entrena.** En este equipo hay Python 3.13, controlador 610.74 y WSL 2 **sin ninguna
distribución instalada**. Entrenar necesita una de dos:

- **WSL + Ubuntu**, que es el camino normal y el que menos sorpresas da. Son varios GB de
  instalación y es un cambio en el equipo.
- **Windows nativo**, que evita instalar WSL pero es terreno más irregular para la cuantización de
  4 bits.

No se instala nada sin decirlo antes.

## Ejecutar lo que ya hay

```bash
python training/mine_voice.py
```

Deja `voice/phrases.jsonl` (una frase por línea, con el archivo de donde salió) y
`voice/summary.json` con los rasgos medibles. Volver a ejecutarlo tras tocar la interfaz es la forma
de comprobar que la voz sigue siendo la misma.
