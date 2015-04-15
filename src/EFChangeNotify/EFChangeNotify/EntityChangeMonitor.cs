using System;
using System.Data.Entity;
using System.Linq.Expressions;
using System.Runtime.Caching;

namespace EFChangeNotify
{
    public class EntityChangeMonitor<TEntity, TDbContext> : ChangeMonitor
        where TDbContext : DbContext
        where TEntity : class
    {
        private EntityChangeNotifier<TEntity, TDbContext> _changeNotifier;

        private readonly string _uniqueId;

        public EntityChangeMonitor(Expression<Func<TEntity, bool>> query, TDbContext context)
        {       
            _uniqueId = Guid.NewGuid().ToString();
            _changeNotifier = new EntityChangeNotifier<TEntity, TDbContext>(query, context);

            _changeNotifier.Error += _changeNotifier_Error;
            _changeNotifier.Changed += _changeNotifier_Changed;

            InitializationComplete();
        }

        void _changeNotifier_Error(object sender, NotifierErrorEventArgs e)
        {
            OnChanged(null);
        }

        void _changeNotifier_Changed(object sender, EntityChangeEventArgs<TEntity> e)
        {
            OnChanged(e.Results);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_changeNotifier != null)
                {
                    _changeNotifier.Dispose();
                    _changeNotifier = null;
                }
            }
        }

        public override string UniqueId
        {
            get { return _uniqueId; }
        }
    }
}
