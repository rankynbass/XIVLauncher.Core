# AppUtil.VersionInfo.cs — Specification

**File:** `src/XIVLauncher.Core/AppUtil.VersionInfo.cs` · 30 lines · 893 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public static partial class AppUtil { public partial class VersionInfo { … } }`
**Role:** Build-version carrier, completed by a compile-time source generator

---

## 1. Purpose

`VersionInfo` is a lazily-created singleton that exposes the launcher's own version as a structured value (`FileName`, `FileVersion`) instead of a hard-coded literal, so the version reported at runtime is always the version that was built.

The hand-written part contains **no version data**. It is a *partial* class whose missing constructor is emitted at build time by `XIVLauncher.VersionGenerator` into `AppUtil.VersionInfo.g.cs`.

## 2. Members

| Member | Kind | Contract |
| --- | --- | --- |
| `Version` | accessor, `public`, get-only, type `(string FileName, Version FileVersion)` (named tuple) | returns the version pair; assigned only by the generated constructor |
| `Instance()` | `public static` method returning `VersionInfo` | memoised singleton creation (`if (version == null) version = new VersionInfo();`) |
| `version` | `private static VersionInfo?` (nested inside the *instance* class, i.e. one per `AppUtil.VersionInfo` type) | singleton slot |

## 3. Generated counterpart (contract of the build)

`src/XIVLauncher.VersionGenerator/VersionSourceGenerator.cs` (`ISourceGenerator`, registered as an `Analyzer`-only `ProjectReference` with a `<CompilerVisibleProperty Include="AssemblyVersion" />`):

```csharp
public partial class VersionInfo
{
    public VersionInfo()
    {
        Version = ("<context.Compilation.AssemblyName>", new Version("<build_property.AssemblyVersion>"));
    }
}
```

`AssemblyVersion` flows from `XIVLauncher.Core.csproj`: `<Version>1.4.0</Version>` → `<FileVersion>$(Version)</FileVersion>` → `<AssemblyVersion>$(Version)</AssemblyVersion>`.

Consequences:

- The `Version` accessor must remain declared **without an initializer and without a `set`** — the generated constructor is the only writer.
- `FileName` is the compiling assembly name (`XIVLauncher.Core`), not a source file name.
- `FileVersion` is `new Version("1.4.0")` — the `Version(string)` constructor (available on the `net10.0` target of this project) sets `Major=1, Minor=4, Build=0, Patch=-1`; `Patch` is absent from `ToString()`, so `FileVersion.ToString()` == `"1.4.0"`, i.e. the string form round-trips to exactly `<FileVersion>` in the csproj.
- `AppUtil.GetAssemblyVersion()` returns that same string, so the log banner, the SDL window title and the UI "about" text all display the csproj `<Version>` verbatim. Verify at runtime if the exact display string matters — `Program.SetupLogging` and the SDL window title both print it.

## 4. Semantics and hazards

- **Not thread-safe:** `Instance()` has an unsynchronised null check; two threads could each build a `VersionInfo`. In practice it is first touched during single-threaded startup (`AppUtil.GetAssemblyVersion` from `SetupLogging`).
- The singleton is *not* a `Singleton<T>` type nor a static initializer, so it is not `const`-folded; each `Instance()` call after the first is a cheap null check.
- Because the nested class is `partial`, removing the generator from the build makes the constructor absent — `Instance()` would then return an object whose `Version` accessor is unset (`FileName == null`, `FileVersion` is a default `Version` with all `-1`). Nothing asserts this, so a mis-wired build silently reports a bogus version.
- `VersionInfo` is `public partial` but not `sealed`: it can be extended from any other file in this project (that is the mechanism the generator relies on).

## 5. Consumers

| Call site | Use |
| --- | --- |
| `AppUtil.GetAssemblyVersion()` | version string for logs, window title, "about" UI text |
| `Program.SetupLogging` | `Log.Information("Starting a session(v{Version} - {Hash})", AppUtil.GetAssemblyVersion(), AppUtil.GetGitHash())` |
| `Program.Main` | window title `"XIVLauncher {version} ({hash})"` |

## 6. Modification contract

To bump the version, change `<Version>` in `XIVLauncher.Core.csproj` — never edit this file. Editing the `Version` accessor's signature breaks the generator's emitted assignment and the `AppUtil.GetAssemblyVersion()` destructure (`var fvi = VersionInfo.Instance().Version; fvi.FileVersion`).
