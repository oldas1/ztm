using System;
using System.Collections.Generic;
using System.Data;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Ztm.Data.Entity.Contexts;
using Ztm.Data.Entity.Contexts.Main;

namespace Ztm.WebApi
{
    public class SqlCallbackRepository : ICallbackRepository
    {
        readonly IMainDatabaseFactory db;

        public SqlCallbackRepository(IMainDatabaseFactory db)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }

            this.db = db;
        }

        public async Task<Callback> AddAsync(IPAddress registeringIp, Uri url, CancellationToken cancellationToken)
        {
            if (registeringIp == null)
            {
                throw new ArgumentNullException(nameof(registeringIp));
            }

            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            using (var db = this.db.CreateDbContext())
            {
                var callback = await db.WebApiCallbacks.AddAsync(new WebApiCallback()
                {
                    Id = Guid.NewGuid(),
                    RegisteredIp = registeringIp,
                    RegisteredTime = DateTime.UtcNow,
                    Url = url,
                }, cancellationToken);

                await db.SaveChangesAsync(cancellationToken);

                return ToDomain(callback.Entity);
            }
        }

        public async Task SetCompletedAsyc(Guid id, CancellationToken cancellationToken)
        {
            using (var db = this.db.CreateDbContext())
            using (var dbtx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken))
            {
                var update = await db.WebApiCallbacks.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

                if (update != null)
                {
                    update.Completed = true;

                    await db.SaveChangesAsync();
                    dbtx.Commit();
                }
            }
        }

        public async Task<Callback> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            using (var db = this.db.CreateDbContext())
            {
                var callback = await db.WebApiCallbacks.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
                return callback == null ? null : ToDomain(callback);
            }
        }

        public async Task<int> AddHistoryAsync(Guid id, CallbackResult result, CancellationToken cancellationToken)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            using (var db = this.db.CreateDbContext())
            {
                var history = await db.WebApiCallbackHistories.AddAsync(
                    new WebApiCallbackHistory
                    {
                        CallbackId = id,
                        Status = result.Status,
                        Data = JsonConvert.SerializeObject(result.Data),
                        InvokedTime = DateTime.UtcNow,
                    }
                );
                await db.SaveChangesAsync(cancellationToken);

                return history.Entity.Id;
            }
        }

        static WebApiCallback ToEntity(Callback callback)
        {
            return new WebApiCallback
            {
                Id = callback.Id,
                RegisteredIp = callback.RegisteredIp,
                RegisteredTime = callback.RegisteredTime.ToUniversalTime(),
                Completed = callback.Completed,
                Url = callback.Url,
            };
        }

        public static Callback ToDomain(WebApiCallback callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            return new Callback(
                callback.Id,
                callback.RegisteredIp,
                DateTime.SpecifyKind(callback.RegisteredTime, DateTimeKind.Utc),
                callback.Completed,
                callback.Url
            );
        }
    }
}