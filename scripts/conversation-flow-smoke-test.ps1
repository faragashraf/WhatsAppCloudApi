$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectDirectory = (Resolve-Path (Join-Path $repoRoot 'WhatsAppCloudApi.Api')).Path
$projectPath = Join-Path $projectDirectory 'WhatsAppCloudApi.Api.csproj'
$baseUrl = 'http://127.0.0.1:5086'
$healthUrl = "$baseUrl/health"
$stdoutFile = Join-Path $PSScriptRoot 'tmp-flow-run-stdout.log'
$stderrFile = Join-Path $PSScriptRoot 'tmp-flow-run-stderr.log'

$originalJwtKey = $env:JWT__KEY
$originalAspNetCoreEnvironment = $env:ASPNETCORE_ENVIRONMENT
$originalAspNetCoreUrls = $env:ASPNETCORE_URLS

function New-JwtSecret {
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $bytes = New-Object byte[] 64
        $rng.GetBytes($bytes)
        return [Convert]::ToBase64String($bytes)
    }
    finally {
        $rng.Dispose()
    }
}

function Wait-ApiReady {
    param(
        [Parameter(Mandatory = $true)]
        [System.Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)]
        [string]$Url
    )

    for ($attempt = 1; $attempt -le 60; $attempt++) {
        if ($Process.HasExited) {
            break
        }

        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) {
                return
            }
        }
        catch {
        }

        Start-Sleep -Seconds 2
    }

    if (Test-Path $stdoutFile) {
        Write-Output '===API_STDOUT==='
        Get-Content -Path $stdoutFile
    }

    if (Test-Path $stderrFile) {
        Write-Output '===API_STDERR==='
        Get-Content -Path $stderrFile
    }

    throw "API did not become ready at $Url."
}

function Invoke-ApiJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Method,
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [hashtable]$Headers = @{},
        $Body
    )

    $params = @{
        Method = $Method
        Uri = $Url
        Headers = $Headers
        TimeoutSec = 60
    }

    if ($PSBoundParameters.ContainsKey('Body')) {
        $params.ContentType = 'application/json'
        $params.Body = ($Body | ConvertTo-Json -Depth 30 -Compress)
    }

    try {
        return Invoke-RestMethod @params
    }
    catch [System.Net.WebException] {
        $responseBody = ''
        if ($_.Exception.Response -and $_.Exception.Response.GetResponseStream()) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            try {
                $responseBody = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }
        }

        throw "HTTP call failed for $Method $Url. Response: $responseBody"
    }
}

function Assert-ApiSuccess {
    param(
        [Parameter(Mandatory = $true)]
        $Response,
        [Parameter(Mandatory = $true)]
        [string]$Operation
    )

    if (-not $Response.success) {
        throw "$Operation failed. Message: $($Response.message)"
    }
}

