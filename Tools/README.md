# Offline verification

`Verify-Offline.ps1` performs checks without starting Unity or touching a player's save file. It requires PowerShell 7.2+, Git, an installed Unity 6000.5.0f1 editor's **managed libraries/compiler**, and a previously imported copy of this project's package cache. `rg` speeds up GUID indexing but is optional.

```powershell
& .\Tools\Verify-Offline.ps1 `
  -ReferenceProject 'C:\path\to\a-previously-imported-WarOfTheWorlds' `
  -UnityEditorRoot 'C:\Program Files\Unity\Hub\Editor\6000.5.0f1' `
  -OutputDirectory 'C:\path\to\offline-results'
```

The reference project's `Library/ScriptAssemblies` supplies third-party package DLLs only. Its `Assembly-CSharp`, `Sanghyuk`, and other project assemblies are excluded; the current project's real sources are compiled instead. The script also compares direct package versions against the current manifest and reports cache differences. No sources from another feature branch are compiled.

Checks:

- Project `.meta` GUID format/uniqueness and metadata coverage for every asset/directory.
- Serialized GUIDs against current project assets, cached packages, installed built-in packages, and Unity's reserved built-in GUIDs. Unresolved references already present in the base are warnings with full paths/lines; newly unresolved references fail.
- All existing scenes/prefabs against the specified base commit. Only explicit gameplay data keys (`hp`, `maxHp`, `playerHp`, `integrated`, `m_TagString`, `noBossCampaign`, `allowMissingBossTestSkip`) may change. Layout, hierarchy, colors, images, sizes, fonts, and other serialized data must remain identical.
- Actual source compilation in Editor, Windows release, and Windows development symbol configurations. Assembly Definition/Reference boundaries are retained. Empty `Jain`/`MyeongChan` definitions produce no DLL. Editor scripts outside assembly definitions compile separately. Dependency cycles, unknown assembly references, unsupported firstpass folders, or version-defined assembly symbols fail explicitly.
- If present, `CampaignLogicChecks.cs` runs only the actual pure managed save-validation/ranking rules from the freshly compiled project DLL under .NET 8. It does not construct MonoBehaviours, use JsonUtility, call save I/O, or start the Unity native engine.

Output includes `summary.json`, `summary.md`, assembly boundaries, unresolved GUID locations, exact compiler response files, per-assembly diagnostics, compiled validation DLLs, and managed logic-check results. An exit code of 1 means at least one check failed. Warnings and skipped checks remain visible in the report.

The script is scoped to this project's current assembly definitions and default external package references. It is not a general replacement for Unity's compiler, asset pipeline, or all possible Assembly Definition settings. Source generators, actual package reimport, importer/subasset fileIDs, a Unity player build, Play mode, manual controls, audio, visuals, and game balance still need the user's editor verification. Compilation and pure rule checks do not prove a full game flow works in Play mode.

Unity's Editor-folder assembly behavior is documented in [Introduction to assemblies](https://docs.unity.com/en-us/engine/6000.3/manual/programming-environment/script-compilation/assembly-definition-files/assembly-definitions-intro).
