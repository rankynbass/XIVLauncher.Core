# TextureWrap.cs — Specification

**File:** `src/XIVLauncher.Core/TextureWrap.cs` · 100 lines · 3,845 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public class TextureWrap : IDisposable, IEquatable<TextureWrap>`
**Role:** Lifetime-managed bridge between an encoded image byte blob, an SDL3 surface, an SDL3 GPU texture, and an ImGui texture handle

---

## 1. Purpose

`TextureWrap` is the project's image object: construct it from encoded image bytes (PNG/JPG/…), and it becomes drawable in ImGui. It exists because ImGui and SDL3 must be kept in lockstep:

```
byte[] ──SDLImage.LoadIO──▶ SDLSurface (CPU pixels)
                                  │  convert to ABGR8888
                                  ▼
                          SDLGPUTexture (on Program.GPUDevice)
                                  │  wrapped in
                                  ▼
                          ImTextureData (Rgba32) ──▶ ImTextureRef (public handle)
```

The class is `Disposable` and is registered in `ImGuiBindings`' refcount lists, so it participates in the renderer's per-frame upload/teardown machinery.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `ImGuiHandle` | `ImTextureRef`, get-only | the value passed to `ImGui.Image(...)`/draw calls; built from `(ImTextureData*, SDLGPUTexture*)` |
| `Width` | `unsafe uint`, get-only | `surface.W` |
| `Height` | `unsafe uint`, get-only | `surface.H` |
| `Size` | `Vector2`, get-only | `(Width, Height)` — for ImGui sizing arguments |
| `TextureWrap(byte[] data)` | **`protected`** ctor | the only construction route; blocked from outside the class |
| `static TextureWrap Load(byte[] data)` | public factory | `=> new(data)` — the public entry point (the `protected` ctor is what forces `Load`) |
| `void Dispose()` | public, `unsafe` | releases all three resources + deregisters |
| `bool Equals(TextureWrap?)` | `unsafe` | identity/value hybrid (see §4) |
| `override bool Equals(object?)` | public | delegates to the above |
| `override int GetHashCode()` | `unsafe` | `HashCode.Combine` over the three raw pointers + `ImGuiHandle` |
| `textureData`, `deviceTexture`, `surface` | `private readonly unsafe` pointers | hidden native resources |

## 3. Construction contract (`Load`)

1. `SDL.IOFromMem(dataPtr, len)` wraps the caller's `byte[]` as an SDL IO object **without copying** — the caller's buffer must stay valid for the duration of `LoadIO` (it does: the span keeps `data` alive through the call).
2. `SDLImage.LoadIO(ioStream, true)` decodes to an `SDLSurface` (`true` = keep/exact pixel format request as documented by the binding).
3. If the surface format is not `SDLPixelFormat.Abgr8888`, `SDL.ConvertSurface(..., Abgr8888)` — the SDL GPU texture path expects ABGR8888 from surfaces.
4. A `SDLGPUTextureCreateInfo` is built with fixed parameters: `R8G8B8A8Unorm`, `Usage = Sampler`, 1 layer, 1 mip level, `Texturetype2D`, `Samplecount1`, `Props = 0`. **No mipmaps, no sRGB format flag.**
5. `SDL.CreateGPUTexture(Program.GPUDevice, &createInfo)` — bound to the *global* device accessed through `Program.GPUDevice`, not an injected device: the texture is unusable if the caller's ImGui/SDL context is not the launcher's.
6. `ImTextureData` is allocated with `Marshal.AllocHGlobal(sizeof(ImTextureData))` and filled field-by-field: `Width`/`Height`/`UsedRect.W`/`UsedRect.H` from the surface, `UsedRect.X = Y = 0`, `Format = ImTextureFormat.Rgba32`, `TexID = deviceTexture`, `WantDestroyNextFrame = 0`.
7. `Program.ImGuiBindings.AddTextureCreator(deviceTexture, surface)` queues the pair for **upload on the next `Render()`** (a copy-pass uploads the CPU surface pixels into the GPU texture through a transfer buffer).
8. `ImGuiHandle = ImTextureRef(textureData, deviceTexture)`.
9. `Program.ImGuiBindings.RefTexture(this)` adds the object to the renderer's referenced-texture list (keeping it alive and disposed-on-shutdown).

Note the ordering subtlety: step 9 runs **outside** the `unsafe` block, i.e. after the unsafe scope closes — same effect, but it means a failure inside the unsafe block leaves the object unregistered (and the partially created GPU resources leaked).

## 4. Equality, hashing, and identity

`Equals` short-circuits on `ReferenceEquals`, then compares **all three native pointers** *and* `ImGuiHandle.Equals(other)`. Since `ImGuiHandle` is built from the first two pointers, this is effectively pointer identity — two separately constructed `TextureWrap`s for the same image are **not** equal. `GetHashCode` mixes the same three pointers plus the handle, consistent with that contract. `Equals(null)` is `false`.

Because identity is pointer-based and hashing is pointer-based, `TextureWrap` values are safe as `Set`/`Map` keys only when the *same instance* is reused; caching by image bytes requires an external key (bytes/hash), not the object.

## 5. `Dispose` contract

```
SDL.ReleaseGPUTexture(Program.GPUDevice, deviceTexture);
Marshal.FreeHGlobal((nint)textureData);
SDL.DestroySurface(surface);
...
Program.ImGuiBindings.DerefTexture(this);   // runs even if the unsafe block partially failed? see §6
```

Ordering matters: GPU texture released before the ImGui-side `ImTextureData` is freed, since `ImTextureData.TexID` references the GPU texture. Deregistration is last, keeping the renderer's list consistent.

## 6. Hazards and invariants

- **Upload happens in `ImGuiBindings.Render()`, not at construction.** A freshly `Load`ed texture is uploaded during the first `Render()` after creation; drawing it in the same frame it was created in is therefore valid only because creation happens before that frame's render (the launcher's loop order is `NewFrame → Draw → Render`). Creating a texture *during* `Render` would upload it late.
- **`AddTextureCreator` is called by the ctor, and the renderer clears its upload queue each frame** (`texturesToBind.Clear()` after the copy pass). A texture created but never followed by a `Render()` leaks.
- **`Program.GPUDevice`/`Program.ImGuiBindings` are required non-null globals**: calling `Load` before `Program.Main` reaches the ImGui init step dereferences `null!`.
- **No `Dispose` guard for double disposal:** calling `Dispose()` twice (explicitly *and* via the shutdown sweep in `ImGuiBindings.Dispose()`) double-frees `textureData` and double-releases the GPU texture — `DerefTexture` removes from a list, so a second `Dispose` is not prevented by the ref bookkeeping. Callers must dispose exactly once.
- **No size guard:** a huge decoded surface allocates proportionally (surface pixels + GPU texture + per-frame transfer buffer). `ImGuiBindings.Render()` grows the transfer buffer only when a larger texture appears, so peak transfer-buffer size equals the largest texture uploaded.
- **`Width`/`Height` read the CPU surface, not the GPU texture** — they would silently disagree if `ConvertSurface` ever produced a resized surface (e.g. a format-converting path).
- `protected` constructor with a `Load` forwarder means **no subclass or external caller can construct one directly**; a derived type would need its own ctor chain, and since the class is not `sealed` a subclass would additionally inherit the disposal contract.
- **Non-mipmapped, non-sRGB:** textures are `R8G8B8A8Unorm` with a single level and drawn as SDR; the launcher does not benefit from filtered minification levels, and colour values are treated as display-space.
- **`unsafe` members** (`Width`, `Height`, `GetHashCode`, `Equals`) are marked because they touch pointer fields; the pointer reads are pure but the annotation is what forces call sites into `unsafe` blocks in the `Hexa` binding style.

## 7. Usage

```csharp
unsafe {
    var logo = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("logo.png"));
    // draw with ImGuiBindings' renderer; texture is uploaded on the next Render()
    ImGui.Image(ImGuiHelpers.ViewportSize origin, logo.ImGuiHandle, logo.Size, ImGuiColors.DalamudWhite);
    ...
    logo.Dispose();   // exactly once
}
```

## 8. Related units

`Program.GPUDevice` (SDL device handle), `Program.ImGuiBindings` (`AddTextureCreator`, `RefTexture`, `DerefTexture`), `ImGuiColors` (white tint for untinted draws), `AppUtil.GetEmbeddedResourceBytes` (the usual byte source), `Hexa.NET.SDL3.Image` (`SDLImage`), `Hexa.NET.ImGui` (`ImTextureRef`/`ImTextureData`/`ImTextureFormat`).
