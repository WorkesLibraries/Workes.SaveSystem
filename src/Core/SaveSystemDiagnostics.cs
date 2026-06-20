using System;

namespace Workes.SaveSystem
{
    internal static class SaveSystemDiagnostics
    {
        public static void LogWarning(string message)
        {
            Console.Error.WriteLine("[Workes.SaveSystem] Warning: " + message);
        }

        public static void LogError(string message)
        {
            Console.Error.WriteLine("[Workes.SaveSystem] Error: " + message);
        }
    }
}