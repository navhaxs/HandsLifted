using System.Runtime.InteropServices;

namespace HandsLiftedApp.Common
{
    public static class Caffeine
    {
        [Flags]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
            // Legacy flag, should not be used.
            // ES_USER_PRESENT = 0x00000004
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        public static void KeepAwake(bool keepAwake)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var executionState = EXECUTION_STATE.ES_CONTINUOUS;

            if (keepAwake)
            {
                executionState |= EXECUTION_STATE.ES_DISPLAY_REQUIRED;
            }

            SetThreadExecutionState(executionState);
        }
    }
}