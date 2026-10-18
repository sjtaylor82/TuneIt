namespace TuneIt;

internal sealed class LiveLabel : Label
{
    private int announcementSequence;

    internal void Announce(string message, bool directScreenReader = false)
    {
        Text = message;
        AccessibleName = message;
        // Some JAWS paths discard a repeated notification when its text is
        // identical. Alternate harmless leading space so consecutive results
        // remain distinct without changing their pronunciation.
        var announcement = new string(' ', Interlocked.Increment(ref announcementSequence) % 2 + 1) + message;
        if (directScreenReader && ScreenReaderOutput.IsJawsRunning)
        {
            // Let JAWS finish processing the label change before sending the
            // result. Otherwise a short result such as "Higher" can be cut off.
            BeginInvoke(() =>
            {
                if (!ScreenReaderOutput.TryAnnounce(announcement)) AnnounceStandard(announcement);
            });
            return;
        }

        AnnounceStandard(announcement);
    }

    private void AnnounceStandard(string message)
    {
        // NVDA and other readers use the standard Windows accessibility path.
        AccessibilityObject.RaiseAutomationNotification(
            System.Windows.Forms.Automation.AutomationNotificationKind.ActionCompleted,
            System.Windows.Forms.Automation.AutomationNotificationProcessing.ImportantMostRecent,
            message);
        AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
    }
}
