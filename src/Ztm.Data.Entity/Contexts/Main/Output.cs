using System;
using NBitcoin;

namespace Ztm.Data.Entity.Contexts.Main
{
    public class Output : IComparable<Output>
    {
        public uint256 TransactionHash { get; set; }
        public long Index { get; set; }
        public long Value { get; set; }
        public Script Script { get; set; }

        public Transaction Transaction { get; set; }

        public int CompareTo(Output other)
        {
            if (other == null)
            {
                return 1;
            }

            // Check transaction hash.
            if (TransactionHash < other.TransactionHash)
            {
                return -1;
            }
            else if (TransactionHash > other.TransactionHash)
            {
                return 1;
            }

            // Check index.
            if (Index < other.Index)
            {
                return -1;
            }
            else if (Index > other.Index)
            {
                return 1;
            }

            return 0;
        }

        public override bool Equals(Object other)
        {
            var output = other as Output;
            return CompareTo(output) == 0;
        }

        public override int GetHashCode()
        {
            int hash = 0;

            hash ^= (TransactionHash != null) ? TransactionHash.GetHashCode() : 0;
            hash ^= Index.GetHashCode();
            hash ^= Value.GetHashCode();
            hash ^= (Script != null) ? Script.GetHashCode() : 0;
            hash ^= (Transaction != null) ? Transaction.GetHashCode() : 0;

            return hash;
        }
    }
}
