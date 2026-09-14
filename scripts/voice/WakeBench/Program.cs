// Banco de medición de «Oye Sakura».
//
// Alimenta grabaciones (cualquier formato que lea ffmpeg) al mismo modelo Vosk, con la misma
// gramática y el mismo WakeWordTextMatcher que usa Sakura, en trozos de 100 ms como WaveInEvent, y
// cuenta los despertares con varias estrategias lado a lado. No está en Nexo.slnx: es una
// herramienta de medición, no parte del producto.
//
//   dotnet run -c Release -- grabacion1.m4a grabacion2.wav      (SENS=Strict|Balanced|High, PHRASE=OyeSakura|Sakura|HeySakura)
//   dotnet run -c Release -- --tts "Oye Sakura, abre la calculadora." salida.wav
//
// Estrategias:
//   app            lo que hace hoy VoskWakeWordService: gramática cerrada, parciales y finales.
//   finales        igual, pero solo con resultados finales.
//   libre          sin gramática; se busca la frase dentro de lo que se transcribe.
//   libre-finales  igual, solo finales.
//   hibrido        la gramática propone y el reconocedor libre confirma que el nombre suena.
//   confusores     la gramática del producto desde D88 (WakeWordGrammarConfusers); CONFUSORES=a,b,c prueba otra lista.
//   confianza      confianza por palabra de Vosk en los finales (no discrimina: 1,00 también en trampas).
//   cascada-*      Vosk propone y Whisper base confirma sobre 2,5 s (VERIFY_PROMPT=... para darle pista).
//                  Descartada: con clips cortos Whisper oye «oye, esa cura»; 3/12 cerca y 0-3/16 a
//                  distancia, y ~1,3 s por comprobación en CPU.
//
// Por qué existe (2026-09-13): con voz sintética, «Voy a sacar la basura», «Oye, saca la ropa»,
// «Oye, ¿sabes a qué hora cierra?» u «Oye, se acabó el café» despiertan a Sakura con la estrategia
// actual —la gramática cerrada obliga a Vosk a escribir la frase más parecida de su lista, y esa es
// «oye sakura»—, también en sensibilidad Precisa. 9 falsos despertares en 15 frases trampa; el
// híbrido, 0, sin perder ninguno de los 5 aciertos. Una sola voz sintética no dice nada del alcance a
// dos metros: eso solo se sabe con grabaciones de verdad.
using System.Diagnostics;
using System.Text.Json;
using Nexo.Core.Voice;
using Vosk;
using Whisper.net;

var modelDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Sakura", "models", "Vosk", "vosk-model-small-es-0.42");
if (args.Length > 0 && args[0] == "--tts") { Synth(args[1], args[2]); return; }

var sensitivity = Enum.Parse<WakeWordSensitivity>(Environment.GetEnvironmentVariable("SENS") ?? "Balanced");
var phrase = Enum.Parse<WakeWordPhrase>(Environment.GetEnvironmentVariable("PHRASE") ?? "OyeSakura");
Vosk.Vosk.SetLogLevel(-1);
using var model = new Model(modelDir);
var whisperPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sakura", "models", "ggml-base.bin");
using var whisper = WhisperFactory.FromPath(whisperPath);
var verifyPrompt = Environment.GetEnvironmentVariable("VERIFY_PROMPT");
var grammar = JsonSerializer.Serialize(WakeWordTextMatcher.GetGrammarPhrases(phrase, sensitivity).Append("[unk]").ToArray());

