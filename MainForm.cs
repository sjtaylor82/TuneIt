using System.Drawing;
using System.Windows.Forms.Automation;

namespace TuneIt;

internal sealed class MainForm : Form
{
    private readonly ComboBox instrumentChoice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ComboBox modeChoice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly CheckBox headphones = new() { Text = "&Listen during tone", AutoSize = true, Checked = true };
    private readonly CheckBox voiceNext = new() { Text = "Say “next” to continue", AutoSize = true, Enabled = false, Checked = true };
    private readonly RadioButton noteFeedback = new() { Text = "&Note and cents", AutoSize = true, Checked = true };
    private readonly RadioButton directionFeedback = new() { Text = "&Higher or lower", AutoSize = true };
    private readonly ComboBox notes = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly Button monitor = new() { Text = "&Start listening", AutoSize = true };
    private readonly Button next = new() { Text = "&Next string", AutoSize = true, Enabled = false };
    private readonly Button play = new() { Text = "&Play reference tone", AutoSize = true };
    private readonly NumericUpDown repeat = new() { Minimum = 2, Maximum = 30, Value = 7, Width = 70 };
    private readonly LiveLabel status = new() { Text = "Ready", AutoSize = true, MaximumSize = new Size(600, 0), BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(10) };
    private readonly Label reading = new() { Text = "Waiting to hear a string", AutoSize = true, Font = new Font(SystemFonts.DefaultFont.FontFamily, 12, FontStyle.Bold) };
    private readonly Label microphoneState = new() { Text = "Listening: Off", AutoSize = true, Font = new Font(SystemFonts.DefaultFont.FontFamily, 11, FontStyle.Bold) };
    private readonly Label voiceState = new() { Text = "Voice command: Off", AutoSize = true, Visible = false };
    private readonly AudioInput input = new();
    private readonly ReferenceTone tone = new();
    private readonly VoiceCommandListener voiceCommands = new();
    private NoteInfo[] tuning = Tunings.Guitar;
    private bool listening, playing, changingChecks, resumeListeningAfterTone;
    private int tutorialIndex;
    private int analysisBusy;
    private long lastAnalysisTicks;
    private long lastLevelUpdateTicks;
    private long lastSoundTicks;
    private long suppressPitchUntilTicks;
    private int noteArmed = 1;

