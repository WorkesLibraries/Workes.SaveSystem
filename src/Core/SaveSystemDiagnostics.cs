namespace Workes.SaveSystem
{
    internal sealed class SaveSystemDiagnostics
    {
        private readonly System.Action<string>? _warningSink;

        public SaveSystemDiagnostics(System.Action<string>? warningSink)
        {
            _warningSink = warningSink;
        }

        public void LogWarning(string message)
        {
            _warningSink?.Invoke(message);
        }
    }
}
