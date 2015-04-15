using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;

namespace EFChangeNotify
{
    internal static class EntityChangeNotifier
    {
        private static readonly List<string> _connectionStrings;
        private static readonly object _lockObj = new object();

        static EntityChangeNotifier()
        {
            _connectionStrings = new List<string>();

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                foreach (var cs in _connectionStrings)
                    SqlDependency.Stop(cs);
            };
        }

        internal static void AddConnectionString(string cs)
        {
            // ReSharper disable InconsistentlySynchronizedField
            if (!_connectionStrings.Contains(cs))
            // ReSharper restore InconsistentlySynchronizedField
            {
                lock (_lockObj)
                {
                    if (!_connectionStrings.Contains(cs))
                    {
                        SqlDependency.Start(cs);
                        _connectionStrings.Add(cs);
                    }
                }
            }
        }
    }

    public class EntityChangeNotifier<TEntity, TDbContext>
        : IDisposable
        where TDbContext : DbContext
        where TEntity : class
    {
        private DbContext _context;
        private readonly Expression<Func<TEntity, bool>> _query;
        private readonly string _connectionString;

        public event EventHandler<EntityChangeEventArgs<TEntity>> Changed;
        public event EventHandler<NotifierErrorEventArgs> Error;

        public EntityChangeNotifier(Expression<Func<TEntity, bool>> query, TDbContext context)
        {
            _context = context;
            _query = query;

            if (!string.IsNullOrWhiteSpace(_context.Database.Connection.ConnectionString))
                _connectionString = _context.Database.Connection.ConnectionString;

            if(string.IsNullOrWhiteSpace(_connectionString))
                throw new Exception("No connectionString provided!");

            EntityChangeNotifier.AddConnectionString(_connectionString);

            RegisterNotification();
        }

        private void RegisterNotification()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = GetCommand())
                {
                    command.Connection = connection;
                    connection.Open();

                    var sqlDependency = new SqlDependency(command);
                    sqlDependency.OnChange += _sqlDependency_OnChange;

                    // NOTE: You have to execute the command, or the notification will never fire.
                    using (command.ExecuteReader()) { }
                }
            }
        }

        public string GetSql()
        {
            var q = GetCurrent();

            return q.ToTraceString();
        }

        private SqlCommand GetCommand()
        {
            var q = GetCurrent();

            return q.ToSqlCommand();
        }

        private DbQuery<TEntity> GetCurrent()
        {
            var query = _context.Set<TEntity>().Where(_query) as DbQuery<TEntity>;

            return query;
        }

        private void _sqlDependency_OnChange(object sender, SqlNotificationEventArgs e)
        {
            if (_context == null)
                return;

            if (e.Type == SqlNotificationType.Subscribe || e.Info == SqlNotificationInfo.Error)
            {
                var args = new NotifierErrorEventArgs
                {
                    Reason = e,
                    Sql = GetCurrent().ToString()
                };

                OnError(args);
            }
            else
            {
                var args = new EntityChangeEventArgs<TEntity>
                {
                    Results = GetCurrent(),
                    ContinueListening = true
                };

                OnChanged(args);

                if (args.ContinueListening)
                {
                    RegisterNotification();
                }
            }
        }

        protected virtual void OnChanged(EntityChangeEventArgs<TEntity> e)
        {
            if (Changed != null)
            {
                Changed(this, e);
            }
        }

        protected virtual void OnError(NotifierErrorEventArgs e)
        {
            if (Error != null)
            {
                Error(this, e);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_context != null)
                {
                    _context.Dispose();
                    _context = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
