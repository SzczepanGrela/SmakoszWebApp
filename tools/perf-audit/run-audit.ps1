<#
.SYNOPSIS
    Runs Lighthouse performance audit on Smakosz.xyz across pages × profiles × runs, aggregates median metrics.

.DESCRIPTION
    Sub-projekt AS - Grupa 8 performance audit baseline. See:
    local design docs

.PARAMETER OutDir
    Where Lighthouse JSON/HTML reports + audit-summary.json land. Default: ./audit-output

.PARAMETER Cookie
    Optional auth cookie (raw "name=value; name2=value2" string from F12). Required for /me/profile + /admin runs.

.PARAMETER Pages
    Page name filter (e.g. -Pages home,search). Default: all 9 from pages.json

.PARAMETER Profiles
    Profile name filter (e.g. -Profiles mobile-cold). Default: all 3 from profiles.json

.PARAMETER RunsPerCombo
    Lighthouse runs per (page, profile) for median. Default: 3

.PARAMETER SmokeOnly
    Run a single home/mobile-cold combo and validate JSON parsing. Skip the full sweep.

.PARAMETER BaseUrl
    Origin to audit. Default: https://smakosz.xyz

.EXAMPLE
    pwsh tools/perf-audit/run-audit.ps1 -SmokeOnly

.EXAMPLE
    pwsh tools/perf-audit/run-audit.ps1 -Cookie "smakosz_auth=eyJ..."
#>

[CmdletBinding()]
param(
    [string]$OutDir = $null,
    [string]$Cookie = $null,
    [string[]]$Pages = $null,
    [string[]]$Profiles = $null,
    [int]$RunsPerCombo = 3,
    [switch]$SmokeOnly,
    [string]$BaseUrl = "https://smakosz.xyz"
)

$ErrorActionPreference = 'Stop'
# $PSScriptRoot is reliable inside the script body; param defaults sometimes evaluate before it's bound on PS 5.1.
$ScriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if (-not $OutDir) { $OutDir = Join-Path $ScriptRoot "audit-output" }
$TmpDir = Join-Path $ScriptRoot "tmp"
$null = New-Item -ItemType Directory -Force -Path $OutDir, $TmpDir

$AllPages    = Get-Content "$ScriptRoot/pages.json"    | ConvertFrom-Json
$AllProfiles = Get-Content "$ScriptRoot/profiles.json" | ConvertFrom-Json

if ($Pages)    { $AllPages    = $AllPages    | Where-Object { $Pages    -contains $_.name } }
if ($Profiles) { $AllProfiles = $AllProfiles | Where-Object { $Profiles -contains $_.name } }

# ----- Helper: pre-fetch slugs from /api/recommendations (trending dish + its restaurant) -----
function Invoke-SlugPrefetch {
    param([string]$BaseUrl)
    $slugs = @{}
    try {
        $resp = Invoke-RestMethod -Uri "$BaseUrl/api/recommendations" -Method GET -TimeoutSec 30
        $trending = $resp.data.trending
        if ($trending -and $trending.Count -gt 0) {
            if ($trending[0].slug)           { $slugs['trending'] = $trending[0].slug }
            if ($trending[0].restaurantSlug) { $slugs['topRated'] = $trending[0].restaurantSlug }
        }
    } catch {
        Write-Warning "Slug prefetch failed: $_"
    }
    return $slugs
}

# ----- Helper: validate auth cookie before auth-gated run -----
function Test-AuthCookie {
    param([string]$BaseUrl, [string]$Cookie)
    if (-not $Cookie) { return $false }
    $headers = @{ 'Cookie' = $Cookie }
    try {
        # Throws on non-2xx; treat any exception as cookie failure
        $resp = Invoke-WebRequest -Uri "$BaseUrl/api/me" -Method GET -Headers $headers -TimeoutSec 15 -UseBasicParsing
        return ($resp.StatusCode -eq 200)
    } catch {
        return $false
    }
}

# ----- Helper: resolve URL template with prefetched slugs -----
function Resolve-Url {
    param($Page, [hashtable]$Slugs, [string]$BaseUrl)
    $url = $Page.url
    if ($Page.slugFrom) {
        $slug = $Slugs[$Page.slugFrom]
        if (-not $slug) {
            Write-Warning "No slug for '$($Page.slugFrom)' on page '$($Page.name)' - skipping"
            return $null
        }
        $url = $url -replace '\{slug\}', $slug
    }
    # Cache-busting query string to force CF origin fetch
    $sep = if ($url -match '\?') { '&' } else { '?' }
    $bust = "perf-audit=$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
    return "$BaseUrl$url$sep$bust"
}

