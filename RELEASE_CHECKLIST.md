# Checklist de lanzamiento de Sakura

## Código

- [ ] Sakura (y las copias de los nombres anteriores, Kohana y Nexo) están cerradas completamente.
- [ ] `dotnet restore .\Nexo.slnx` pasa.
- [ ] `dotnet test .\Nexo.slnx -c Release` pasa.
- [ ] `dotnet build .\Nexo.slnx -c Release` pasa.
- [ ] No hay cambios sin commit.
- [ ] `CHANGELOG.md` contiene la versión que se publicará.
- [ ] El número de versión coincide con la etiqueta.

## Identidad y migración

- [ ] La ventana, Capsule, Peek, bandeja y onboarding muestran Sakura.
- [ ] El ejecutable generado es `Sakura.exe`.
- [ ] El icono de aplicación se ve a 16, 32 y 256 px.
- [ ] `Oye Sakura`, `Sakura` y `Hey Sakura` funcionan según la frase seleccionada.
- [ ] La prueba muestra exactamente lo que entendió Vosk.
- [ ] Los aliases personales se guardan sin audio.
- [ ] Los datos de `%LocalAppData%\Nexo` y `%LocalAppData%\Kohana` (la cadena de `ProductIdentity.PreviousDataDirectoryNames`) se copian a `%LocalAppData%\Sakura` sin borrar el origen.
- [ ] Una segunda ejecución no duplica ni sobrescribe la migración.

## Runtime

- [ ] Sistema muestra estado de voz, IA, Vision y rendimiento.
- [ ] Reiniciar voz funciona sin reiniciar Sakura.
- [ ] Diagnóstico abre desde el panel Runtime.

## Artefactos

- [ ] `scripts\publish.ps1` genera `Sakura.exe`.
- [ ] `scripts\verify-release.ps1` no encuentra datos privados.
- [ ] El SHA-256 del ZIP coincide.
- [ ] El ZIP portable abre después de extraerse en otra carpeta.
- [ ] El instalador se crea con Inno Setup 6.
- [ ] El instalador funciona sin permisos de administrador.
- [ ] Los accesos directos abren Sakura.
- [ ] La bandeja, `Alt + A`, Peek y Look Mode funcionan instalados.
- [ ] La desinstalación elimina la aplicación.
- [ ] El usuario puede conservar o borrar sus datos locales de Sakura.

## Actualización de la versión anterior

Es lo único que este bloque de la lista existe para asegurar: que actualizar de la versión anterior a la nueva funciona. Se ensaya en una copia aparte (zip portable de la versión anterior en una carpeta de al menos dos niveles, con `SAKURA_DATA_ROOT` aislado), nunca sobre la instalación real.

- [ ] La publicación tiene sus **cuatro** adjuntos y cada `.sha256` se llama exactamente como su archivo más `.sha256`.
- [ ] Instalada la versión **anterior**, «Buscar actualizaciones» encuentra la nueva.
- [ ] Instalarla deja la versión nueva en la carpeta, **vuelve a abrir Sakura sola**, y `%LOCALAPPDATA%\Sakura\actualizaciones\ultima-actualizacion.log` termina en «Terminado.».
- [ ] Tras actualizar, **no vuelve a salir la bienvenida** y se conservan tareas, rutinas, enfoque y el modelo de IA elegido.
- [ ] Tras actualizar, «Aplicaciones instaladas» de Windows dice la versión nueva.
- [ ] Tras actualizar, el desinstalador sigue existiendo y desinstalar deja la carpeta vacía.
- [ ] No quedan `Sakura.new`, `Sakura.old` ni zips en la carpeta de actualizaciones.

## Prueba limpia

- [ ] La primera ejecución muestra onboarding.
- [ ] Ollama ausente o detenido produce un mensaje claro, no un cierre.
- [ ] El modo local funciona sin modelos descargados.
- [ ] Micrófono desconectado produce una explicación clara.
- [ ] Tareas, enfoque y rutinas persisten.
- [ ] Vision funciona en al menos un monitor y con escalado 125 %.
- [ ] `Windows + Shift + S` no activa Modo Juego.
- [ ] Un juego real a pantalla completa sí activa Modo Juego.
- [ ] Buscar actualizaciones utiliza el repositorio configurado.

## Publicación

- [ ] Se creó una etiqueta `vX.Y.Z-sufijo`.
- [ ] GitHub Actions terminó en verde.
- [ ] La release está marcada como beta/prerelease.
- [ ] ZIP, instalador y archivos SHA-256 están adjuntos.
- [ ] Las notas no incluyen claves, rutas personales ni capturas privadas.
