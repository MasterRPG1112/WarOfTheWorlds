#requires -Version 7.2
<#!
.SYNOPSIS
Compile the project's real C# sources and inspect serialized references without starting Unity.
.DESCRIPTION
Uses an installed Unity Roslyn compiler/managed reference assemblies and an existing package cache.
This does not import assets, run Play mode, run Unity tests, build a player, or prove visual/input behavior.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$UnityEditorRoot = 'C:\Program Files\Unity\Hub\Editor\6000.5.0f1',
    [Parameter(Mandatory)][string]$ReferenceProject,
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'work\offline-validation'),
    [string]$BaseRef = '5327764858af3b54aa2ea59533ae9a0adecc4a37',
    [string[]]$AllowedSerializedKeys = @('hp','maxHp','playerHp','integrated','m_TagString','noBossCampaign','allowMissingBossTestSkip'),
    [switch]$SkipCompile
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
$ReferenceProject = [IO.Path]::GetFullPath($ReferenceProject)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$utf8 = [Text.UTF8Encoding]::new($false)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$checks = [Collections.Generic.List[object]]::new()
$compileResults = [Collections.Generic.List[object]]::new()
$limitations = @(
    'Unity Editor, asset import, player build, Play mode, Unity tests, rendering, audio, keyboard and balancing were not executed.',
    'Offline C# compilation uses the installed Unity managed libraries and cached package DLLs; Unity import/compilation is still a manual confirmation.',
    'GUID resolution checks asset identity only, not arbitrary subasset fileIDs or successful importer behavior.',
    'C# compilation is not a gameplay test.'
)