foreach (var file in args)
{
    var pcm = ToPcm16kMono(file);
    Console.WriteLine($"\n=== {Path.GetFileName(file)}  ({pcm.Length / 32000.0:0.0} s, sensibilidad {sensitivity})");
    foreach (var mode in new[] { "app", "finales", "libre", "libre-finales" })
    {
        var detections = new List<(double t, string text)>();
        using var rec = mode.StartsWith("libre") ? new VoskRecognizer(model, 16000f) : new VoskRecognizer(model, 16000f, grammar);
        var finalsOnly = mode.EndsWith("finales");
        const int chunk = 3200; double skipUntil = -1;
        for (int off = 0; off < pcm.Length; off += chunk)
        {
            var n = Math.Min(chunk, pcm.Length - off); var t = off / 32000.0;
            if (t < skipUntil) continue;
            var buf = pcm.AsSpan(off, n).ToArray();
            var fin = rec.AcceptWaveform(buf, n);
            if (finalsOnly && !fin) continue;
            var text = Read(fin ? rec.Result() : rec.PartialResult(), fin ? "text" : "partial");
            if (Wakes(text, phrase, sensitivity, mode.StartsWith("libre")))
            { detections.Add((t, text)); rec.Reset(); skipUntil = t + 2.5; }
        }
        if (finalsOnly) { var text = Read(rec.FinalResult(), "text"); if (Wakes(text, phrase, sensitivity, mode.StartsWith("libre"))) detections.Add((pcm.Length / 32000.0, text)); }
        Console.WriteLine($"[{mode,-13}] despertares: {detections.Count}  " + string.Join(" | ", detections.Select(d => $"{d.t:0.0}s «{d.text}»")));
    }
    {
        // Híbrido: la gramática propone, el reconocedor libre confirma que el nombre suena de verdad.
        var names = new HashSet<string> { "sakura", "sacura", "zacura", "sakuro", "sakuras", "saqura", "sagura" };
        using var g = new VoskRecognizer(model, 16000f, grammar);
        using var f = new VoskRecognizer(model, 16000f);
        var detections = new List<(double t, string text)>(); double skipUntil = -1, pendingSince = -1; int heard = 0, consumed = 0;
        var sw = Stopwatch.StartNew();
        for (int off = 0; off < pcm.Length; off += 3200)
        {
            var n = Math.Min(3200, pcm.Length - off); var t = off / 32000.0; var buf = pcm.AsSpan(off, n).ToArray();
            var ff = f.AcceptWaveform(buf, n);
            var ft = Read(ff ? f.Result() : f.PartialResult(), ff ? "text" : "partial");
            // Cuenta cuántas veces suena el nombre en el enunciado en curso; solo una aparición nueva confirma.
            var count = WakeWordTextMatcher.Normalize(ft).Split(' ').Count(names.Contains);
            heard = Math.Max(heard, count);
            if (ff) { heard -= consumed; consumed = 0; if (heard < 0) heard = 0; }
            if (t < skipUntil) continue;
            var gf = g.AcceptWaveform(buf, n);
            var gt = Read(gf ? g.Result() : g.PartialResult(), gf ? "text" : "partial");
            if (WakeWordTextMatcher.Evaluate(gt, phrase, sensitivity).IsMatch && pendingSince < 0) pendingSince = t;
            if (pendingSince >= 0 && heard > consumed) { detections.Add((t, gt)); g.Reset(); skipUntil = t + 2.5; pendingSince = -1; consumed++; }
            else if (pendingSince >= 0 && t - pendingSince > 1.5) { pendingSince = -1; }
        }
        Console.WriteLine($"[{"hibrido",-13}] despertares: {detections.Count}  " + string.Join(" | ", detections.Select(d => $"{d.t:0.0}s")) + $"  (cómputo {sw.ElapsedMilliseconds} ms para {pcm.Length / 32000.0:0.0} s)");
    }
    {
        // Confusores: la gramática ofrece también las palabras con las que «sakura» se confunde, para
        // que Vosk tenga dónde escribir «saca» en vez de verse obligado a escribir «sakura».
        var confusers = Environment.GetEnvironmentVariable("CONFUSORES")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? WakeWordGrammarConfusers.For(phrase).ToArray();
        var cg = JsonSerializer.Serialize(WakeWordTextMatcher.GetGrammarPhrases(phrase, sensitivity).Concat(confusers).Append("[unk]").ToArray());
        using var rec = new VoskRecognizer(model, 16000f, cg);
        rec.SetWords(true); rec.SetPartialWords(true);
        var detections = new List<(double t, string text, double conf)>(); double skipUntil = -1;
        for (int off = 0; off < pcm.Length; off += 3200)
        {
            var n = Math.Min(3200, pcm.Length - off); var t = off / 32000.0;
            if (t < skipUntil) continue;
            var buf = pcm.AsSpan(off, n).ToArray();
            var fin = rec.AcceptWaveform(buf, n);
            var json = fin ? rec.Result() : rec.PartialResult();
            var text = Read(json, fin ? "text" : "partial");
            if (WakeWordTextMatcher.Evaluate(text, phrase, sensitivity).IsMatch)
            { detections.Add((t, text, NameConfidence(json, fin))); rec.Reset(); skipUntil = t + 2.5; }
        }
        // Al final de un archivo no llega el silencio que cerraría el enunciado: se pide a mano.
        { var json = rec.FinalResult(); var text = Read(json, "text"); if (pcm.Length / 32000.0 >= skipUntil && WakeWordTextMatcher.Evaluate(text, phrase, sensitivity).IsMatch) detections.Add((pcm.Length / 32000.0, text, NameConfidence(json, true))); }
        Console.WriteLine($"[{"confusores",-13}] despertares: {detections.Count}  " + string.Join(" | ", detections.Select(d => $"{d.t:0.0}s" + (d.conf >= 0 ? $" c{d.conf:0.00}" : ""))));
    }
    {
        // Confianza: la estrategia actual, pero se lee la confianza por palabra del resultado final.
        using var rec = new VoskRecognizer(model, 16000f, grammar);
        rec.SetWords(true);
        var confs = new List<string>();
        for (int off = 0; off < pcm.Length; off += 3200)
        {
            var n = Math.Min(3200, pcm.Length - off); var buf = pcm.AsSpan(off, n).ToArray();
            if (rec.AcceptWaveform(buf, n)) { var c = NameConfidence(rec.Result(), true); if (c >= 0) confs.Add($"{off / 32000.0:0.0}s c{c:0.00}"); }
        }
        Console.WriteLine($"[{"confianza",-13}] finales con el nombre: {confs.Count}  " + string.Join(" | ", confs));
    }
    foreach (var source in new[] { "app", "confusores" })
    {
        // Cascada: Vosk propone (con la gramática de siempre o con confusores) y Whisper, sobre los
        // 2,5 s de audio que acaban en la propuesta, confirma que se dijo la frase. Sin prompt que
        // empuje a Whisper hacia «Sakura»: un verificador sesgado hacia la respuesta no verifica nada.
        var forms = WakeWordTextMatcher.GetGrammarPhrases(phrase, sensitivity);
        var g = source == "app"
            ? JsonSerializer.Serialize(forms.Append("[unk]").ToArray())
            : JsonSerializer.Serialize(WakeWordGrammarConfusers.ComposeGrammar(phrase, forms));
        using var rec = new VoskRecognizer(model, 16000f, g);
        var accepted = new List<string>(); var rejected = new List<string>(); var ms = new List<long>();
        double skipUntil = -1;
        for (int off = 0; off < pcm.Length; off += 3200)
        {
            var n = Math.Min(3200, pcm.Length - off); var t = off / 32000.0;
            if (t < skipUntil) continue;
            var buf = pcm.AsSpan(off, n).ToArray();
            var fin = rec.AcceptWaveform(buf, n);
            var text = Read(fin ? rec.Result() : rec.PartialResult(), fin ? "text" : "partial");
            if (!WakeWordTextMatcher.Evaluate(text, phrase, sensitivity).IsMatch) continue;
            rec.Reset(); skipUntil = t + 2.5;
            // Se espera medio segundo más para que la palabra termine de entrar en la ventana.
            var endByte = Math.Min(pcm.Length, off + n + 16000);
            var startByte = Math.Max(0, endByte - 80000);
            startByte -= startByte % 2;
            var sw = Stopwatch.StartNew();
            var heard = Transcribe(whisper, pcm.AsSpan(startByte, endByte - startByte).ToArray(), verifyPrompt);
            ms.Add(sw.ElapsedMilliseconds);
            var ok = Wakes(WakeWordTextMatcher.Normalize(heard), phrase, sensitivity, free: true);
            (ok ? accepted : rejected).Add($"{t:0.0}s «{heard}»");
        }
        Console.WriteLine($"[cascada-{source,-10}] despertares: {accepted.Count} (Whisper descartó {rejected.Count}, {(ms.Count > 0 ? ms.Average() : 0):0} ms por verificación)");
        foreach (var a in accepted) Console.WriteLine($"      sí  {a}");
        foreach (var r in rejected) Console.WriteLine($"      no  {r}");
    }
    // 2) Transcripción libre, para ver qué oye realmente.
    using (var free = new VoskRecognizer(model, 16000f))
    {
        free.AcceptWaveform(pcm, pcm.Length);
        Console.WriteLine("Lo que oye sin gramática: «" + Read(free.FinalResult(), "text") + "»");
    }
}

