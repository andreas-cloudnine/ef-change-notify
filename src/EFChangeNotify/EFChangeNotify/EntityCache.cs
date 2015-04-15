using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Caching;

namespace EFChangeNotify
{
    public class EntityCache<TEntity, TDbContext>
        : IDisposable
        where TDbContext : DbContext
        where TEntity : class
    {
        private TDbContext _context;
        private readonly Expression<Func<TEntity, bool>> _query;
        private readonly IEntityCacheLogger _entityCacheLogger;

        private readonly string _cacheKey = Guid.NewGuid().ToString();

        public EntityCache(Expression<Func<TEntity, bool>> query, IEntityCacheLogger entityCacheLogger, TDbContext context)
        {
            _context = context;
            _query = query;
            _entityCacheLogger = entityCacheLogger;
        }

        private IEnumerable<TEntity> GetCurrent()
        {
            var query = _context.Set<TEntity>().Where(_query);

            return query;
        }

        private IEnumerable<TEntity> GetResults()
        {
            List<TEntity> value = MemoryCache.Default[_cacheKey] as List<TEntity>;

            if (value == null)
            {
                value = GetCurrent().ToList();

                var changeMonitor = new EntityChangeMonitor<TEntity, TDbContext>(_query, _context);

                CacheItemPolicy policy = new CacheItemPolicy();

                policy.ChangeMonitors.Add(changeMonitor);

                MemoryCache.Default.Add(_cacheKey, value, policy);

                if (_entityCacheLogger != null)
                    _entityCacheLogger.Log(string.Format("Using database to get {0}", _cacheKey));
            }
            else
            {
                if (_entityCacheLogger != null)
                    _entityCacheLogger.Log(string.Format("Using cache to get {0}", _cacheKey));
            }

            return value;
        }

        public IEnumerable<TEntity> Results
        {
            get
            {
                return GetResults();
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