# ----- Helper: single Lighthouse run -----
function Invoke-Lighthouse {
    param(
        [string]$Url,
        [string]$ProfileName,
        [string[]]$ProfileFlags,
        [string]$OutputBasename,
        [string]$Cookie,
        [string]$ChromeUserDataDir,
        [switch]$SaveAssets
    )

    # Write extra-headers to a tmp JSON file (avoids shell-quoting hell across PS 5.1 / 7)
    $extraHeaders = @{ 'Cache-Control' = 'no-cache'; 'Pragma' = 'no-cache' }
    if ($Cookie) { $extraHeaders['Cookie'] = $Cookie }
    $headersFile = "$OutputBasename.headers.json"
    $extraHeaders | ConvertTo-Json -Compress | Out-File -FilePath $headersFile -Encoding ascii

    $chromeFlags = @('--headless=new', '--no-sandbox', '--disable-gpu')
    if ($ChromeUserDataDir) { $chromeFlags += "--user-data-dir=$ChromeUserDataDir" }
    $chromeFlagsStr = $chromeFlags -join ' '

    $lhArgs = @(
        $Url
        '--output=json,html'
        "--output-path=$OutputBasename"
        '--quiet'
        '--only-categories=performance'
        "--extra-headers=$headersFile"
        "--chrome-flags=$chromeFlagsStr"
    )
    foreach ($f in $ProfileFlags) { $lhArgs += $f }
    if ($SaveAssets) { $lhArgs += '--save-assets' }

    Write-Host "  lighthouse $($lhArgs -join ' ')" -ForegroundColor DarkGray
    & npx --yes lighthouse @lhArgs 2>&1 | Out-Null
    return ($LASTEXITCODE -eq 0)
}

# ----- Helper: warm-hop wrapper (first throw-away populates SW + browser cache) -----
function Invoke-LighthouseWarm {
    param(
        [string]$Url,
        [string]$ProfileName,
        [string[]]$ProfileFlags,
        [string]$OutputBasename,
        [string]$Cookie,
        [string]$PageName
    )
    $userDataDir = Join-Path $TmpDir "chrome-$PageName-warm"
    $null = New-Item -ItemType Directory -Force -Path $userDataDir

    # Hop 1: throw-away, populate cache
    $throwaway = Join-Path $TmpDir "throwaway-$PageName"
    Invoke-Lighthouse -Url $Url -ProfileName $ProfileName -ProfileFlags $ProfileFlags `
        -OutputBasename $throwaway -Cookie $Cookie -ChromeUserDataDir $userDataDir -SaveAssets | Out-Null

    # Hop 2: measured run, same user-data-dir -> cache reused
    return Invoke-Lighthouse -Url $Url -ProfileName $ProfileName -ProfileFlags $ProfileFlags `
        -OutputBasename $OutputBasename -Cookie $Cookie -ChromeUserDataDir $userDataDir
}

# ----- Helper: extract median metrics from a list of LH JSON files -----
function Get-LighthouseMedian {
    param([string[]]$JsonPaths)
    $metrics = @{
        perfScore   = @()
        lcp         = @()
        fcp         = @()
        tbt         = @()
        cls         = @()
        speedIndex  = @()
        ttfb        = @()
    }
    foreach ($p in $JsonPaths) {
        if (-not (Test-Path $p)) { continue }
        try {
            $lh = Get-Content $p -Raw | ConvertFrom-Json
            $metrics.perfScore  += [double]($lh.categories.performance.score * 100)
            $metrics.lcp        += [double]$lh.audits.'largest-contentful-paint'.numericValue
            $metrics.fcp        += [double]$lh.audits.'first-contentful-paint'.numericValue
            $metrics.tbt        += [double]$lh.audits.'total-blocking-time'.numericValue
            $metrics.cls        += [double]$lh.audits.'cumulative-layout-shift'.numericValue
            $metrics.speedIndex += [double]$lh.audits.'speed-index'.numericValue
            $metrics.ttfb       += [double]$lh.audits.'server-response-time'.numericValue
        } catch {
            Write-Warning "Failed to parse $p : $_"
        }
    }
    $median = @{}
    foreach ($k in $metrics.Keys) {
        $sorted = @($metrics[$k] | Sort-Object)
        if ($sorted.Count -eq 0) { $median[$k] = $null; continue }
        $mid = [int][Math]::Floor($sorted.Count / 2)
        $median[$k] = if ($sorted.Count % 2 -eq 1) { $sorted[$mid] } else { ($sorted[$mid - 1] + $sorted[$mid]) / 2 }
    }
    return $median
}

# ===== Main flow =====

Write-Host "==> Pre-fetching slugs from $BaseUrl"
$slugs = Invoke-SlugPrefetch -BaseUrl $BaseUrl
foreach ($k in $slugs.Keys) { Write-Host "    $k -> $($slugs[$k])" }

# Auth cookie validation - only if any selected page requires it
$needsAuth = ($AllPages | Where-Object { $_.requiresAuth }).Count -gt 0
if ($needsAuth) {
    if (-not $Cookie) {
        Write-Warning "Auth-gated pages selected but no -Cookie given - they will be skipped"
    } else {
        Write-Host "==> Validating auth cookie"
        if (-not (Test-AuthCookie -BaseUrl $BaseUrl -Cookie $Cookie)) {
            throw "Auth cookie failed /api/me HEAD check. Refresh JWT and re-run."
        }
        Write-Host "    cookie OK"
    }
}