    internal MainForm()
    {
        Text = "TuneIt — Accessible Guitar and Bass Tuner";
        ClientSize = new Size(690, 610); MinimumSize = new Size(620, 580); StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Segoe UI", 10); KeyPreview = true;
        AccessibleName = "TuneIt accessible tuner";

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 9, AutoScroll = true };
        root.RowStyles.Clear();
        var title = new Label { Text = "TuneIt", Font = new Font(Font.FontFamily, 20, FontStyle.Bold), AutoSize = true };
        var intro = new Label { Text = "Choose a string, then listen or play its tone.", AutoSize = true, MaximumSize = new Size(630, 0) };
        var instrument = new GroupBox { Text = "Instrument", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
        var instrumentFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var instrumentLabel = new Label { Text = "&Instrument:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        instrumentChoice.AccessibleName = "Instrument";
        instrumentChoice.Items.AddRange(["Guitar (6 strings)", "Bass (4 strings)"]);
        instrumentChoice.SelectedIndex = 0;
        instrumentFlow.Controls.AddRange([instrumentLabel, instrumentChoice]); instrument.Controls.Add(instrumentFlow);
        var modeGroup = new GroupBox { Text = "Tuning mode", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
        var modeFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var modeLabel = new Label { Text = "&Mode:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        modeChoice.AccessibleName = "Tuning mode";
        modeChoice.Items.AddRange(["One string", "Guided tuning"]);
        modeChoice.SelectedIndex = 0;
        modeFlow.Controls.AddRange([modeLabel, modeChoice]); modeGroup.Controls.Add(modeFlow);
        var noteGroup = new GroupBox { Text = "String", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) }; noteGroup.Controls.Add(notes);
        notes.AccessibleName = "String";
        var feedbackGroup = new GroupBox { Text = "Feedback", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
        var feedbackFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        feedbackFlow.Controls.AddRange([noteFeedback, directionFeedback]); feedbackGroup.Controls.Add(feedbackFlow);
        var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        var repeatLabel = new Label { Text = "&Repeat the reference tone every", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        repeatLabel.AccessibleDescription = "How many seconds before the reference tone repeats";
        repeat.AccessibleName = "Seconds before the reference tone repeats";
        var secondsLabel = new Label { Text = "seconds", AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        options.Controls.AddRange([voiceNext, headphones, repeatLabel, repeat, secondsLabel]);
        var controls = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top }; controls.Controls.AddRange([monitor, next, play]);
        var statusGroup = new GroupBox { Text = "Tuning result", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
        var statusFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill }; statusFlow.Controls.AddRange([microphoneState, voiceState, reading, status]); statusGroup.Controls.Add(statusFlow);
        // Do not set a fixed AccessibleName on changing labels: screen readers would
        // announce that name instead of the current result text.
        status.LiveSetting = AutomationLiveSetting.Off;
        var help = new Label { Text = "Shortcuts: Alt+L listen; Alt+P reference tone; Alt+I instrument; Alt+M mode; Alt+N next.", AutoSize = true, MaximumSize = new Size(630, 0) };
        root.Controls.Add(title); root.Controls.Add(intro); root.Controls.Add(instrument); root.Controls.Add(modeGroup); root.Controls.Add(noteGroup); root.Controls.Add(feedbackGroup); root.Controls.Add(options); root.Controls.Add(controls); root.Controls.Add(statusGroup); root.Controls.Add(help);
        Controls.Add(root);

        instrumentChoice.SelectedIndexChanged += (_, _) => ChangeTuning(instrumentChoice.SelectedIndex == 1 ? Tunings.Bass : Tunings.Guitar);
        notes.SelectedIndexChanged += (_, _) => StringChanged();
        monitor.Click += (_, _) => ToggleListening();
        play.Click += (_, _) => ToggleTone();
        next.Click += (_, _) => AdvanceTutorial();
        modeChoice.SelectedIndexChanged += (_, _) =>
        {
            next.Enabled = GuidedMode;
            voiceNext.Enabled = GuidedMode;
            notes.Enabled = !GuidedMode;
            if (GuidedMode)
            {
                tutorialIndex = 0;
                changingChecks = true;
                notes.SelectedIndex = 0;
                changingChecks = false;
                Interlocked.Exchange(ref noteArmed, 1);
                if (playing) StartTone();
            }
            UpdateVoiceCommands();
            // Do not fire a live-region message here. It would interrupt the
            // screen reader's announcement of the newly selected radio button.
            if (GuidedMode) status.Text = $"Ready: {Target.Name}";
        };
        repeat.ValueChanged += (_, _) => { if (playing) StartTone(); };
        headphones.CheckedChanged += (_, _) => HeadphoneModeChanged();
        voiceNext.CheckedChanged += (_, _) => UpdateVoiceCommands();
        voiceCommands.NextRecognized += () =>
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(() => { if (GuidedMode) AdvanceTutorial(); }); }
            catch (InvalidOperationException) { }
        };
        voiceCommands.SpeechStarted += () => Interlocked.Exchange(ref suppressPitchUntilTicks, Environment.TickCount64 + 1500);
        voiceCommands.Ready += () => SetVoiceState("Voice command: Ready");
        voiceCommands.Failed += message => SetVoiceState($"Voice command unavailable: {message}");
        input.SamplesAvailable += AnalyzeSamples;
        ChangeTuning(Tunings.Guitar);
    }

    private void UpdateVoiceCommands()
    {
        if (GuidedMode && voiceNext.Checked)
        {
            voiceState.Visible = true;
            voiceState.Text = "Voice command: Loading";
            try { voiceCommands.Start(); }
            catch (Exception ex) { voiceState.Text = $"Voice command unavailable: {ex.Message}"; }
        }
        else
        {
            voiceCommands.Stop();
            voiceState.Visible = false;
            voiceState.Text = "Voice command: Off";
        }
    }

    private void SetVoiceState(string message)
    {
        try
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(() => { if (GuidedMode && voiceNext.Checked) voiceState.Text = message; });
        }
        catch (InvalidOperationException) { }
    }

    private void ChangeTuning(NoteInfo[] selected)
    {
        tuning = selected; tutorialIndex = 0; changingChecks = true; notes.Items.Clear();
        foreach (var n in tuning) notes.Items.Add(n);
        notes.SelectedIndex = 0; changingChecks = false;
        if (playing) StartTone(); AnnouncePrompt();
    }

    private void StringChanged()
    {
        if (changingChecks) return;
        tutorialIndex = SelectedIndex();
        Interlocked.Exchange(ref noteArmed, 1);
        if (playing) StartTone();
        AnnouncePrompt();
    }

    private int SelectedIndex() => Math.Max(0, notes.SelectedIndex);

    private bool GuidedMode => modeChoice.SelectedIndex == 1;
    private NoteInfo Target => tuning[Math.Clamp(GuidedMode ? tutorialIndex : SelectedIndex(), 0, tuning.Length - 1)];

    private void ToggleListening()
    {
        try
        {
            if (!listening)
            {
                if (playing && !headphones.Checked) { SetStatus("Stop tone first, or use headphones"); return; }
                Interlocked.Exchange(ref noteArmed, 1);
                input.Start(); listening = true; monitor.Text = "&Stop listening"; microphoneState.Text = "Listening: On"; SetStatus($"Play {Target.Name}");
            }
            else { input.Stop(); listening = false; resumeListeningAfterTone = false; monitor.Text = "&Start listening"; microphoneState.Text = "Listening: Off"; SetStatus("Stopped"); }
        }
        catch (Exception ex) { SetStatus(ex.Message); }
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData == (Keys.Alt | Keys.L))
        {
            ToggleListening();
            return true;
        }
        if (keyData == (Keys.Alt | Keys.P))
        {
            ToggleTone();
            return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    private void AnalyzeSamples(short[] samples, int count, int rate)
    {
        voiceCommands.Feed(samples, count);
        var now = Environment.TickCount64;
        if (now < Interlocked.Read(ref suppressPitchUntilTicks)) return;
        double sum = 0;
        for (var i = 0; i < count; i += 8) sum += (double)samples[i] * samples[i];
        var level = Math.Sqrt(sum / Math.Max(1, count / 8));
        if (level < 120)
        {
            if (now - Interlocked.Read(ref lastSoundTicks) > 180) Interlocked.Exchange(ref noteArmed, 1);
        }
        else
        {
            Interlocked.Exchange(ref lastSoundTicks, now);
        }
        if (level >= 120 && now - Interlocked.Read(ref lastLevelUpdateTicks) >= 750)
        {
            Interlocked.Exchange(ref lastLevelUpdateTicks, now);
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(() => { if (listening) reading.Text = "Sound detected"; }); }
            catch (InvalidOperationException) { }
        }
        if (now - Interlocked.Read(ref lastAnalysisTicks) < 100 || Interlocked.CompareExchange(ref analysisBusy, 1, 0) != 0) return;
        Interlocked.Exchange(ref lastAnalysisTicks, now);
        _ = Task.Run(async () =>
        {
            try
            {
                var reducedCount = count / 2;
                var reduced = new short[reducedCount];
                for (var i = 0; i < reducedCount; i++) reduced[i] = samples[i * 2];
                var frequency = PitchDetector.Detect(reduced, reducedCount, rate / 2);
                if (frequency is not null && !IsDisposed && IsHandleCreated)
                {
                    // Give the one-word recognizer time to claim spoken "next"
                    // before presenting the voice as a musical pitch.
                    if (GuidedMode && voiceNext.Checked) await Task.Delay(250);
                    if (Environment.TickCount64 < Interlocked.Read(ref suppressPitchUntilTicks)) return;
                    try { BeginInvoke(() => ShowPitch(frequency.Value)); }
                    catch (InvalidOperationException) { }
                }
            }
            finally { Interlocked.Exchange(ref analysisBusy, 0); }
        });
    }

    private void ShowPitch(double frequency)
    {
        if (!listening) return;
        var exactMidi = 69 + 12 * Math.Log2(frequency / 440.0);
        var midi = (int)Math.Round(exactMidi);
        var targetCents = 1200 * Math.Log2(frequency / Target.Frequency);
        if (directionFeedback.Checked && Math.Abs(targetCents) > 650) return;
        if (Interlocked.Exchange(ref noteArmed, 0) == 0) return;
        string[] names = ["C", "C sharp", "D", "D sharp", "E", "F", "F sharp", "G", "G sharp", "A", "A sharp", "B"];
        var note = $"{names[(midi % 12 + 12) % 12]}{midi / 12 - 1}";
        string message;
        if (noteFeedback.Checked)
        {
            var cents = (int)Math.Round((exactMidi - midi) * 100);
            message = Math.Abs(cents) <= 2 ? $"{note}, in tune" : $"{note}, {Math.Abs(cents)} cents {(cents < 0 ? "low" : "high")}";
        }
        else
        {
            message = targetCents < -5 ? "Tune higher" : targetCents > 5 ? "Tune lower" : "In tune";
        }
        reading.Text = message;
        SetStatus(message, true);
    }

    private void SetStatus(string message, bool tuningResult = false)
    {
        if (tuningResult) status.Announce(message, true);
        else status.Text = message;
    }

    private void ToggleTone()
    {
        if (playing)
        {
            tone.Stop(); playing = false; play.Text = "&Play reference tone";
            if (resumeListeningAfterTone)
            {
                resumeListeningAfterTone = false;
                try { input.Start(); listening = true; monitor.Text = "&Stop listening"; microphoneState.Text = "Listening: On"; SetStatus("Listening resumed"); }
                catch (Exception ex) { SetStatus(ex.Message); }
            }
            else SetStatus("Tone stopped");
        }
        else StartTone();
    }

    private void StartTone()
    {
        if (listening && !headphones.Checked)
        {
            input.Stop(); listening = false; resumeListeningAfterTone = true; monitor.Text = "&Start listening"; microphoneState.Text = "Listening: Paused";
        }
        tone.Play(Target.Frequency, (int)repeat.Value, instrumentChoice.SelectedIndex == 1); playing = true; play.Text = "&Stop reference tone";
        SetStatus($"Playing {Target.Name}");
    }

    private void HeadphoneModeChanged()
    {
        if (!playing) return;
        if (headphones.Checked && resumeListeningAfterTone)
        {
            try { input.Start(); listening = true; resumeListeningAfterTone = false; monitor.Text = "&Stop listening"; microphoneState.Text = "Listening: On"; SetStatus("Listening resumed"); }
            catch (Exception ex) { SetStatus(ex.Message); }
        }
        else if (!headphones.Checked && listening)
        {
            input.Stop(); listening = false; resumeListeningAfterTone = true; monitor.Text = "&Start listening"; microphoneState.Text = "Listening: Paused"; SetStatus("Listening paused");
        }
    }
    private void AnnouncePrompt() { if (GuidedMode) SetStatus($"Tune {Target.Name}"); }
    private void AdvanceTutorial()
    {
        tutorialIndex = (tutorialIndex + 1) % tuning.Length; changingChecks = true;
        notes.SelectedIndex = tutorialIndex; changingChecks = false;
        if (playing) StartTone(); AnnouncePrompt();
    }

    protected override void OnFormClosed(FormClosedEventArgs e) { voiceCommands.Dispose(); input.Dispose(); tone.Dispose(); base.OnFormClosed(e); }
}
