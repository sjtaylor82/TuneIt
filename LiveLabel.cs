namespace TuneIt;

internal sealed class LiveLabel : Label
{
    internal void Announce(string message, bool directScreenReader = false)
    {
        Text = message;
        AccessibleName = message;
        if (directScreenReader && ScreenReaderOutput.TryAnnounce(message)) return;

        // NVDA and other readers use the standard Windows accessibility path.
        AccessibilityObject.RaiseAutomationNotification(
            System.Windows.Forms.Automation.AutomationNotificationKind.ActionCompleted,
            System.Windows.Forms.Automation.AutomationNotificationProcessing.ImportantMostRecent,
            message);
        AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
    }
}