function Assert-True {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Contains {
    param(
        [AllowNull()]
        [string]$Actual,
        [Parameter(Mandatory = $true)]
        [string]$Expected,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $actualValue = if ($null -eq $Actual) { '' } else { $Actual }
    if ($actualValue.IndexOf($Expected, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "$Message Actual: $actualValue"
    }
}

function As-Array {
    param($Value)

    if ($null -eq $Value) {
        return @()
    }

    return @($Value)
}

function Invoke-FlowSimulation {
    param(
        [Parameter(Mandatory = $true)]
        [long]$FlowId,
        [Parameter(Mandatory = $true)]
        [hashtable]$Headers,
        [Parameter(Mandatory = $true)]
        [string]$ContactNumber,
        [Parameter(Mandatory = $true)]
        [string]$ContactName,
        [string]$Content,
        [string]$SelectionId,
        [string]$SelectionTitle,
        [bool]$UsePublishedVersion,
        [bool]$DryRun = $true
    )

    $body = [ordered]@{
        contactNumber = $ContactNumber
        contactName = $ContactName
        messageType = 'text'
        content = $Content
        selectionId = $SelectionId
        selectionTitle = $SelectionTitle
        dryRun = $DryRun
        usePublishedVersion = $UsePublishedVersion
    }

    $response = Invoke-ApiJson -Method POST -Url "$baseUrl/api/conversation-flows/$FlowId/simulate" -Headers $Headers -Body $body
    Assert-ApiSuccess -Response $response -Operation "Flow simulation for $ContactNumber"
    return $response.data
}

if ([string]::IsNullOrWhiteSpace($env:JWT__KEY)) {
    $env:JWT__KEY = New-JwtSecret
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = $baseUrl

$apiProcess = Start-Process dotnet `
    -ArgumentList @('run', '--project', "`"$projectPath`"", '--no-build', '--no-launch-profile') `
    -WorkingDirectory $repoRoot `
    -PassThru `
    -WindowStyle Hidden `
    -RedirectStandardOutput $stdoutFile `
    -RedirectStandardError $stderrFile

try {
    Wait-ApiReady -Process $apiProcess -Url $healthUrl

    $timestamp = Get-Date -Format 'yyyyMMddHHmmss'
    $password = 'FlowTest!234'
    $companyCode = "FLOW$timestamp"
    $companyName = "Flow QA $timestamp"
    $adminEmail = "flow.qa.$timestamp@example.com"
    $agentEmail = "flow.agent.$timestamp@example.com"

    $registerResponse = Invoke-ApiJson -Method POST -Url "$baseUrl/api/auth/register-company" -Body ([ordered]@{
        companyName = $companyName
        companyCode = $companyCode
        companyEmail = "company.$timestamp@example.com"
        adminFullName = 'Flow QA Admin'
        adminEmail = $adminEmail
        password = $password
    })
    Assert-ApiSuccess -Response $registerResponse -Operation 'Company registration'

    $loginResponse = Invoke-ApiJson -Method POST -Url "$baseUrl/api/auth/login" -Body ([ordered]@{
        email = $adminEmail
        password = $password
    })
    Assert-ApiSuccess -Response $loginResponse -Operation 'Admin login'

    $authHeaders = @{
        Authorization = "Bearer $($loginResponse.data.tokens.accessToken)"
    }

    $createAgentResponse = Invoke-ApiJson -Method POST -Url "$baseUrl/api/users" -Headers $authHeaders -Body ([ordered]@{
        fullName = 'Flow QA Agent'
        email = $agentEmail
        password = $password
        role = 'Agent'
        isActive = $true
    })
    Assert-ApiSuccess -Response $createAgentResponse -Operation 'Agent creation'
    $agentId = [int]$createAgentResponse.data.companyUserId

    $flowDefinition = [ordered]@{
        nodes = @(
            [ordered]@{
                id = 'start'
                type = 'start'
                title = 'Start'
                x = 80
                y = 180
            },
            [ordered]@{
                id = 'welcome_message'
                type = 'message'
                title = 'Welcome message'
                x = 340
                y = 180
                bodyText = 'Hello {{customer_name}}, choose one of the available options.'
            },
            [ordered]@{
                id = 'intent_menu'
                type = 'menu'
                title = 'Intent menu'
                x = 640
                y = 180
                bodyText = 'Please choose Sales, Support, or Links.'
                menuPresentation = 'buttons'
                variableName = 'intent'
                invalidInputMessage = 'Please choose one of the available options.'
                options = @(
                    [ordered]@{ id = 'sales'; label = 'Sales' },
                    [ordered]@{ id = 'support'; label = 'Support' },
                    [ordered]@{ id = 'link'; label = 'Links' }
                )
            },
            [ordered]@{
                id = 'capture_request'
                type = 'capture_text'
                title = 'Capture request'
                x = 980
                y = 80
                bodyText = 'Please describe your request in one message.'
                variableName = 'customer_request'
                invalidInputMessage = 'Please send a text answer.'
            },
            [ordered]@{
                id = 'assign_sales'
                type = 'assign_agent'
                title = 'Assign sales agent'
                x = 980
                y = 220
                assignMode = 'specific'
                assignToUserId = $agentId
                updateContactOwner = $false
                assignReason = 'FLOW_TEST_HANDOFF'
            },
            [ordered]@{
                id = 'send_link'
                type = 'external_link'
                title = 'Send external link'
                x = 980
                y = 360
                bodyText = 'Complete your request from the following link:'
                url = 'https://example.com/apply'
            },
            [ordered]@{
                id = 'support_done'
                type = 'end'
                title = 'Support completed'
                x = 1320
                y = 80
                bodyText = 'Thanks {{customer_name}}, we captured your request: {{customer_request}}'
            },
            [ordered]@{
                id = 'link_done'
                type = 'end'
                title = 'Link completed'
                x = 1320
                y = 360
                bodyText = 'Continue using the link above. If you need help, reply again.'
            }
        )
        edges = @(
            [ordered]@{ id = 'start_default_welcome'; sourceNodeId = 'start'; targetNodeId = 'welcome_message'; sourceHandle = 'default'; label = '' },
            [ordered]@{ id = 'welcome_default_menu'; sourceNodeId = 'welcome_message'; targetNodeId = 'intent_menu'; sourceHandle = 'default'; label = '' },
            [ordered]@{ id = 'menu_sales_assign'; sourceNodeId = 'intent_menu'; targetNodeId = 'assign_sales'; sourceHandle = 'sales'; label = 'sales' },
            [ordered]@{ id = 'menu_support_capture'; sourceNodeId = 'intent_menu'; targetNodeId = 'capture_request'; sourceHandle = 'support'; label = 'support' },
            [ordered]@{ id = 'menu_link_external'; sourceNodeId = 'intent_menu'; targetNodeId = 'send_link'; sourceHandle = 'link'; label = 'link' },
            [ordered]@{ id = 'capture_default_end'; sourceNodeId = 'capture_request'; targetNodeId = 'support_done'; sourceHandle = 'default'; label = '' },
            [ordered]@{ id = 'link_default_end'; sourceNodeId = 'send_link'; targetNodeId = 'link_done'; sourceHandle = 'default'; label = '' }
        )
    }

    $createFlowResponse = Invoke-ApiJson -Method POST -Url "$baseUrl/api/conversation-flows" -Headers $authHeaders -Body ([ordered]@{
        name = "QA Flow $timestamp"
        description = 'Automated smoke-test flow'
        entryTriggerType = 'exact'
        entryTriggerValue = "start-flow-$timestamp"
        isActive = $true
        definition = $flowDefinition
    })
    Assert-ApiSuccess -Response $createFlowResponse -Operation 'Flow creation'
    $flowId = [long]$createFlowResponse.data.conversationFlowId

    $draftSimulation = Invoke-FlowSimulation -FlowId $flowId -Headers $authHeaders -ContactNumber '+15555000101' -ContactName 'Draft Tester' -Content "start-flow-$timestamp" -UsePublishedVersion $false
    $draftActions = As-Array $draftSimulation.actions
    Assert-True -Condition $draftSimulation.handled -Message 'Draft simulation should be handled.'
    Assert-True -Condition $draftSimulation.startedNewSession -Message 'Draft simulation should start a new session.'
    Assert-True -Condition ($draftSimulation.sessionStatus -eq 'WAITING_INPUT') -Message 'Draft simulation should stop on menu waiting for input.'
    Assert-True -Condition ($draftActions.Count -eq 2) -Message 'Draft simulation should emit two actions.'
    Assert-Contains -Actual $draftActions[0].preview -Expected 'Hello' -Message 'Draft simulation should send welcome text.'
    Assert-True -Condition ($draftActions[1].actionType -eq 'interactive') -Message 'Draft simulation should send an interactive menu.'

    $publishResponse = Invoke-ApiJson -Method POST -Url "$baseUrl/api/conversation-flows/$flowId/publish" -Headers $authHeaders
    Assert-ApiSuccess -Response $publishResponse -Operation 'Flow publish'

    $supportStart = Invoke-FlowSimulation -FlowId $flowId -Headers $authHeaders -ContactNumber '+15555000111' -ContactName 'Support Tester' -Content "start-flow-$timestamp" -UsePublishedVersion $true
    Assert-True -Condition ($supportStart.sessionStatus -eq 'WAITING_INPUT') -Message 'Support branch should pause on menu after start.'

    $supportSelection = Invoke-FlowSimulation -FlowId $flowId -Headers $authHeaders -ContactNumber '+15555000111' -ContactName 'Support Tester' -SelectionId 'support' -SelectionTitle 'Support' -UsePublishedVersion $true
    $supportSelectionActions = As-Array $supportSelection.actions
    Assert-True -Condition ($supportSelection.sessionStatus -eq 'WAITING_INPUT') -Message 'Support branch should wait for text input after choosing support.'
    Assert-Contains -Actual $supportSelectionActions[0].preview -Expected 'Please describe your request' -Message 'Support branch should prompt for customer text.'

    $supportCompletion = Invoke-FlowSimulation -FlowId $flowId -Headers $authHeaders -ContactNumber '+15555000111' -ContactName 'Support Tester' -Content 'Need invoice copy' -UsePublishedVersion $true
    $supportCompletionActions = As-Array $supportCompletion.actions
    Assert-True -Condition ($supportCompletion.sessionStatus -eq 'COMPLETED') -Message 'Support branch should complete after capturing text.'
    Assert-Contains -Actual $supportCompletionActions[0].preview -Expected 'Need invoice copy' -Message 'Support completion should include captured variable in final message.'

    $salesStart = Invoke-FlowSimulation -FlowId $flowId -Headers $authHeaders -ContactNumber '+15555000121' -ContactName 'Sales Tester' -Content "start-flow-$timestamp" -UsePublishedVersion $true
    Assert-True -Condition ($salesStart.sessionStatus -eq 'WAITING_INPUT') -Message 'Sales branch should pause on menu after start.'

    $salesHandoff = Invoke-FlowSimulation -FlowId $flowId -Headers $authHeaders -ContactNumber '+15555000121' -ContactName 'Sales Tester' -SelectionId 'sales' -SelectionTitle 'Sales' -UsePublishedVersion $true
    $salesHandoffActions = As-Array $salesHandoff.actions
    Assert-True -Condition ($salesHandoff.sessionStatus -eq 'HANDED_OFF') -Message 'Sales branch should end with handoff.'
    Assert-True -Condition ($salesHandoffActions[0].actionType -eq 'assign_agent') -Message 'Sales branch should simulate an assignment action.'

    $linkStart = Invoke-FlowSimulation -FlowId $flowId -Headers $authHeaders -ContactNumber '+15555000131' -ContactName 'Link Tester' -Content "start-flow-$timestamp" -UsePublishedVersion $true
    Assert-True -Condition ($linkStart.sessionStatus -eq 'WAITING_INPUT') -Message 'Link branch should pause on menu after start.'

    $linkCompletion = Invoke-FlowSimulation -FlowId $flowId -Headers $authHeaders -ContactNumber '+15555000131' -ContactName 'Link Tester' -SelectionId 'link' -SelectionTitle 'Links' -UsePublishedVersion $true
    $linkCompletionActions = As-Array $linkCompletion.actions
    Assert-True -Condition ($linkCompletion.sessionStatus -eq 'COMPLETED') -Message 'Link branch should complete after sending link.'
    Assert-Contains -Actual $linkCompletionActions[0].preview -Expected 'https://example.com/apply' -Message 'Link branch should send the external URL.'
    Assert-Contains -Actual $linkCompletionActions[1].preview -Expected 'Continue using the link above' -Message 'Link branch should send a completion message.'

    $invalidStart = Invoke-FlowSimulation -FlowId $flowId -Headers $authHeaders -ContactNumber '+15555000141' -ContactName 'Invalid Tester' -Content "start-flow-$timestamp" -UsePublishedVersion $true
    Assert-True -Condition ($invalidStart.sessionStatus -eq 'WAITING_INPUT') -Message 'Invalid-input branch should pause on menu after start.'

    $invalidReply = Invoke-FlowSimulation -FlowId $flowId -Headers $authHeaders -ContactNumber '+15555000141' -ContactName 'Invalid Tester' -SelectionId 'not-valid' -SelectionTitle 'Not Valid' -UsePublishedVersion $true
    $invalidActions = As-Array $invalidReply.actions
    Assert-True -Condition ($invalidReply.sessionStatus -eq 'WAITING_INPUT') -Message 'Invalid selection should keep the session waiting for input.'
    Assert-Contains -Actual $invalidActions[0].preview -Expected 'Please choose one of the available options' -Message 'Invalid selection should trigger fallback text.'

    [ordered]@{
        baseUrl = $baseUrl
        companyId = $loginResponse.data.companyId
        adminUserId = $loginResponse.data.userId
        agentUserId = $agentId
        flowId = $flowId
        draftSimulation = [ordered]@{
            sessionId = $draftSimulation.sessionId
            sessionStatus = $draftSimulation.sessionStatus
            actions = $draftActions
        }
        publishedChecks = [ordered]@{
            support = $supportCompletion
            sales = $salesHandoff
            links = $linkCompletion
            invalidInput = $invalidReply
        }
        status = 'passed'
    } | ConvertTo-Json -Depth 10
}
catch {
    if (Test-Path $stdoutFile) {
        Write-Output '===API_STDOUT==='
        Get-Content -Path $stdoutFile
    }

    if (Test-Path $stderrFile) {
        Write-Output '===API_STDERR==='
        Get-Content -Path $stderrFile
    }

    throw
}
finally {
    if ($null -eq $originalJwtKey) {
        Remove-Item Env:JWT__KEY -ErrorAction SilentlyContinue
    }
    else {
        $env:JWT__KEY = $originalJwtKey
    }

    if ($null -eq $originalAspNetCoreEnvironment) {
        Remove-Item Env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
    }
    else {
        $env:ASPNETCORE_ENVIRONMENT = $originalAspNetCoreEnvironment
    }

    if ($null -eq $originalAspNetCoreUrls) {
        Remove-Item Env:ASPNETCORE_URLS -ErrorAction SilentlyContinue
    }
    else {
        $env:ASPNETCORE_URLS = $originalAspNetCoreUrls
    }

    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
    }

    Remove-Item -Path $stdoutFile -ErrorAction SilentlyContinue
    Remove-Item -Path $stderrFile -ErrorAction SilentlyContinue
}
