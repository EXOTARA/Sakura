/*
  Sakura — comportamiento de la página de descargas.

  Sin librerías. Todo lo que hay aquí cabe en un archivo y no necesita red: la página tiene que
  poder abrirse desde cualquier sitio, incluso desde el disco, y cargar rápido en el equipo de
  alguien que todavía no sabe si quiere descargar nada.

  Cuatro cosas: los pétalos, la marca que sigue al puntero, la entrada de las secciones al
  desplazar, y los botones de copiar hash. Las cuatro se apagan solas si el sistema pide menos
  movimiento.
*/

(function () {
  "use strict";

  var reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

  /* ------------------------------------------------------------------ *
   * Pétalos
   *
   * Son tridimensionales de verdad, dentro de lo que da un lienzo 2D: cada pétalo gira sobre su
   * propio eje vertical, y esa rotación se proyecta comprimiendo su anchura y oscureciéndolo
   * cuando queda de canto. Un pétalo que sólo rota en el plano se lee como una pegatina; uno que
   * se pone de perfil y vuelve a abrirse se lee como algo que cae.
   *
   * La profundidad hace el resto: los de atrás son más pequeños, más lentos y más tenues.
   * ------------------------------------------------------------------ */

  function startPetals(canvas) {
    var ctx = canvas.getContext("2d", { alpha: true });
    if (!ctx) return;

    var petals = [];
    var width = 0;
    var height = 0;
    var dpr = 1;
    var running = true;
    var frame = 0;
    var lastTime = 0;

    // Los tonos salen de la paleta de la aplicación, no de un degradado cualquiera.
    var tints = [
      [232, 175, 192],
      [196, 136, 154],
      [223, 205, 212],
      [169, 111, 130]
    ];

    function count() {
      if (width < 640) return 16;
      if (width < 1100) return 30;
      return 46;
    }

    function makePetal(seeded) {
      var depth = Math.random();
      return {
        x: Math.random() * width,
        y: seeded ? Math.random() * height : -40 - Math.random() * height * 0.5,
        depth: depth,
        size: 7 + depth * 13,
        fall: 14 + depth * 34,
        drift: (Math.random() - 0.5) * 26,
        sway: 0.5 + Math.random() * 1.1,
        phase: Math.random() * Math.PI * 2,
        spin: Math.random() * Math.PI * 2,
        spinRate: (0.5 + Math.random() * 1.5) * (Math.random() < 0.5 ? -1 : 1),
        roll: Math.random() * Math.PI * 2,
        rollRate: (Math.random() - 0.5) * 0.9,
        tint: tints[(Math.random() * tints.length) | 0]
      };
    }

    function resize() {
      var rect = canvas.getBoundingClientRect();
      dpr = Math.min(window.devicePixelRatio || 1, 2);
      width = rect.width;
      height = rect.height;
      canvas.width = Math.round(width * dpr);
      canvas.height = Math.round(height * dpr);
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

      var target = count();
      while (petals.length < target) petals.push(makePetal(true));
      if (petals.length > target) petals.length = target;
    }

    // Un pétalo: dos curvas que se juntan en la punta y una nervadura suave.
    function drawPetal(petal, openness) {
      var s = petal.size;
      ctx.beginPath();
      ctx.moveTo(0, s);
      ctx.bezierCurveTo(-s * 0.95, s * 0.35, -s * 0.62, -s * 0.85, 0, -s);
      ctx.bezierCurveTo(s * 0.62, -s * 0.85, s * 0.95, s * 0.35, 0, s);
      ctx.closePath();

      var c = petal.tint;
      // De canto entra menos luz: el pétalo se oscurece igual que lo haría uno de verdad.
      var light = 0.45 + openness * 0.55;
      var alpha = (0.28 + petal.depth * 0.5) * (0.35 + openness * 0.65);
      ctx.fillStyle =
        "rgba(" + Math.round(c[0] * light) + "," + Math.round(c[1] * light) +
        "," + Math.round(c[2] * light) + "," + alpha.toFixed(3) + ")";
      ctx.fill();
    }

    function step(now) {
      if (!running) return;
      window.requestAnimationFrame(step);

      var dt = lastTime ? Math.min((now - lastTime) / 1000, 0.05) : 0.016;
      lastTime = now;
      frame++;

      ctx.clearRect(0, 0, width, height);

      for (var i = 0; i < petals.length; i++) {
        var p = petals[i];

        p.y += p.fall * dt;
        p.phase += p.sway * dt;
        p.x += (p.drift + Math.sin(p.phase) * 22) * dt;
        p.spin += p.spinRate * dt;
        p.roll += p.rollRate * dt;

        if (p.y - p.size > height) {
          petals[i] = makePetal(false);
          continue;
        }
        if (p.x < -60) p.x = width + 40;
        else if (p.x > width + 60) p.x = -40;

        // La proyección: el ancho se comprime con el coseno del giro propio.
        var openness = Math.abs(Math.cos(p.spin));

        ctx.save();
        ctx.translate(p.x, p.y);
        ctx.rotate(p.roll);
        ctx.scale(Math.max(openness, 0.08), 1);
        drawPetal(p, openness);
        ctx.restore();
      }
    }

    // Una sola imagen quieta cuando el sistema pide menos movimiento: la página sigue teniendo
    // pétalos, sencillamente no caen.
    function drawStill() {
      ctx.clearRect(0, 0, width, height);
      for (var i = 0; i < petals.length; i++) {
        var p = petals[i];
        var openness = Math.abs(Math.cos(p.spin));
        ctx.save();
        ctx.translate(p.x, p.y);
        ctx.rotate(p.roll);
        ctx.scale(Math.max(openness, 0.08), 1);
        drawPetal(p, openness);
        ctx.restore();
      }
    }

    function apply() {
      resize();
      if (reduceMotion.matches) {
        running = false;
        drawStill();
      } else if (!running) {
        running = true;
        lastTime = 0;
        window.requestAnimationFrame(step);
      }
    }

    var resizeTimer = 0;
    window.addEventListener("resize", function () {
      window.clearTimeout(resizeTimer);
      resizeTimer = window.setTimeout(apply, 150);
    });

    // Una pestaña que no se ve no necesita gastar batería.
    document.addEventListener("visibilitychange", function () {
      if (reduceMotion.matches) return;
      if (document.hidden) {
        running = false;
      } else if (!running) {
        running = true;
        lastTime = 0;
        window.requestAnimationFrame(step);
      }
    });

    if (reduceMotion.addEventListener) {
      reduceMotion.addEventListener("change", apply);
    }

    resize();
    if (reduceMotion.matches) {
      drawStill();
    } else {
      window.requestAnimationFrame(step);
    }
  }

  /* ------------------------------------------------------------------ *
   * La marca se inclina hacia el puntero.
   * Muy poco: seis grados como máximo. Lo suficiente para que parezca que tiene volumen.
   * ------------------------------------------------------------------ */

  function startMarkParallax(stage, mark) {
    if (reduceMotion.matches) return;
    if (!window.matchMedia("(hover: hover) and (pointer: fine)").matches) return;

    var pending = false;
    var targetX = 0;
    var targetY = 0;

    function render() {
      pending = false;
      mark.style.transform = "rotateX(" + targetY.toFixed(2) + "deg) rotateY(" + targetX.toFixed(2) + "deg)";
    }

    stage.addEventListener("pointermove", function (event) {
      var rect = stage.getBoundingClientRect();
      targetX = ((event.clientX - rect.left) / rect.width - 0.5) * 12;
      targetY = ((event.clientY - rect.top) / rect.height - 0.5) * -12;
      if (!pending) {
        pending = true;
        window.requestAnimationFrame(render);
      }
    });

    stage.addEventListener("pointerleave", function () {
      targetX = 0;
      targetY = 0;
      if (!pending) {
        pending = true;
        window.requestAnimationFrame(render);
      }
    });
  }

  /* ------------------------------------------------------------------ *
   * Entrada de secciones y barra pegada.
   * ------------------------------------------------------------------ */

  function startReveals() {
    var items = document.querySelectorAll(".reveal");
    if (!("IntersectionObserver" in window) || reduceMotion.matches) {
      for (var i = 0; i < items.length; i++) items[i].classList.add("in");
      return;
    }

    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        // Escalonado corto: 45 ms entre hermanos, tal como pide la guía de movimiento.
        var delay = Number(entry.target.getAttribute("data-stagger") || 0) * 45;
        window.setTimeout(function () {
          entry.target.classList.add("in");
        }, delay);
        observer.unobserve(entry.target);
      });
    }, { rootMargin: "0px 0px -12% 0px", threshold: 0.08 });

    for (var j = 0; j < items.length; j++) observer.observe(items[j]);
  }

  function startStickyBar(bar) {
    var ticking = false;
    function update() {
      ticking = false;
      bar.classList.toggle("is-stuck", window.scrollY > 12);
    }
    window.addEventListener("scroll", function () {
      if (ticking) return;
      ticking = true;
      window.requestAnimationFrame(update);
    }, { passive: true });
    update();
  }

  /* ------------------------------------------------------------------ *
   * Copiar un hash.
   * El botón dice qué pasó; no se confía en que el color solo lo explique.
   * ------------------------------------------------------------------ */

  function startCopyButtons() {
    document.addEventListener("click", function (event) {
      var button = event.target.closest ? event.target.closest(".copy") : null;
      if (!button) return;

      var target = document.getElementById(button.getAttribute("data-copy"));
      if (!target) return;

      var label = button.querySelector(".copy-label");
      var original = label ? label.textContent : "";

      function done(ok) {
        button.setAttribute("data-copied", ok ? "true" : "false");
        if (label) label.textContent = ok ? "Copiado" : "No se pudo";
        window.setTimeout(function () {
          button.removeAttribute("data-copied");
          if (label) label.textContent = original;
        }, 2200);
      }

      var text = (target.textContent || "").trim();
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).then(function () { done(true); }, function () { done(false); });
      } else {
        done(false);
      }
    });
  }

  /* ------------------------------------------------------------------ */

  function init() {
    var canvas = document.getElementById("petals");
    if (canvas) startPetals(canvas);

    var stage = document.querySelector(".mark-stage");
    var mark = document.querySelector(".mark");
    if (stage && mark) startMarkParallax(stage, mark);

    var bar = document.querySelector(".topbar");
    if (bar) startStickyBar(bar);

    startReveals();
    startCopyButtons();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
