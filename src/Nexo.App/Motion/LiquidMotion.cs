using System.Windows.Media;
using Nexo.Core.Shell;

namespace Nexo.App.Motion;

/// <summary>
/// Los muelles de los mandos líquidos: el valor que se ve, la inercia de la comba, lo inflado del
/// pomo y la intensidad del brillo. Lo comparten el mando vertical de los controles rápidos
/// (<c>LiquidLevelGauge</c>) y el deslizador horizontal de la pestaña Audio (<c>LiquidSliderTrack</c>),
/// que antes eran dos formas distintas de moverse para la misma idea.
///
/// Solo pide fotogramas mientras algo se mueve; en reposo no cuesta nada.
/// </summary>
public sealed class LiquidMotion
{
    // El valor llega con un pequeño rebote; la comba tiembla más, que es lo que la hace parecer
    // líquida; el pomo se infla al aparecer y al pulsarlo.
    private const double ValueStiffness = 170;
    private const double ValueDamping = 0.74;
    private const double StretchStiffness = 260;
    private const double StretchDamping = 0.42;
    private const double KnobStiffness = 320;
    private const double KnobDamping = 0.5;

    private readonly Action _invalidate;

    private double _velocity;
    private double _stretchVelocity;
    private double _stretchTarget;
    private double _knobVelocity;
    private double _knobTarget = 1;
    private double _glowVelocity;
    private double _glowTarget = 1;
    private bool _following;
    private bool _ticking;
    private TimeSpan _lastFrame = TimeSpan.MinValue;
    private DateTime _lastFollow = DateTime.MinValue;

    public LiquidMotion(Action invalidate)
    {
        ArgumentNullException.ThrowIfNull(invalidate);
        _invalidate = invalidate;
    }

    /// <summary>El valor pedido, de 0 a 100; lo dibujado puede ir un poco detrás.</summary>
    public double Target { get; private set; }

    /// <summary>El valor que se dibuja. Puede pasarse un poco del rango mientras rebota.</summary>
    public double Value { get; private set; }

    /// <summary>La inercia de la comba, de -1 a 1; positiva cuando el valor sube.</summary>
    public double Stretch { get; private set; }

    public double Knob { get; private set; } = 1;

    public double Glow { get; private set; } = 1;

    /// <summary>
    /// Entrada al aparecer: el pomo se infla desde pequeño y el valor sube un tramo corto hasta su
    /// sitio. Un tramo corto y no desde cero: lo que se quiere es que se asiente, no que se recorra.
    /// </summary>
    public void Reveal(double percent)
    {
        Target = Math.Clamp(percent, 0, 100);
        _following = false;

        if (!SakuraMotion.AnimationsEnabled)
        {
            SnapTo(Target);
            return;
        }

        Value = Math.Max(0, Target - 30);
        _velocity = 0;
        Knob = 0.35;
        _knobVelocity = 0;
        _knobTarget = 1;
        Glow = 0;
        _glowVelocity = 0;
        _glowTarget = 1;
        Stretch = 0;
        _stretchVelocity = 0;
        _stretchTarget = 0;
        Start();
    }

    /// <summary>
    /// Pone un valor. Con <paramref name="follow"/> el pomo va exactamente donde está el puntero —D38:
    /// una barra que se arrastra no interpola— y lo único que se anima es la inercia de la comba.
    /// </summary>
    public void SetLevel(double percent, bool follow)
    {
        var clamped = Math.Clamp(percent, 0, 100);

        if (!SakuraMotion.AnimationsEnabled)
        {
            SnapTo(clamped);
            return;
        }

        if (follow)
        {
            var now = DateTime.UtcNow;
            var seconds = Math.Max((now - _lastFollow).TotalSeconds, 1d / 120);
            var speed = _following ? (clamped - Target) / seconds : 0;
            _lastFollow = now;
            _following = true;

            Target = clamped;
            Value = clamped;
            _velocity = 0;
            _stretchTarget = Math.Clamp(speed / 260, -1, 1);
        }
        else
        {
            _following = false;
            Target = clamped;
        }

        Start();
    }

    /// <summary>El pomo se encoge un poco mientras se pulsa y se infla al soltar, como una burbuja.</summary>
    public void SetPressed(bool pressed)
    {
        if (!pressed)
        {
            _following = false;
        }

        if (!SakuraMotion.AnimationsEnabled)
        {
            return;
        }

        _knobTarget = pressed ? 0.86 : 1;
        _glowTarget = pressed ? 1.35 : 1;
        Start();
    }

    /// <summary>Al valor sin animar, y en reposo.</summary>
    public void SnapTo(double percent)
    {
        Stop();
        Target = Math.Clamp(percent, 0, 100);
        Value = Target;
        _velocity = 0;
        Stretch = _stretchVelocity = _stretchTarget = 0;
        Knob = _knobTarget = 1;
        _knobVelocity = 0;
        Glow = _glowTarget = 1;
        _glowVelocity = 0;
        _following = false;
        _invalidate();
    }

    public void Stop()
    {
        if (_ticking)
        {
            _ticking = false;
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    private void Start()
    {
        if (!_ticking)
        {
            _ticking = true;
            _lastFrame = TimeSpan.MinValue;
            CompositionTarget.Rendering += OnRendering;
        }

        _invalidate();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var time = e is RenderingEventArgs rendering ? rendering.RenderingTime : TimeSpan.Zero;

        // Rendering puede avisar dos veces por fotograma con el mismo instante; el segundo no avanza nada.
        if (time == _lastFrame)
        {
            return;
        }

        var dt = _lastFrame == TimeSpan.MinValue ? 1d / 60 : (time - _lastFrame).TotalSeconds;
        _lastFrame = time;

        if (!_following)
        {
            double value, velocity;
            (value, velocity) = LiquidLevelMath.SpringStep(Value, _velocity, Target, dt, ValueStiffness, ValueDamping);
            Value = value;
            _velocity = velocity;
            _stretchTarget = Math.Clamp(_velocity / 260, -1, 1);
        }
        else
        {
            // Si el puntero se para, la gota deja de estirarse aunque no llegue otro movimiento.
            _stretchTarget *= Math.Pow(0.02, dt);
        }

        (var stretch, _stretchVelocity) = LiquidLevelMath.SpringStep(Stretch, _stretchVelocity, _stretchTarget, dt, StretchStiffness, StretchDamping);
        (var knob, _knobVelocity) = LiquidLevelMath.SpringStep(Knob, _knobVelocity, _knobTarget, dt, KnobStiffness, KnobDamping);
        (var glow, _glowVelocity) = LiquidLevelMath.SpringStep(Glow, _glowVelocity, _glowTarget, dt, KnobStiffness, 0.9);
        Stretch = stretch;
        Knob = knob;
        Glow = glow;

        _invalidate();

        var settled =
            LiquidLevelMath.IsSettled(Value, _velocity, Target) &&
            LiquidLevelMath.IsSettled(Stretch, _stretchVelocity, _stretchTarget, 0.004) &&
            Math.Abs(_stretchTarget) < 0.004 &&
            LiquidLevelMath.IsSettled(Knob, _knobVelocity, _knobTarget, 0.003) &&
            LiquidLevelMath.IsSettled(Glow, _glowVelocity, _glowTarget, 0.003);

        if (settled)
        {
            Value = Target;
            Stretch = 0;
            Knob = _knobTarget;
            Glow = _glowTarget;
            Stop();
        }
    }
}
