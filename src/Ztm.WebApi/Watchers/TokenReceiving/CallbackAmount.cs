using System;
using Ztm.Zcoin.NBitcoin.Exodus;

namespace Ztm.WebApi.Watchers.TokenReceiving
{
    public sealed class CallbackAmount
    {
        public PropertyAmount? Confirmed { get; set; }

        public PropertyAmount? Pending { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as CallbackAmount;

            if (other == null)
            {
                return false;
            }

            return other.Confirmed == Confirmed && other.Pending == Pending;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Confirmed, Pending);
        }

        public static bool operator==(CallbackAmount first, CallbackAmount second)
        {
            return ReferenceEquals(first, second) || (!ReferenceEquals(first, null) && first.Equals(second));
        }

        public static bool operator!=(CallbackAmount first, CallbackAmount second)
        {
            return !(first == second);
        }
    }
}
