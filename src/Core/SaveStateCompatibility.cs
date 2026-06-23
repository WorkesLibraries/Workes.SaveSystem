using System;

namespace Workes.SaveSystem
{
    internal static class SaveStateCompatibility
    {
        public static bool CanAcceptNull(Type stateType)
        {
            if (stateType == null)
                throw new ArgumentNullException(nameof(stateType));

            return !stateType.IsValueType || Nullable.GetUnderlyingType(stateType) != null;
        }

        public static bool IsCompatibleState(Type stateType, object? state)
        {
            if (stateType == null)
                throw new ArgumentNullException(nameof(stateType));

            if (state == null)
                return CanAcceptNull(stateType);

            var compatibleType = Nullable.GetUnderlyingType(stateType) ?? stateType;
            return compatibleType.IsInstanceOfType(state);
        }
    }
}