function Write-JsonFile([string]$path, $value) {
    [IO.File]::WriteAllText($path,($value | ConvertTo-Json -Depth 12),$utf8)
}
function Add-Check([string]$name,[string]$status,$details) {
    $checks.Add([ordered]@{name=$name;status=$status;details=$details})
    Write-Host (('{0}: {1}' -f $status.ToUpperInvariant(),$name))
}
function Relative-Path([string]$path) { return [IO.Path]::GetRelativePath($ProjectRoot,$path).Replace('\','/') }
function Run-Git([string[]]$gitArguments) {
    $gitOutput = @(& git -C $ProjectRoot -c core.quotepath=false @gitArguments 2>&1)
    if($LASTEXITCODE -ne 0) { throw ('git failed: ' + ($gitOutput -join "`n")) }
    return $gitOutput
}
function Normalize-Lines([string]$content) { return $content.Replace("`r`n","`n").TrimEnd([char[]]"`r`n") }
function Read-MetaGuid([string]$path) {
    foreach($line in [IO.File]::ReadLines($path)) {
        if($line -match '^guid:\s*([0-9a-fA-F]{32})\s*$') { return $Matches[1].ToLowerInvariant() }
    }
    return $null
}

Push-Location $ProjectRoot
try {
    $head = (Run-Git @('rev-parse','HEAD')) -join ''
    $branch = (Run-Git @('branch','--show-current')) -join ''
    $baseFiles = @(Run-Git @('ls-tree','-r','--name-only',$BaseRef,'--','Assets'))
    $baseFileSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($path in $baseFiles) { [void]$baseFileSet.Add($path) }
    $localGuids = @{}
    $duplicateGuids = [Collections.Generic.List[object]]::new()
    $invalidMetas = [Collections.Generic.List[string]]::new()
    $metaFiles = @(Get-ChildItem (Join-Path $ProjectRoot 'Assets') -Filter '*.meta' -File -Recurse)
    foreach($file in $metaFiles) {
        $guid = Read-MetaGuid $file.FullName
        if(-not $guid) { $invalidMetas.Add((Relative-Path $file.FullName)); continue }
        if($localGuids.ContainsKey($guid)) { $duplicateGuids.Add(@{guid=$guid;first=$localGuids[$guid];second=(Relative-Path $file.FullName)}) }
        else { $localGuids[$guid] = Relative-Path $file.FullName }
    }
    Add-Check 'Project metadata GUID uniqueness' $(if($duplicateGuids.Count -eq 0 -and $invalidMetas.Count -eq 0){'pass'}else{'fail'}) @{metaCount=$metaFiles.Count;duplicates=@($duplicateGuids);invalid=@($invalidMetas)}

    $missingMetas = @(Get-ChildItem (Join-Path $ProjectRoot 'Assets') -Recurse | Where-Object { $_.Extension -ne '.meta' -and -not (Test-Path -LiteralPath ($_.FullName + '.meta')) } | ForEach-Object { Relative-Path $_.FullName })
    Add-Check 'Every Assets file and directory has metadata' $(if($missingMetas.Count -eq 0){'pass'}else{'fail'}) $missingMetas

    $cacheRoots = @((Join-Path $ReferenceProject 'Library\PackageCache'),(Join-Path $ProjectRoot 'Packages'),(Join-Path $UnityEditorRoot 'Editor\Data\Resources\PackageManager\BuiltInPackages')) | Where-Object { Test-Path -LiteralPath $_ }
    $externalGuids = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    if(Get-Command rg -ErrorAction SilentlyContinue) {
        # One batched scan avoids opening thousands of metadata files through PowerShell individually.
        $cachedGuidLines = @(& rg --no-ignore --hidden --no-heading -N --color never -g '*.meta' '^guid:\s*[0-9a-fA-F]{32}\s*$' @cacheRoots)
        if($LASTEXITCODE -gt 1) { throw 'Could not index cached package GUIDs.' }
        foreach($line in $cachedGuidLines) {
            if($line -match 'guid:\s*([0-9a-fA-F]{32})\s*$') { [void]$externalGuids.Add($Matches[1]) }
        }
    }
    else {
        foreach($cacheRoot in $cacheRoots) {
            foreach($file in Get-ChildItem -LiteralPath $cacheRoot -Filter '*.meta' -Recurse -File) {
                $guid = Read-MetaGuid $file.FullName
                if($guid) { [void]$externalGuids.Add($guid) }
            }
        }
    }
    $referenceExtensions = @('.unity','.prefab','.asset','.mat','.controller','.anim','.overridecontroller','.playable','.mixer','.rendertexture','.spriteatlas','.spriteatlasv2','.shadergraph','.vfx','.scenetemplate','.asmdef','.asmref')
    $referenceFiles = @(Get-ChildItem (Join-Path $ProjectRoot 'Assets'),(Join-Path $ProjectRoot 'ProjectSettings') -Recurse -File | Where-Object { $_.Extension.ToLowerInvariant() -in $referenceExtensions })
    $unresolved = [Collections.Generic.List[object]]::new()
    $resolvedReferenceCount = 0
    foreach($file in $referenceFiles) {
        $content = [IO.File]::ReadAllText($file.FullName)
        $relative = Relative-Path $file.FullName
        $baseContent = $null
        foreach($match in [regex]::Matches($content,'\bguid:\s*([0-9a-fA-F]{32})\b')) {
            $guid = $match.Groups[1].Value.ToLowerInvariant()
            if($guid -match '^0{16}[def]0{15}$|^0{32}$' -or $localGuids.ContainsKey($guid) -or $externalGuids.Contains($guid)) { $resolvedReferenceCount++; continue }
            if($null -eq $baseContent) {
                $baseContent = if($baseFileSet.Contains($relative)){(Run-Git @('show',($BaseRef + ':' + $relative))) -join "`n"}else{''}
            }
            $unresolved.Add([ordered]@{file=$relative;guid=$guid;line=(1 + [regex]::Matches($content.Substring(0,$match.Index),"`n").Count);presentInBase=$baseContent.Contains($guid)})
        }
    }
    $newUnresolved = @($unresolved | Where-Object { -not $_.presentInBase })
    $baselineUnresolved = @($unresolved | Where-Object presentInBase)
    Write-JsonFile (Join-Path $OutputDirectory 'unresolved-guid-references.json') @($unresolved)
    Add-Check 'Serialized asset GUID references' $(if($newUnresolved.Count -gt 0){'fail'}elseif($baselineUnresolved.Count -gt 0){'warning'}else{'pass'}) @{files=$referenceFiles.Count;resolvedOccurrences=$resolvedReferenceCount;cachedPackageGuids=$externalGuids.Count;newUnresolved=@($newUnresolved);baselineUnresolvedCount=$baselineUnresolved.Count;fullDetails='unresolved-guid-references.json'}

    $serializedFiles = @($baseFiles | Where-Object { $_ -match '\.(unity|prefab)$' })
    $unauthorizedChanges = [Collections.Generic.List[string]]::new()
    $authorizedChanges = [Collections.Generic.List[object]]::new()
    $unchangedCount = 0
    $allowedKeysPattern = ($AllowedSerializedKeys | ForEach-Object { [regex]::Escape($_) }) -join '|'
    $allowedLinePattern = '(?m)^(\s*(?:' + $allowedKeysPattern + '):)[^\r\n]*$'
    foreach($relative in $serializedFiles) {
        $fullPath = Join-Path $ProjectRoot $relative
        if(-not (Test-Path -LiteralPath $fullPath)) { $unauthorizedChanges.Add(($relative + ' [deleted]')); continue }
        $before = Normalize-Lines ((Run-Git @('show',($BaseRef + ':' + $relative))) -join "`n")
        $after = Normalize-Lines ([IO.File]::ReadAllText($fullPath))
        if($before -ceq $after) { $unchangedCount++; continue }
        $normalizedBefore = [regex]::Replace($before,$allowedLinePattern,'$1 <authorized gameplay value>')
        $normalizedAfter = [regex]::Replace($after,$allowedLinePattern,'$1 <authorized gameplay value>')
        if($normalizedBefore -cne $normalizedAfter) { $unauthorizedChanges.Add($relative) }
        else {
            $authorizedChanges.Add(@{file=$relative;before=@([regex]::Matches($before,$allowedLinePattern) | ForEach-Object Value);after=@([regex]::Matches($after,$allowedLinePattern) | ForEach-Object Value)})
        }
    }
    Add-Check 'Existing scene and prefab serialization preserved' $(if($unauthorizedChanges.Count -eq 0){'pass'}else{'fail'}) @{baseRef=$BaseRef;files=$serializedFiles.Count;unchanged=$unchangedCount;allowedGameplayKeys=$AllowedSerializedKeys;allowedChanges=@($authorizedChanges);unexpectedChanges=@($unauthorizedChanges)}

    $manifest = Get-Content (Join-Path $ProjectRoot 'Packages\manifest.json') -Raw | ConvertFrom-Json -AsHashtable
    $cacheVersions = @{}
    foreach($packageDirectory in Get-ChildItem (Join-Path $ReferenceProject 'Library\PackageCache') -Directory) {
        $packageJson = Join-Path $packageDirectory.FullName 'package.json'
        if(Test-Path -LiteralPath $packageJson) {
            $packageInfo = Get-Content $packageJson -Raw | ConvertFrom-Json -AsHashtable
            $cacheVersions[$packageInfo['name']] = $packageInfo['version']
        }
    }
    $versionDifferences = @($manifest['dependencies'].GetEnumerator() | Where-Object { -not $_.Key.StartsWith('com.unity.modules.') -and (-not $cacheVersions.ContainsKey($_.Key) -or $cacheVersions[$_.Key] -ne $_.Value) } | ForEach-Object { @{package=$_.Key;requested=$_.Value;cached=$cacheVersions[$_.Key]} })
    Add-Check 'Cached direct package versions match manifest' $(if($versionDifferences.Count -eq 0){'pass'}else{'warning'}) $versionDifferences

    if(-not $SkipCompile) {
        $editorData = Join-Path $UnityEditorRoot 'Editor\Data'
        $dotnet = Join-Path $editorData 'DotNetSdk\dotnet.exe'
        $compiler = Get-ChildItem (Join-Path $editorData 'DotNetSdk\sdk') -Filter 'csc.dll' -Recurse -File | Where-Object { $_.FullName -match 'Roslyn[\\/]bincore[\\/]csc\.dll$' } | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
        if(-not (Test-Path -LiteralPath $dotnet) -or -not $compiler) { throw 'Unity Roslyn compiler/runtime not found. Supply the UnityEditorRoot parameter.' }
        $referenceDlls = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach($file in Get-ChildItem (Join-Path $editorData 'NetStandard\ref\2.1.0'),(Join-Path $editorData 'NetStandard\compat\2.1.0\shims\netstandard'),(Join-Path $editorData 'NetStandard\compat\2.1.0\shims\netfx') -Filter '*.dll' -File) { if(-not $referenceDlls.ContainsKey($file.Name)) { $referenceDlls[$file.Name] = $file.FullName } }
        foreach($file in Get-ChildItem (Join-Path $editorData 'Managed\UnityEngine') -Filter '*.dll' -File) { $referenceDlls[$file.Name] = $file.FullName }
        foreach($file in Get-ChildItem (Join-Path $ReferenceProject 'Library\ScriptAssemblies') -Filter '*.dll' -File) {
            if($file.Name -notmatch '^(Assembly-CSharp(?:-firstpass|-Editor|-Editor-firstpass)?|Sanghyuk|Jain|MyeongChan)\.dll$') { $referenceDlls[$file.Name] = $file.FullName }
        }
        foreach($file in Get-ChildItem (Join-Path $ReferenceProject 'Library\PackageCache') -Filter 'nunit.framework.dll' -Recurse -File) { $referenceDlls[$file.Name] = $file.FullName; break }
        # Existing external DLLs are package references only; cached project assemblies are never used.
        $asmdefs = @{}
        $folderAssemblies = @{}
        $asmGuidNames = @{}
        foreach($file in Get-ChildItem (Join-Path $ProjectRoot 'Assets') -Filter '*.asmdef' -Recurse -File) {
            $definition = Get-Content $file.FullName -Raw | ConvertFrom-Json -AsHashtable
            $definition['_file'] = Relative-Path $file.FullName
            $definition['_sources'] = [Collections.Generic.List[string]]::new()
            $asmdefs[$definition['name']] = $definition
            $folderAssemblies[$file.DirectoryName] = $definition['name']
            $definitionGuid = Read-MetaGuid ($file.FullName + '.meta')
            if($definitionGuid) { $asmGuidNames[$definitionGuid] = $definition['name'] }
            [void]$referenceDlls.Remove(($definition['name'] + '.dll'))
        }
        foreach($file in Get-ChildItem (Join-Path $ProjectRoot 'Assets') -Filter '*.asmref' -Recurse -File) {
            $assemblyReference = (Get-Content $file.FullName -Raw | ConvertFrom-Json -AsHashtable)['reference']
            if($assemblyReference.StartsWith('GUID:')) { $assemblyReference = $asmGuidNames[$assemblyReference.Substring(5)] }
            if(-not $asmdefs.ContainsKey($assemblyReference)) { throw ('Unknown assembly reference in ' + $file.FullName) }
            $folderAssemblies[$file.DirectoryName] = $assemblyReference
        }
        $runtimeSources = [Collections.Generic.List[string]]::new()
        $editorSources = [Collections.Generic.List[string]]::new()
        foreach($file in Get-ChildItem (Join-Path $ProjectRoot 'Assets') -Filter '*.cs' -File -Recurse) {
            $owner = $null
            $ancestor = $file.Directory
            while($ancestor -and $ancestor.FullName.StartsWith((Join-Path $ProjectRoot 'Assets'),[StringComparison]::OrdinalIgnoreCase)) {
                if($folderAssemblies.ContainsKey($ancestor.FullName)) { $owner = $folderAssemblies[$ancestor.FullName]; break }
                $ancestor = $ancestor.Parent
            }
            if($owner) { $asmdefs[$owner]['_sources'].Add($file.FullName) }
            elseif((Relative-Path $file.FullName) -match '(^|/)Editor/') { $editorSources.Add($file.FullName) }
            elseif((Relative-Path $file.FullName) -match '^Assets/(Plugins|Standard Assets|Pro Standard Assets)/') { throw 'A predefined firstpass folder was found; extend this validator to preserve firstpass boundaries before compiling.' }
            else { $runtimeSources.Add($file.FullName) }
        }
        $baseSymbols = @('UNITY_6000_5_0','UNITY_6000_5','UNITY_6000','UNITY_6000_5_OR_NEWER','UNITY_6000_0_OR_NEWER','UNITY_2023_1_OR_NEWER','UNITY_2022_1_OR_NEWER','UNITY_2021_1_OR_NEWER','UNITY_2020_1_OR_NEWER','UNITY_2019_1_OR_NEWER','UNITY_2018_1_OR_NEWER','UNITY_2017_1_OR_NEWER','UNITY_5_3_OR_NEWER','UNITY_STANDALONE_WIN','UNITY_STANDALONE','UNITY_64','ENABLE_INPUT_SYSTEM','ENABLE_LEGACY_INPUT_MANAGER','ENABLE_PHYSICS','ENABLE_PHYSICS2D','NET_STANDARD','NET_STANDARD_2_0','NET_STANDARD_2_1','NETSTANDARD','NETSTANDARD2_1','CSHARP_7_OR_LATER','CSHARP_7_3_OR_NEWER')
        function Test-Constraint($constraint,[string[]]$symbols) {
            foreach($alternative in ($constraint -split '\|\|')) {
                $term = $alternative.Trim()
                if($term.StartsWith('!')) { if($term.Substring(1) -notin $symbols) { return $true } }
                elseif($term -in $symbols) { return $true }
            }
            return $false
        }
        function Invoke-SourceCompile([string]$configuration,[string]$assemblyName,[string[]]$sources,[string[]]$references,[string[]]$symbols,[bool]$allowUnsafe=$false) {
            $configurationOutput = Join-Path $OutputDirectory $configuration
            New-Item -ItemType Directory -Path $configurationOutput -Force | Out-Null
            $dllPath = Join-Path $configurationOutput ($assemblyName + '.dll')
            $rspPath = Join-Path $configurationOutput ($assemblyName + '.rsp')
            $compilerOptions = @('-nologo','-target:library','-nostdlib+','-langversion:9.0','-utf8output','-deterministic+','-warn:4',('-out:"' + $dllPath + '"'),('-define:' + ($symbols -join ';')))
            if($allowUnsafe) { $compilerOptions += '-unsafe+' }
            $compilerOptions += @($references | Sort-Object -Unique | ForEach-Object { '-reference:"' + $_ + '"' })
            $compilerOptions += @($sources | Sort-Object | ForEach-Object { '"' + $_ + '"' })
            [IO.File]::WriteAllLines($rspPath,$compilerOptions,$utf8)
            $compilerOutput = @(& $dotnet $compiler ('@' + $rspPath) 2>&1 | ForEach-Object { $_.ToString() })
            $compilerExit = $LASTEXITCODE
            [IO.File]::WriteAllText((Join-Path $configurationOutput ($assemblyName + '.log')),($compilerOutput -join "`n"),$utf8)
            $compileResults.Add([ordered]@{configuration=$configuration;assembly=$assemblyName;sources=$sources.Count;exitCode=$compilerExit;errors=@($compilerOutput | Where-Object { $_ -match ': error CS\d+' });warnings=@($compilerOutput | Where-Object { $_ -match ': warning CS\d+' });log=($configuration + '/' + $assemblyName + '.log')})
            Write-Host ('COMPILE {0}/{1}: {2} sources, exit {3}' -f $configuration,$assemblyName,$sources.Count,$compilerExit)
            return $(if($compilerExit -eq 0){$dllPath}else{$null})
        }
        $boundaries = [ordered]@{predefinedRuntime=$runtimeSources.Count;predefinedEditor=$editorSources.Count;asmdefs=@($asmdefs.Values | ForEach-Object { @{name=$_['name'];file=$_['_file'];sources=$_['_sources'].Count;includePlatforms=$_['includePlatforms'];excludePlatforms=$_['excludePlatforms'];references=$_['references']} })}
        Write-JsonFile (Join-Path $OutputDirectory 'assembly-boundaries.json') $boundaries
        foreach($configuration in @('Editor','PlayerRelease','PlayerDevelopment')) {
            $isEditor = $configuration -eq 'Editor'
            $symbols = @($baseSymbols)
            if($isEditor) { $symbols += @('UNITY_EDITOR','UNITY_EDITOR_WIN','UNITY_EDITOR_64','UNITY_INCLUDE_TESTS','DEBUG','TRACE','UNITY_ASSERTIONS') }
            elseif($configuration -eq 'PlayerDevelopment') { $symbols += @('DEVELOPMENT_BUILD','DEBUG','TRACE','UNITY_ASSERTIONS') }
            $externalReferences = @($referenceDlls.Values | Where-Object { $isEditor -or [IO.Path]::GetFileName($_) -notmatch '^(UnityEditor|.*\.Editor(?:\.|\.dll$))' })
            $builtAssemblies = @{}
            $remaining = [Collections.Generic.List[string]]::new()
            foreach($name in ($asmdefs.Keys | Sort-Object)) {
                $definition = $asmdefs[$name]
                $platform = if($isEditor){'Editor'}else{'WindowsStandalone64'}
                if($definition['_sources'].Count -eq 0) { continue }
                if($definition['includePlatforms'].Count -gt 0 -and $platform -notin $definition['includePlatforms']) { continue }
                if($platform -in $definition['excludePlatforms']) { continue }
                $excluded = $false
                foreach($constraint in $definition['defineConstraints']) { if(-not (Test-Constraint $constraint $symbols)) { $excluded=$true;break } }
                if(-not $excluded) { $remaining.Add($name) }
            }
            while($remaining.Count -gt 0) {
                $progress = $false
                foreach($name in @($remaining.ToArray())) {
                    $definition = $asmdefs[$name]
                    if($definition['versionDefines'].Count -gt 0) { throw ('Version-specific assembly defines need explicit support: ' + $name) }
                    $dependencyNames = @($definition['references'] | ForEach-Object { if($_.StartsWith('GUID:')){$asmGuidNames[$_.Substring(5)]}else{$_} })
                    if(@($dependencyNames | Where-Object { $_ -in $remaining }).Count -gt 0) { continue }
                    $asmReferences = @($externalReferences)
                    foreach($dependency in $dependencyNames) {
                        if($builtAssemblies.ContainsKey($dependency)) { $asmReferences += $builtAssemblies[$dependency] }
                        elseif($asmdefs.ContainsKey($dependency)) { throw ('Unavailable project assembly dependency: ' + $dependency) }
                    }
                    if($definition['noEngineReferences']) { $asmReferences = @($asmReferences | Where-Object { [IO.Path]::GetFileName($_) -notmatch '^Unity(?:Engine|Editor)' }) }
                    $built = Invoke-SourceCompile $configuration $name $definition['_sources'].ToArray() $asmReferences $symbols ([bool]$definition['allowUnsafeCode'])
                    if($built) { $builtAssemblies[$name] = $built }
                    [void]$remaining.Remove($name)
                    $progress = $true
                }
                if(-not $progress) { throw ('Assembly dependency cycle: ' + ($remaining -join ', ')) }
            }
            $predefinedReferences = @($externalReferences)
            foreach($name in $builtAssemblies.Keys) { if($asmdefs[$name]['autoReferenced'] -ne $false){$predefinedReferences += $builtAssemblies[$name]} }
            $runtimeDll = Invoke-SourceCompile $configuration 'Assembly-CSharp' $runtimeSources.ToArray() $predefinedReferences $symbols
            if($isEditor -and $runtimeDll -and $editorSources.Count -gt 0) {
                [void](Invoke-SourceCompile $configuration 'Assembly-CSharp-Editor' $editorSources.ToArray() ($predefinedReferences + $runtimeDll) $symbols)
            }
        }
        $compileFailures = @($compileResults | Where-Object { $_.exitCode -ne 0 })
        Add-Check 'Offline compilation of actual project C# sources' $(if($compileFailures.Count -eq 0){'pass'}else{'fail'}) @{compiler=$compiler;unityRoot=$UnityEditorRoot;referenceProject=$ReferenceProject;cachedProjectAssembliesUsed=$false;assemblies=@($compileResults)}

        $logicHarness = Join-Path $ProjectRoot 'Tools\CampaignLogicChecks.cs'
        $releaseAssembly = Join-Path $OutputDirectory 'PlayerRelease\Assembly-CSharp.dll'
        if($compileFailures.Count -eq 0 -and (Test-Path -LiteralPath $logicHarness)) {
            $logicOutput = Join-Path $OutputDirectory 'LogicChecks'
            New-Item -ItemType Directory -Path $logicOutput -Force | Out-Null
            $logicDll = Join-Path $logicOutput 'CampaignLogicChecks.dll'
            $logicRsp = Join-Path $logicOutput 'CampaignLogicChecks.rsp'
            $logicReferences = @($referenceDlls.Values | Where-Object { [IO.Path]::GetFileName($_) -notmatch '^(UnityEditor|.*\.Editor(?:\.|\.dll$))' }) + $releaseAssembly
            $logicOptions = @('-nologo','-target:exe','-nostdlib+','-langversion:9.0','-utf8output',('-out:"' + $logicDll + '"'))
            $logicOptions += @($logicReferences | Sort-Object -Unique | ForEach-Object { '-reference:"' + $_ + '"' })
            $logicOptions += '"' + $logicHarness + '"'
            [IO.File]::WriteAllLines($logicRsp,$logicOptions,$utf8)
            $logicCompileOutput = @(& $dotnet $compiler ('@' + $logicRsp) 2>&1 | ForEach-Object { $_.ToString() })
            $logicCompileExit = $LASTEXITCODE
            [IO.File]::WriteAllText((Join-Path $logicOutput 'compile.log'),($logicCompileOutput -join "`n"),$utf8)
            if($logicCompileExit -eq 0) {
                # Only the harness is executed. The project and Unity DLLs are passive managed references.
                Copy-Item -LiteralPath $releaseAssembly -Destination $logicOutput
                foreach($name in @('Sanghyuk.dll','Jain.dll','MyeongChan.dll')) {
                    $assemblyPath = Join-Path (Join-Path $OutputDirectory 'PlayerRelease') $name
                    if(Test-Path -LiteralPath $assemblyPath){Copy-Item -LiteralPath $assemblyPath -Destination $logicOutput}
                }
                foreach($name in @('UnityEngine.dll','UnityEngine.CoreModule.dll')) { Copy-Item -LiteralPath (Join-Path (Join-Path $editorData 'Managed\UnityEngine') $name) -Destination $logicOutput }
                Write-JsonFile (Join-Path $logicOutput 'CampaignLogicChecks.runtimeconfig.json') @{runtimeOptions=@{tfm='net8.0';framework=@{name='Microsoft.NETCore.App';version='8.0.0'};rollForward='LatestMinor'}}
                $logicRunOutput = @(& $dotnet $logicDll 2>&1 | ForEach-Object { $_.ToString() })
                $logicRunExit = $LASTEXITCODE
                [IO.File]::WriteAllText((Join-Path $logicOutput 'results.log'),($logicRunOutput -join "`n"),$utf8)
                $passedCases = @($logicRunOutput | Where-Object { $_.StartsWith('PASS ') }).Count
                Add-Check 'Pure managed campaign logic harness' $(if($logicRunExit -eq 0){'pass'}else{'fail'}) @{compileExitCode=$logicCompileExit;runExitCode=$logicRunExit;passedCases=$passedCases;output=$logicRunOutput;runtime='Microsoft.NETCore.App 8; no Unity executable or native engine loaded'}
            }
            else { Add-Check 'Pure managed campaign logic harness' 'fail' @{compileExitCode=$logicCompileExit;output=$logicCompileOutput} }
        }
        else { Add-Check 'Pure managed campaign logic harness' 'skipped' 'Project compile failed or Tools/CampaignLogicChecks.cs is absent.' }
    }
    else { Add-Check 'Offline compilation of actual project C# sources' 'skipped' 'SkipCompile parameter supplied.' }
}
catch {
    Add-Check 'Validator execution' 'fail' @{message=$_.Exception.Message;location=$_.ScriptStackTrace}
}
finally {
    Pop-Location
    $failedChecks = @($checks | Where-Object { $_.status -eq 'fail' })
    $report = [ordered]@{timestampUtc=[DateTime]::UtcNow.ToString('o');project=$ProjectRoot;branch=$branch;head=$head;baseRef=$BaseRef;success=($failedChecks.Count -eq 0);checks=@($checks);limitations=$limitations}
    Write-JsonFile (Join-Path $OutputDirectory 'summary.json') $report
    $summaryLines = @('# Offline validation', '', ('Result: ' + $(if($report.success){'PASS (within stated offline scope)'}else{'FAIL'})), ('Branch: ' + $branch), ('Head: ' + $head), ('Base: ' + $BaseRef), '')
    foreach($check in $checks) { $summaryLines += ('- ' + $check.status.ToUpperInvariant() + ': ' + $check.name) }
    $summaryLines += @('','Limits:') + @($limitations | ForEach-Object { '- ' + $_ })
    [IO.File]::WriteAllLines((Join-Path $OutputDirectory 'summary.md'),$summaryLines,$utf8)
}
if($failedChecks.Count -gt 0) { exit 1 }
