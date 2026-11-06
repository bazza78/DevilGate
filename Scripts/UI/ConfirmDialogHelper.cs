using Godot;

namespace DevilGate.Scripts;

/// <summary>
/// Small helper for native yes/no confirmations. Works on any screen —
/// unlike the in-world modal stack it does not need an InteractionController.
/// </summary>
public static class ConfirmDialogHelper
{
    /// <summary>Show a centred confirmation dialog; onConfirm runs only on Yes.</summary>
    public static void Show(Node host, string title, string message, Action onConfirm)
    {
        var dialog = new ConfirmationDialog
        {
            Title = title,
            DialogText = message,
            OkButtonText = "Yes",
            CancelButtonText = "No",
        };
        dialog.Confirmed += () =>
        {
            dialog.QueueFree();
            onConfirm();
        };
        dialog.Canceled += () => dialog.QueueFree();
        dialog.CloseRequested += () => dialog.QueueFree();
        host.AddChild(dialog);
        dialog.PopupCentered();
    }
}
