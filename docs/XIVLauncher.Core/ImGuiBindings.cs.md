# ImGuiBindings.cs — Specification

**File:** `src/XIVLauncher.Core/ImGuiBindings.cs` · 288 lines · 9,892 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public class ImGuiBindings : IDisposable`
**Role:** The ImGui ⇄ SDL3-GPU bridge: context creation, input event pump, per-frame render, texture upload/ownership

---

## 1. Purpose

`ImGuiBindings` is the launcher's UI controller: a port of Veldrid/`ImGuiRenderer`'s responsibilities onto SDL 3's GPU API (`Hexa.NET.SDL3`) with the `Hexa.NET.ImGui.Backends.SDL3` renderer plug-in (`ImGuiImplSDL3`). It:

1. creates the ImGui context and wires the SDL-GPU backend to it,
2. installs keyboard/gamepad navigation and clipboard integration,
3. pumps SDL events once per frame (`ProcessExit`) and reports quit requests,
4. uploads launcher textures (`TextureWrap`s) into GPU textures via copy passes,
5. submits each frame's draw list into the window's swapchain texture,
6. owns the refcount list that keeps textures alive and disposes them at shutdown.

Its doc comment ("A modified version of Veldrid.ImGui's ImGuiRenderer") is retained from the original design; the SDL backend replaced Veldrid's device/queue objects entirely.

## 2. State surface

| Member | Kind | Notes |
| --- | --- | --- |
| `window` | `private unsafe SDLWindow*` | window set by `Program.Main` before construction |
| `device` | `private unsafe SDLGPUDevice*` | the SDL GPU device (same handle as `Program.GPUDevice`) |
| `transferBuffer` | `private unsafe SDLGPUTransferBuffer* = null!` | lazily created/grown by `CreateTransferBuffer` |
| `uploadSize` | `private uint = 0` | current transfer-buffer capacity (0 ⇒ none yet) |
| `clearColor` | `private Vector4 (0.1,0.1,0.1,1)` | background clear colour; duplicates `Program.ClearColor` (which is unused) |
| `iniPathPtr` | `private IntPtr` | heap ANSI copy of the ini path, kept alive for ImGui's `IniFilename` pointer |
| `texturesToBind` | `private readonly List<Tuple<Pointer<SDLGPUTexture>, Pointer<SDLSurface>>>` | **per-frame upload queue** |
| `texturesReferenced` | `private readonly List<TextureWrap>` | **liveness registry** for textures created by the launcher |

`#pragma warning disable CS8618` silences the "non-nullable field not set in ctor" warning because `transferBuffer` (and the alias-derived SDL objects) are initialised indirectly — the comment says "Veldrid objects are setup in method called by constructor".

Type aliases in scope: `ImSDLEvent`, `ImSDLGPUCommandBuffer`, `ImSDLGPUDevice`, `ImSDLGPURenderPass`, `ImSDLWindow`, `SDLGPUGraphicsPipeline`, plus `SDLEvent`/`SDLGPUDevice`/`SDLWindow`.

## 3. Constructor contract

`public unsafe ImGuiBindings(SDLWindow* window, SDLGPUDevice* device, FileInfo iniPath, float fontPxSize, float mainScale)`

In order:

