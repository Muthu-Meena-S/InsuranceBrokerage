# ============================================================
#  Insurance Brokerage - Start All Services
#  Run this from the solution root: .\start-all.ps1
# ============================================================

$root = $PSScriptRoot

# ---- Helper: Kill any process using a given port ----
function Stop-Port {
    param([int]$Port)
    $pids = netstat -ano | Select-String ":$Port\s" | ForEach-Object {
        ($_ -split '\s+')[-1]
    } | Sort-Object -Unique
    foreach ($p in $pids) {
        if ($p -match '^\d+$' -and $p -ne '0') {
            try {
                Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
                Write-Host "  Killed process $p on port $Port" -ForegroundColor DarkGray
            } catch {}
        }
    }
}

Write-Host ""
Write-Host "Stopping any existing services on ports 3001, 5153, 5051..." -ForegroundColor DarkYellow
Stop-Port 3001
Stop-Port 5153
Stop-Port 5051
Start-Sleep -Seconds 2

Write-Host ""
Write-Host "Starting Insurance Brokerage Services..." -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. MCP Server (must start first)
Write-Host "[1/3] Starting MCP Server on http://localhost:3001 ..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", `
    "cd '$root\InsuranceBrokerage.McpServer'; Write-Host 'MCP Server Starting...' -ForegroundColor Yellow; dotnet run"

Start-Sleep -Seconds 5

# 2. REST API
Write-Host "[2/3] Starting REST API on http://localhost:5153 ..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", `
    "cd '$root\InsuranceBrokerage.Api'; Write-Host 'REST API Starting...' -ForegroundColor Green; dotnet run"

Start-Sleep -Seconds 3

# 3. AI Orchestrator (starts last, depends on MCP)
Write-Host "[3/3] Starting AI Orchestrator on http://localhost:5051 ..." -ForegroundColor Magenta
Start-Process powershell -ArgumentList "-NoExit", "-Command", `
    "cd '$root\InsuranceBrokerage.AI'; Write-Host 'AI Orchestrator Starting...' -ForegroundColor Magenta; dotnet run"

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "All 3 services launched!" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Chat UI  -->  http://localhost:5051"         -ForegroundColor White
Write-Host "  Swagger  -->  http://localhost:5153/swagger" -ForegroundColor White
Write-Host "  MCP      -->  http://localhost:3001/mcp"     -ForegroundColor White
Write-Host ""

