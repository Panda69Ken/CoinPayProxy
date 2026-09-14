using FreeSql;

namespace CoinPayProxy.Domain.Repositories
{
    public interface IRepository<TEntity> : IBaseRepository<TEntity> where TEntity : class
    {
    }

    public class Repository<TEntity> : BaseRepository<TEntity>, IRepository<TEntity> where TEntity : class
    {
        public readonly IFreeSql FreeSql;

        public Repository(IFreeSql fsql) : base(fsql)
        {
            FreeSql = fsql;
        }
    }
}