1. Store `window`/`device`; `ImGui.CreateContext()` → `SetCurrentContext` → `GetIO()`.
2. `ConfigFlags |= NavEnableKeyboard | NavEnableGamepad`; `BackendFlags |= HasGamepad` — navigation (keyboard + gamepad) is always on, so the launcher is operable with a controller (Steam Deck).
3. `#if DEBUG`: `ConfigDebugIsDebuggerPresent = Debugger.IsAttached`, `ConfigErrorRecovery = true`, `ConfigErrorRecoveryEnableAssert = false`, `ConfigErrorRecoveryEnableDebugLog = false`, `ConfigErrorRecoveryEnableTooltip = true`. Debug builds recover from ImGui misuse and surface it as a tooltip instead of asserting.
4. `SetIniPath(iniPath.FullName)` — see §4.
5. Platform IO: `PlatformSetClipboardTextFn` / `PlatformGetClipboardTextFn` bound to the static methods via `Marshal.GetFunctionPointerForDelegate`, `PlatformClipboardUserData = 0` — ImGui's clipboard now routes to SDL's clipboard (`SDL.SetClipboardText` / `SDL.GetClipboardTextS`), so ImGui's built-in text-selection can be pasted into the system clipboard and back.
6. Style scaling: `ScaleAllSizes(mainScale)` **and** `FontScaleDpi = mainScale`, plus `ConfigDpiScaleFonts = true`, `ConfigDpiScaleViewports = true`. `mainScale` originates as `SDL.GetDisplayContentScale(SDL.GetPrimaryDisplay())` in `Program.Main`; sizes passed to the ctor (1280×800 window, `FontPxSize`) are already pre-scaled in `Program`, so this is DPI/style scaling on top of that — see §6 for the double-scaling caveat.
7. If `ViewportsEnable` is set in the flags: `WindowRounding = 0` and `Colors[WindowBg].W = 1` (fully opaque windows, required when viewports draw outside window bounds). Note that with the flags actually enabled in step 2, `ViewportsEnable` is **not** set by default, so this branch is dormant unless a theme/page turns it on.
8. `ImGuiImplSDL3.SetCurrentContext(ctx)`; `ImGuiImplSDL3.InitForSDLGPU((ImSDLWindow*)window)`.
9. `ImGuiImplSDLGPU3InitInfo { Device, ColorTargetFormat = SDL.GetGPUSwapchainTextureFormat(device, window), MSAASamples = Samplecount1 }` → `SDLGPU3Init(&initInfo)`. The colour target format is therefore **queried from the swapchain** and must match what `Program.Main` configured (`SDLGPUSwapchainComposition.Sdr`). MSAA is fixed at 1 sample (no antialiasing).
10. `new FontManager().SetupFonts(fontPxSize)` — fonts are (re)registered last, so they exist only after the backend is initialised. `fontPxSize` comes from `Config.FontPxSize ?? 21.0f`.

## 4. Ini ownership (`SetIniPath`)