# Smoke test: 1 run home/mobile-cold, validate JSON parses
if ($SmokeOnly) {
    Write-Host "==> Smoke test: home / mobile-cold / 1 run"
    $homePage = $AllPages | Where-Object { $_.name -eq 'home' } | Select-Object -First 1
    $mobileCold = $AllProfiles | Where-Object { $_.name -eq 'mobile-cold' } | Select-Object -First 1
    if (-not $homePage -or -not $mobileCold) { throw "Smoke test needs home + mobile-cold in selection" }

    $url = Resolve-Url -Page $homePage -Slugs $slugs -BaseUrl $BaseUrl
    $basename = Join-Path $OutDir "smoke-home-mobile-cold"
    $ok = Invoke-Lighthouse -Url $url -ProfileName $mobileCold.name -ProfileFlags $mobileCold.lighthouseFlags `
        -OutputBasename $basename -Cookie $null

    if (-not $ok) { throw "Lighthouse smoke run failed (exit $LASTEXITCODE)" }

    $jsonPath = "$basename.report.json"
    if (-not (Test-Path $jsonPath)) { throw "Expected JSON output not found at $jsonPath" }
    $median = Get-LighthouseMedian -JsonPaths @($jsonPath)
    if ($null -eq $median.perfScore) { throw "Smoke test: perfScore failed to parse from JSON" }
    if ($null -eq $median.lcp)       { throw "Smoke test: lcp failed to parse from JSON" }

    Write-Host ""
    Write-Host "==> Smoke test PASS" -ForegroundColor Green
    Write-Host "    perfScore=$([Math]::Round($median.perfScore, 1)) lcp=$([Math]::Round($median.lcp))ms fcp=$([Math]::Round($median.fcp))ms cls=$([Math]::Round($median.cls, 3))"
    exit 0
}

# Full sweep
$summary = @()
$totalCombos = $AllPages.Count * $AllProfiles.Count
$comboIdx = 0

foreach ($page in $AllPages) {
    foreach ($profile in $AllProfiles) {
        $comboIdx++
        $combo = "$($page.name) / $($profile.name)"
        Write-Host ""
        Write-Host "==> [$comboIdx/$totalCombos] $combo" -ForegroundColor Cyan

        if ($page.requiresAuth -and -not $Cookie) {
            Write-Host "    SKIP (auth required, no cookie)" -ForegroundColor Yellow
            continue
        }
        # Re-validate cookie just before each auth-gated run (handles long sweeps)
        if ($page.requiresAuth) {
            if (-not (Test-AuthCookie -BaseUrl $BaseUrl -Cookie $Cookie)) {
                throw "Auth cookie expired mid-sweep at combo $comboIdx ($combo). Refresh JWT and re-run -Pages from this point."
            }
        }

        $url = Resolve-Url -Page $page -Slugs $slugs -BaseUrl $BaseUrl
        if (-not $url) { continue }

        $jsonPaths = @()
        for ($r = 1; $r -le $RunsPerCombo; $r++) {
            $basename = Join-Path $OutDir "$($page.name)-$($profile.name)-run$r"
            $cookieForRun = if ($page.requiresAuth) { $Cookie } else { $null }

            Write-Host "    run $r/$RunsPerCombo"
            $ok = if ($profile.warmHop) {
                Invoke-LighthouseWarm -Url $url -ProfileName $profile.name -ProfileFlags $profile.lighthouseFlags `
                    -OutputBasename $basename -Cookie $cookieForRun -PageName $page.name
            } else {
                Invoke-Lighthouse -Url $url -ProfileName $profile.name -ProfileFlags $profile.lighthouseFlags `
                    -OutputBasename $basename -Cookie $cookieForRun
            }
            if ($ok) { $jsonPaths += "$basename.report.json" }
            else     { Write-Warning "    run $r failed" }
        }

        $median = Get-LighthouseMedian -JsonPaths $jsonPaths
        $summary += [PSCustomObject]@{
            page       = $page.name
            profile    = $profile.name
            url        = $url
            runsOk     = $jsonPaths.Count
            runsTotal  = $RunsPerCombo
            perfScore  = $median.perfScore
            lcpMs      = $median.lcp
            fcpMs      = $median.fcp
            tbtMs      = $median.tbt
            cls        = $median.cls
            speedIndexMs = $median.speedIndex
            ttfbMs     = $median.ttfb
        }
    }
}

$summaryPath = Join-Path $OutDir "audit-summary.json"
$summary | ConvertTo-Json -Depth 5 | Out-File -FilePath $summaryPath -Encoding utf8
Write-Host ""
Write-Host "==> Done. Summary written to $summaryPath" -ForegroundColor Green
Write-Host "    $($summary.Count) combos, $totalCombos expected"
