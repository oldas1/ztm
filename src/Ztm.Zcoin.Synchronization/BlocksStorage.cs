using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NBitcoin;
using Ztm.Configuration;
using Ztm.Data.Entity.Contexts;
using Ztm.Zcoin.NBitcoin;

namespace Ztm.Zcoin.Synchronization
{
    public class BlocksStorage : IBlocksStorage
    {
        readonly IMainDatabaseFactory db;
        readonly Network zcoinNetwork;

        public BlocksStorage(IConfiguration config, IMainDatabaseFactory db)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }

            this.db = db;
            this.zcoinNetwork = ZcoinNetworks.Instance.GetNetwork(config.GetZcoinSection().Network.Type);
        }

        public async Task AddAsync(ZcoinBlock block, int height, CancellationToken cancellationToken)
        {
            if (block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            if (height < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "The value is negative.");
            }

            block.Header.PrecomputeHash(invalidateExisting: true, lazily: false);

            using (var db = this.db.CreateDbContext())
            using (var dbtx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
            {
                var entity = ToEntity(block, height);

                // Do not insert transactions that already exists.
                var transactions = entity.Transactions.Select(t => t.TransactionHash).ToArray();
                var existed = await db.Transactions
                    .Where(t => transactions.Contains(t.Hash))
                    .ToDictionaryAsync(t => t.Hash, cancellationToken);

                foreach (var tx in entity.Transactions)
                {
                    if (existed.ContainsKey(tx.TransactionHash))
                    {
                        tx.Transaction = null;
                    }
                }

                // Add block.
                await db.Blocks.AddAsync(entity, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                dbtx.Commit();
            }
        }

        public async Task<(ZcoinBlock block, int height)> GetAsync(uint256 hash, CancellationToken cancellationToken)
        {
            Ztm.Data.Entity.Contexts.Main.Block data, previous;

            if (hash == null)
            {
                throw new ArgumentNullException(nameof(hash));
            }

            using (var db = this.db.CreateDbContext())
            using (await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken))
            {
                data = await db.Blocks.IncludeAll().SingleOrDefaultAsync(b => b.Hash == hash, cancellationToken);

                if (data == null)
                {
                    return (block: null, height: 0);
                }

                if (data.Height == 0)
                {
                    previous = null;
                }
                else
                {
                    previous = await db.Blocks.SingleAsync(b => b.Height == data.Height - 1, cancellationToken);
                }
            }

            return (block: ToDomain(data, previous), height: data.Height);
        }

        public async Task<ZcoinBlock> GetAsync(int height, CancellationToken cancellationToken)
        {
            Ztm.Data.Entity.Contexts.Main.Block data, previous;

            if (height < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "The value is negative.");
            }

            using (var db = this.db.CreateDbContext())
            {
                var rows = await db.Blocks
                    .IncludeAll()
                    .Where(b => b.Height == height || b.Height == height - 1)
                    .ToArrayAsync(cancellationToken);

                if (rows.Length == 0 || rows[0].Height != height)
                {
                    return null;
                }

                data = rows[0];
                previous = (rows.Length > 1) ? rows[1] : null;
            }

            return ToDomain(data, previous);
        }

        public async Task<ZcoinBlock> GetFirstAsync(CancellationToken cancellationToken)
        {
            Ztm.Data.Entity.Contexts.Main.Block data;

            using (var db = this.db.CreateDbContext())
            {
                data = await db.Blocks.IncludeAll().SingleOrDefaultAsync(e => e.Height == 0, cancellationToken);

                if (data == null)
                {
                    return null;
                }
            }

            return ToDomain(data);
        }

        public async Task<(ZcoinBlock block, int height)> GetLastAsync(CancellationToken cancellationToken)
        {
            Ztm.Data.Entity.Contexts.Main.Block data, previous;

            using (var db = this.db.CreateDbContext())
            {
                var rows = await db.Blocks
                    .IncludeAll()
                    .OrderByDescending(e => e.Height)
                    .Take(2)
                    .ToArrayAsync(cancellationToken);

                if (rows.Length == 0)
                {
                    return (block: null, height: 0);
                }

                data = rows[0];
                previous = (rows.Length > 1) ? rows[1] : null;
            }

            return (block: ToDomain(data, previous), height: data.Height);
        }

        public async Task RemoveLastAsync(CancellationToken cancellationToken)
        {
            using (var db = this.db.CreateDbContext())
            using (var dbtx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
            {
                // Remove block.
                var block = await db.Blocks
                    .Include(b => b.Transactions)
                    .ThenInclude(t => t.Transaction)
                    .ThenInclude(t => t.Blocks)
                    .OrderByDescending(b => b.Height)
                    .Take(1)
                    .SingleOrDefaultAsync(cancellationToken);

                if (block == null)
                {
                    return;
                }

                db.Blocks.Remove(block);

                // Remove referenced transactions if no other blocks referenced it.
                foreach (var transaction in block.Transactions.Select(t => t.Transaction))
                {
                    if (transaction.Blocks.Select(t => t.BlockHash).Distinct().Count() > 1)
                    {
                        continue;
                    }

                    db.Transactions.Remove(transaction);
                }

                await db.SaveChangesAsync(cancellationToken);
                dbtx.Commit();
            }
        }

        ZcoinBlock ToDomain(Ztm.Data.Entity.Contexts.Main.Block data, Ztm.Data.Entity.Contexts.Main.Block previous = null)
        {
            var block = ZcoinBlock.CreateBlock(this.zcoinNetwork);

            // Block properties.
            block.Header.Version = data.Version;
            block.Header.HashMerkleRoot = data.MerkleRoot;
            block.Header.BlockTime = DateTime.SpecifyKind(data.Time, DateTimeKind.Utc);
            block.Header.Bits = data.Bits;
            block.Header.Nonce = (uint)data.Nonce;

            if (previous != null)
            {
                block.Header.HashPrevBlock = previous.Hash;
            }

            block.Transactions = data.Transactions.Select(e =>
            {
                // Transaction properties.
                var tx = new ZcoinTransaction()
                {
                    Version = (uint)e.Transaction.Version,
                    LockTime = (uint)e.Transaction.LockTime
                };

                // Transaction outputs.
                foreach (var output in e.Transaction.Outputs)
                {
                    tx.Outputs.Add(new ZcoinTxOut()
                    {
                        ScriptPubKey = output.Script,
                        Value = output.Value
                    });
                }

                // Transaction inputs.
                foreach (var input in e.Transaction.Inputs)
                {
                    tx.Inputs.Add(new ZcoinTxIn()
                    {
                        Sequence = (uint)input.Sequence,
                        PrevOut = new OutPoint(input.OutputHash, (uint)input.OutputIndex),
                        ScriptSig = input.Script
                    });
                }

                return tx;
            }).Cast<Transaction>().ToList();

            return block;
        }

        Ztm.Data.Entity.Contexts.Main.Block ToEntity(ZcoinBlock block, int height)
        {
            var entity = new Ztm.Data.Entity.Contexts.Main.Block()
            {
                Height = height,
                Hash = block.GetHash(),
                Version = block.Header.Version,
                Bits = block.Header.Bits,
                Nonce = block.Header.Nonce,
                Time = block.Header.BlockTime.UtcDateTime,
                MerkleRoot = block.Header.HashMerkleRoot
            };

            // Transactions.
            var transactions = new Dictionary<uint256, Ztm.Data.Entity.Contexts.Main.Transaction>();

            for (int i = 0; i < block.Transactions.Count; i++)
            {
                Ztm.Data.Entity.Contexts.Main.Transaction tx;

                block.Transactions[i].PrecomputeHash(invalidateExisting: true, lazily: false);

                var hash = block.Transactions[i].GetHash();
                var blockTx = new Ztm.Data.Entity.Contexts.Main.BlockTransaction()
                {
                    BlockHash = block.GetHash(),
                    TransactionHash = hash,
                    Index = i,
                    Block = entity
                };

                if (!transactions.TryGetValue(hash, out tx))
                {
                    tx = ToEntity((ZcoinTransaction)block.Transactions[i]);
                    transactions.Add(hash, tx);

                    blockTx.Transaction = tx;
                    tx.Blocks.Add(blockTx);
                }

                entity.Transactions.Add(blockTx);
            }

            return entity;
        }

        Ztm.Data.Entity.Contexts.Main.Transaction ToEntity(ZcoinTransaction tx)
        {
            var entity = new Ztm.Data.Entity.Contexts.Main.Transaction()
            {
                Hash = tx.GetHash(),
                Version = tx.Version,
                LockTime = tx.LockTime
            };

            // Outputs.
            for (int i = 0; i < tx.Outputs.Count; i++)
            {
                var output = new Ztm.Data.Entity.Contexts.Main.Output()
                {
                    TransactionHash = entity.Hash,
                    Index = i,
                    Value = tx.Outputs[i].Value,
                    Script = tx.Outputs[i].ScriptPubKey,
                    Transaction = entity
                };

                entity.Outputs.Add(output);
            }

            // Inputs.
            for (int i = 0; i < tx.Inputs.Count; i++)
            {
                var input = new Ztm.Data.Entity.Contexts.Main.Input()
                {
                    TransactionHash = entity.Hash,
                    Index = i,
                    OutputHash = tx.Inputs[i].PrevOut.Hash,
                    OutputIndex = tx.Inputs[i].PrevOut.N,
                    Script = tx.Inputs[i].ScriptSig,
                    Sequence = tx.Inputs[i].Sequence,
                    Transaction = entity
                };

                entity.Inputs.Add(input);
            }

            return entity;
        }
    }
}