$ErrorActionPreference = 'Stop'
$archive = Join-Path $PSScriptRoot 'vosk-model-small-en-us-0.15.zip'
$destination = Join-Path $PSScriptRoot 'speech-model'
$model = Join-Path $destination 'vosk-model-small-en-us-0.15'

if (Test-Path -LiteralPath $model) {
    Write-Output 'Speech model is already installed.'
    exit 0
}

Invoke-WebRequest -Uri 'https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip' -OutFile $archive
Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force
Remove-Item -LiteralPath $archive
Write-Output 'Speech model installed.'