- Frees any previous `iniPathPtr` (`Marshal.FreeHGlobal`) before overwriting, so repeated calls do not leak.
- `Marshal.StringToHGlobalAnsi` produces a **single-byte (ANSI) C string**; `io.IniFilename` is set to it as a `byte*`. ImGui writes/reads that ini path (style persistence). Consequences: paths outside the ANSI code page are mangled; the launcher must not free the buffer while ImGui may still hold it — `Dispose` does **not** free `iniPathPtr` (§6).
- The path is `<storage>/launcherUI.ini`, distinct from `launcher.ini` (config) and `dalamudUI.ini` (used by Dalamud's own UI).

## 5. Per-frame protocol

The frame loop in `Program.Main` calls these in this order: `ProcessExit()` → (`NewFrame()` → `launcherApp.Draw()` → `Render()`), or `SDL.Delay(10)` if the window is minimised/hidden.

### `ProcessExit()` → `bool`

Drains **all** pending SDL events (`while (SDL.PollEvent(&e))`), forwards each to `ImGuiImplSDL3.ProcessEvent` (so ImGui's nav/input state tracks controller/keyboard/window events), and returns `true` when the event type is `Quit`, or `WindowCloseRequested` whose `Window.WindowID == SDL.GetWindowID(this.window)`. Returning `true` ends the launcher's loop. Other windows' close requests are ignored.

### `NewFrame()`

`SDLGPU3NewFrame()` → `ImGuiImplSDL3.NewFrame()` → `ImGui.NewFrame()` — backend first, then the SDL plug-in, then ImGui itself.

### `Render()`

1. `ImGui.Render()` (produces the draw data) and `GetDrawData()`.
2. `isMinimized = drawData.DisplaySize.X <= 0 || Y <= 0`.
3. `SDL.AcquireGPUCommandBuffer(device)`.
4. **Texture upload pass** (inside `lock (texturesToBind)`): `BeginGPUCopyPass`; for each queued `(gpuTexture, surface)`:
   - `size = surface.Handle.Pitch * surface.Handle.H` (row-padded byte count),
   - if `uploadSize < size` → `CreateTransferBuffer(size)` (releases the old buffer, allocates a larger `Usage.Upload` one),
   - `MapGPUTransferBuffer(Program.GPUDevice, transferBuffer, true)`, copy `surface.Handle.Pixels` into the mapped span via `new Span<byte>(...)` copies, then `UnmapGPUTransferBuffer`,
   - `SDLGPUTextureTransferInfo { TransferBuffer, PixelsPerRow = 0, RowsPerLayer = 0 }` (zeros ⇒ let SDL infer from the region),
   - `SDLGPUTextureRegion` covering the whole surface (`X=Y=Z=0, W=surface.W, H=surface.H, D=1, Layer=0, MipLevel=0`),
   - `UploadToGPUTexture(copyPass, &transferInfo, &textureRegion, true)`.
   `EndGPUCopyPass`, then `texturesToBind.Clear()` — the queue is consumed exactly once per frame.
5. `WaitAndAcquireGPUSwapchainTexture(commandBuffer, window, &swapTexture, null, null)`; on failure the error is logged and `swapTexture` stays null.
6. If `swapTexture != null && !isMinimized`: `SDLGPU3PrepareDrawData`, a `SDLGPUColorTargetInfo { Texture = swapTexture, ClearColor = clearColor, LoadOp = Clear, StoreOp = Store }`, `BeginGPURenderPass(commandBuffer, &targetInfo, 1 /*subpasses*/, null)`, `SDLGPU3RenderDrawData(..., pipeline = null)` (the backend owns its pipeline), `EndGPURenderPass`.
7. If `ViewportsEnable`: `ImGui.UpdatePlatformWindows()` + `ImGui.RenderPlatformWindowsDefault()` (dormant by default, see §3 step 7).
8. `SDL.SubmitGPUCommandBuffer(commandBuffer)` — unconditional, so an empty/minimised frame still submits (a clear may not have happened, but the command buffer is valid).

Note that `Program.Main` also skips the whole `NewFrame/Draw/Render` trio when the window flags contain `Minimized | Hidden`, so the `isMinimized` check here is a second line of defence for the case where the frame is still presented.

## 6. Ownership & shutdown semantics

### Public texture API

| Member | Effect |
| --- | --- |
| `public unsafe void AddTextureCreator(SDLGPUTexture*, SDLSurface*)` | appends boxed `Pointer<>` pair to `texturesToBind` (upload queue) |
| `public void RefTexture(TextureWrap)` | adds to `texturesReferenced` (liveness registry) |
| `public void DerefTexture(TextureWrap)` | removes from `texturesReferenced` |

`TextureWrap`'s ctor calls `AddTextureCreator` then `RefTexture`; its `Dispose` calls `DerefTexture`. `ImGuiBindings` is therefore the *only* place that can guarantee textures are uploaded and released.

### `Dispose()`

1. Under `lock (texturesReferenced)`, snapshot the registry and `Dispose()` each referenced texture — i.e. shutdown disposes anything the launcher forgot to dispose itself. Because the snapshot is a copy, concurrent `RefTexture`/`DerefTexture` during teardown is tolerated (but see the double-dispose hazard in `TextureWrap.cs.md`).
2. `SDL.WaitForGPUIdle(device)` — drains GPU work before backend teardown.
3. `ImGuiImplSDL3.Shutdown()` → `ImGuiImplSDL3.SDLGPU3Shutdown()` → `ImGui.DestroyContext()`.

`window` and `device` are **not** destroyed here: `Program.Main` does `ReleaseWindowFromGPUDevice`, `DestroyGPUDevice`, `DestroyWindow`, `SDL.Quit()` afterwards.

## 7. Device-handle inconsistency (latent bug surface)

`CreateTransferBuffer`, `ReleaseGPUTransferBuffer`, `AcquireGPUCommandBuffer`, `WaitForGPUIdle` use **`this.device`**, while `MapGPUTransferBuffer`/`UnmapGPUTransferBuffer` in `Render()` use **`Program.GPUDevice`**. These are the same handle today (assigned from the same value) but the mixed usage means a second `ImGuiBindings` instance constructed with a different device would map transfer buffers on the wrong device. Spec-wise, prefer `this.device` everywhere.

## 8. Thread-safety

- `lock (…)` (C# `lock` statement) protects only the two lists' mutation: `texturesToBind` and `texturesReferenced`.
- Not protected: `transferBuffer`/`uploadSize` (`CreateTransferBuffer` is unsynchronised and called from `Render`), `clearColor`, `iniPathPtr`, and ImGui context state (inherently single-threaded).
- Consequence: texture *registration* may come from another thread, but rendering must remain single-threaded (`Program`'s frame loop).

## 9. Known deviations from a textbook ImGui backend

- MSAA fixed at 1 sample; no HDR (SDR composition, `Vsync` present mode — both set in `Program`, not here).
- Textures are uploaded **once per queued creation** rather than being resident: a `TextureWrap` created every frame pays a full copy pass every frame. Long-lived textures cost nothing per frame, but they also never re-upload — mutate-in-place drawing through the same surface is not supported (`AddTextureCreator` would have to be called again to requeue).
- ImGui's `IniFilename` is heap ANSI, so ini persistence assumes a single-byte path.
- Clipboard bridge uses `Marshal.GetFunctionPointerForDelegate` on **static** delegates, which are stable (no `freecallback` leak), but the delegates are never released explicitly.
- `DestroyContext` is called but `ImGui`'s allocated fonts are not individually destroyed — `FontManager` relies on atlas teardown (see `FontManager.cs.md`).
