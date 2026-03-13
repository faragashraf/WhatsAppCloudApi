Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

$failures = [System.Collections.Generic.List[string]]::new()
$passes = [System.Collections.Generic.List[string]]::new()

function Add-Pass {
    param([string]$Message)
    $passes.Add($Message)
}

function Add-Failure {
    param([string]$Message)
    $failures.Add($Message)
}

function Get-LeafJsonKeys {
    param(
        [Parameter(Mandatory = $true)][object]$Node,
        [string]$Prefix = ''
    )

    $keys = [System.Collections.Generic.List[string]]::new()

    if ($null -eq $Node) {
        return $keys
    }

    if ($Node -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Node.PSObject.Properties) {
            $name = if ([string]::IsNullOrWhiteSpace($Prefix)) {
                $property.Name
            } else {
                "$Prefix.$($property.Name)"
            }

            if ($property.Value -is [System.Management.Automation.PSCustomObject]) {
                foreach ($nested in (Get-LeafJsonKeys -Node $property.Value -Prefix $name)) {
                    $keys.Add($nested)
                }
            } elseif ($property.Value -is [System.Collections.IEnumerable] -and -not ($property.Value -is [string])) {
                $index = 0
                foreach ($item in $property.Value) {
                    $itemName = "$name[$index]"
                    if ($item -is [System.Management.Automation.PSCustomObject]) {
                        foreach ($nested in (Get-LeafJsonKeys -Node $item -Prefix $itemName)) {
                            $keys.Add($nested)
                        }
                    } else {
                        $keys.Add($itemName)
                    }

                    $index++
                }
            } else {
                $keys.Add($name)
            }
        }

        return $keys
    }

    if ($Node -is [System.Collections.IEnumerable] -and -not ($Node -is [string])) {
        $index = 0
        foreach ($item in $Node) {
            $itemName = if ([string]::IsNullOrWhiteSpace($Prefix)) { "[$index]" } else { "$Prefix[$index]" }

            if ($item -is [System.Management.Automation.PSCustomObject]) {
                foreach ($nested in (Get-LeafJsonKeys -Node $item -Prefix $itemName)) {
                    $keys.Add($nested)
                }
            } else {
                $keys.Add($itemName)
            }

            $index++
        }

        return $keys
    }

    if (-not [string]::IsNullOrWhiteSpace($Prefix)) {
        $keys.Add($Prefix)
    }

    return $keys
}

function Test-Contains {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Pattern
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $content = Get-Content -LiteralPath $Path -Raw
    return $content.Contains($Pattern)
}

# 1) Bilingual support guardrails (Arabic + English)
$enPath = Join-Path $repoRoot 'frontend/public/i18n/en.json'
$arPath = Join-Path $repoRoot 'frontend/public/i18n/ar.json'
$languageServicePath = Join-Path $repoRoot 'frontend/src/app/core/services/language.service.ts'
$appRootPath = Join-Path $repoRoot 'frontend/src/app/app.ts'

if (-not (Test-Path -LiteralPath $enPath) -or -not (Test-Path -LiteralPath $arPath)) {
    Add-Failure "Missing translation files at frontend/public/i18n."
} else {
    try {
        $enJson = Get-Content -LiteralPath $enPath -Raw | ConvertFrom-Json
        $arJson = Get-Content -LiteralPath $arPath -Raw | ConvertFrom-Json

        $enKeys = @(Get-LeafJsonKeys -Node $enJson | Sort-Object -Unique)
        $arKeys = @(Get-LeafJsonKeys -Node $arJson | Sort-Object -Unique)

        $missingInArabic = @($enKeys | Where-Object { $_ -notin $arKeys })
        $missingInEnglish = @($arKeys | Where-Object { $_ -notin $enKeys })

        if ($missingInArabic.Count -gt 0 -or $missingInEnglish.Count -gt 0) {
            $missingArPreview = if ($missingInArabic.Count -gt 0) { $missingInArabic[0..([Math]::Min(2, $missingInArabic.Count - 1))] -join ', ' } else { 'none' }
            $missingEnPreview = if ($missingInEnglish.Count -gt 0) { $missingInEnglish[0..([Math]::Min(2, $missingInEnglish.Count - 1))] -join ', ' } else { 'none' }
            Add-Failure "Translation key mismatch. Missing in ar: $($missingInArabic.Count) [$missingArPreview]. Missing in en: $($missingInEnglish.Count) [$missingEnPreview]."
        } else {
            Add-Pass "Translation key parity is valid (ar/en)."
        }
    } catch {
        Add-Failure "Failed to parse translation files: $($_.Exception.Message)"
    }
}