// En modo libre el texto trae la frase entera; se busca la ventana de palabras que contiene el nombre.
static bool Wakes(string text, WakeWordPhrase phrase, WakeWordSensitivity sens, bool free)
{
    if (!free) return WakeWordTextMatcher.Evaluate(text, phrase, sens).IsMatch;
    var w = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    for (int i = 0; i < w.Length; i++) for (int len = 1; len <= 2 && i + len <= w.Length; len++)
        if (WakeWordTextMatcher.Evaluate(string.Join(' ', w[i..(i + len)]), phrase, sens).IsMatch) return true;
    return false;
}
static double NameConfidence(string json, bool final)
{
    using var d = JsonDocument.Parse(json);
    if (!d.RootElement.TryGetProperty(final ? "result" : "partial_result", out var arr)) return -1;
    foreach (var w in arr.EnumerateArray())
        if (w.TryGetProperty("word", out var word) && (word.GetString() ?? "").Contains("ura") && w.TryGetProperty("conf", out var c)) return c.GetDouble();
    return -1;
}
static string Transcribe(WhisperFactory factory, byte[] pcm16k, string? prompt)
{
    using var wav = new MemoryStream();
    using (var w = new BinaryWriter(wav, System.Text.Encoding.ASCII, leaveOpen: true))
    {
        w.Write("RIFF"u8); w.Write(36 + pcm16k.Length); w.Write("WAVE"u8); w.Write("fmt "u8); w.Write(16);
        w.Write((short)1); w.Write((short)1); w.Write(16000); w.Write(32000); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(pcm16k.Length); w.Write(pcm16k);
    }
    wav.Position = 0;
    var builder = factory.CreateBuilder().WithLanguage("es");
    if (!string.IsNullOrWhiteSpace(prompt)) builder = builder.WithPrompt(prompt);
    using var processor = builder.Build();
    var sb = new System.Text.StringBuilder();
    foreach (var segment in processor.ProcessAsync(wav).ToBlockingEnumerable()) sb.Append(segment.Text?.Trim()).Append(' ');
    return sb.ToString().Trim();
}
static string Read(string json, string p) { using var d = JsonDocument.Parse(json); return d.RootElement.TryGetProperty(p, out var v) ? v.GetString() ?? "" : ""; }

static byte[] ToPcm16kMono(string file)
{
    var psi = new ProcessStartInfo("ffmpeg", $"-v error -i \"{file}\" -ac 1 -ar 16000 -f s16le -") { RedirectStandardOutput = true, UseShellExecute = false };
    using var p = Process.Start(psi)!; using var ms = new MemoryStream(); p.StandardOutput.BaseStream.CopyTo(ms); p.WaitForExit(); return ms.ToArray();
}

static void Synth(string text, string outFile)
{
    using var s = new System.Speech.Synthesis.SpeechSynthesizer();
    var voice = s.GetInstalledVoices().Select(v => v.VoiceInfo).FirstOrDefault(v => v.Culture.Name.StartsWith("es"));
    if (voice != null) s.SelectVoice(voice.Name);
    s.SetOutputToWaveFile(outFile); s.Speak(text);
    Console.WriteLine($"TTS con {voice?.Name ?? "voz por defecto"}");
}
