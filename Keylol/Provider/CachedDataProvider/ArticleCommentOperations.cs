using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Keylol.Models.DAL;

namespace Keylol.Provider.CachedDataProvider
{
    /// <summary>
    /// ��������������ز���
    /// </summary>
    public class ArticleCommentOperations
    {
        private readonly KeylolDbContext _dbContext;
        private readonly RedisProvider _redis;

        /// <summary>
        /// ���� <see cref="ArticleCommentOperations"/>
        /// </summary>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="redis"><see cref="RedisProvider"/></param>
        public ArticleCommentOperations(KeylolDbContext dbContext, RedisProvider redis)
        {
            _dbContext = dbContext;
            _redis = redis;
        }

        private static string ArticleCommentCountCacheKey(string articleId) => $"article-comment-count:{articleId}";

        /// <summary>
        /// ��ȡָ�����µ�������
        /// </summary>
        /// <param name="articleId">���� ID</param>
        /// <exception cref="ArgumentNullException"><paramref name="articleId"/> Ϊ null</exception>
        /// <returns></returns>
        public async Task<int> GetArticleCommentCountAsync([NotNull] string articleId)
        {
            if (articleId == null)
                throw new ArgumentNullException(nameof(articleId));
            var cacheKey = ArticleCommentCountCacheKey(articleId);
            var redisDb = _redis.GetDatabase();
            var cacheResult = await redisDb.StringGetAsync(cacheKey);
            if (cacheResult.HasValue)
            {
                await redisDb.KeyExpireAsync(cacheKey, CachedDataProvider.DefaultTtl);
                return (int) cacheResult;
            }

            var commentCount = await _dbContext.ArticleComments
                .Where(c => c.ArticleId == articleId)
                .CountAsync();
            await redisDb.StringSetAsync(cacheKey, commentCount, CachedDataProvider.DefaultTtl);
            return commentCount;
        }
    }
}