if ((Test-Contains -Path $languageServicePath -Pattern "addLangs(['ar', 'en'])") -and
    (Test-Contains -Path $languageServicePath -Pattern "document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr'") -and
    (Test-Contains -Path $appRootPath -Pattern "inject(LanguageService)")) {
    Add-Pass "Language initialization + RTL/LTR wiring is present."
} else {
    Add-Failure "Language wiring check failed (LanguageService or root injection missing)."
}

# 2) Theme support guardrails
$themeServicePath = Join-Path $repoRoot 'frontend/src/app/core/services/theme.service.ts'
$appConfigPath = Join-Path $repoRoot 'frontend/src/app/app.config.ts'

if ((Test-Contains -Path $themeServicePath -Pattern "classList.toggle('dark'") -and
    (Test-Contains -Path $appConfigPath -Pattern "darkModeSelector: '.dark'") -and
    (Test-Contains -Path $appRootPath -Pattern "inject(ThemeService)")) {
    Add-Pass "Theme wiring is present (light/dark with persisted root initialization)."
} else {
    Add-Failure "Theme wiring check failed (ThemeService/app.config/app root injection)."
}

# 3) Database migrations guardrails (auto-apply on first backend startup)
$programPath = Join-Path $repoRoot 'WhatsAppCloudApi.Api/Program.cs'
$dbInitPath = Join-Path $repoRoot 'WhatsAppCloudApi.Api/Extensions/DatabaseInitializationExtensions.cs'
$migrationsDir = Join-Path $repoRoot 'WhatsAppCloudApi.Infrastructure/Data/Migrations'

if (Test-Contains -Path $programPath -Pattern "await app.InitializeDatabaseAsync(databaseInitializationOptions);") {
    Add-Pass "Backend startup calls database initialization."
} else {
    Add-Failure "Program.cs is missing automatic database initialization call."
}

if (Test-Contains -Path $dbInitPath -Pattern "await dbContext.Database.MigrateAsync(cancellationToken);") {
    Add-Pass "Database initialization applies EF migrations."
} else {
    Add-Failure "DatabaseInitializationExtensions is missing Database.MigrateAsync call."
}

if (Test-Path -LiteralPath $migrationsDir) {
    $migrationFiles = Get-ChildItem -Path $migrationsDir -Filter '*.cs' -File |
        Where-Object { $_.Name -match '^\d{14}_.+\.cs$' }
    if ($migrationFiles.Count -gt 0) {
        Add-Pass "Migration files detected ($($migrationFiles.Count))."
    } else {
        Add-Failure "No timestamped migration files were found in Data/Migrations."
    }
} else {
    Add-Failure "Migrations directory not found at WhatsAppCloudApi.Infrastructure/Data/Migrations."
}

Write-Host ''
Write-Host 'Delivery Guardrails Report'
Write-Host '--------------------------'
foreach ($pass in $passes) {
    Write-Host "[PASS] $pass"
}
foreach ($failure in $failures) {
    Write-Host "[FAIL] $failure"
}

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host "Guardrails failed: $($failures.Count) issue(s)."
    exit 1
}

Write-Host ''
Write-Host 'All guardrails passed.'
exit 0
