# TuneIt

An accessible Windows guitar and bass tuner built with .NET 8 Windows Forms. TuneIt works with screen readers and does not add its own text-to-speech voice.

## Use

1. Choose Guitar or Bass from the Instrument list.
2. Choose One string or Guided tuning.
3. Choose Note and cents or Higher or lower feedback.
4. Choose **Start listening**, then play the string clearly.
5. Optionally play the repeating plucked-string reference tone.

Guided tuning starts on the lowest string. Choose **Next string**, press Alt+N, or say “next” to continue. Offline voice recognition uses the bundled Vosk model in release packages.

Keyboard shortcuts:

- Alt+L: start or stop listening.
- Alt+P: play or stop the reference tone.
- Alt+N: next string in Guided tuning.
- Alt+I: instrument.
- Alt+M: tuning mode.

Tutorial mode steps through strings in standard low-to-high order. The UI has keyboard mnemonics, logical tab order, high-contrast-compatible native controls, a screen-reader live region, and optional Windows speech announcements.

## Build

```powershell
.\Get-SpeechModel.ps1
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The published executable is in `bin\Release\net8.0-windows\win-x64\publish\TuneIt.exe`.
