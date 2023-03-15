using System.Collections.Generic;
using System.Linq;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using NHibernate;

namespace TryCodeAnalysis;

public class Example
{
    public static void Main(string args)
    {
        var msSqlConfiguration = MsSqlConfiguration.MsSql2008.ConnectionString("");

        var fluentConfiguration = Fluently.Configure().Database(msSqlConfiguration);
        var sessionFactory = fluentConfiguration.BuildSessionFactory();
        ISession session = sessionFactory.OpenSession();
        session.FlushMode = FlushMode.Commit;


        var entities = GetEntities(session);


        static List<MyEntity> GetEntities(ISession session)
        {
            var ids = new List<long>();
            return session.Query<MyEntity>()
                .Select(e => e)
                .Where(e => ids.Contains(e.Id))
                .ToList();
        }
    }

    internal class MyEntity
    {
        public long Id { get; set; }
    }
}
