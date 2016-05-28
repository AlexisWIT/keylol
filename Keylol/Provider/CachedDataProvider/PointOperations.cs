using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Keylol.Models.DAL;
using Keylol.Models.DTO;

namespace Keylol.Provider.CachedDataProvider
{
    /// <summary>
    /// ����ݵ���ز���
    /// </summary>
    public class PointOperations
    {
        private readonly KeylolDbContext _dbContext;
        private readonly RedisProvider _redis;

        /// <summary>
        /// ���� <see cref="PointOperations"/>
        /// </summary>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="redis"><see cref="RedisProvider"/></param>
        public PointOperations(KeylolDbContext dbContext, RedisProvider redis)
        {
            _dbContext = dbContext;
            _redis = redis;
        }

        private static string RatingCacheKey(string pointId) => $"point-rating:{pointId}";

        /// <summary>
        /// ��ȡָ���ݵ������
        /// </summary>
        /// <param name="pointId">�ݵ� ID</param>
        /// <exception cref="ArgumentNullException"><paramref name="pointId"/> Ϊ null</exception>
        /// <returns>�ݵ�����</returns>
        public async Task<PointRatingsDto> GetRatingsAsync([NotNull] string pointId)
        {
            if (pointId == null)
                throw new ArgumentNullException(nameof(pointId));

            var cacheKey = RatingCacheKey(pointId);
            var redisDb = _redis.GetDatabase();
            var cacheResult = await redisDb.StringGetAsync(cacheKey);
            if (cacheResult.HasValue)
            {
                await redisDb.KeyExpireAsync(cacheKey, CachedDataProvider.DefaultTtl);
                return RedisProvider.Deserialize<PointRatingsDto>(cacheResult);
            }

            // TODO
            var random = new Random();
            var rating = new PointRatingsDto
            {
                OneStarCount = random.Next(0, 100),
                TwoStarCount = random.Next(0, 100),
                ThreeStarCount = random.Next(0, 100),
                FourStarCount = random.Next(0, 100),
                FiveStarCount = random.Next(0, 100),
                TotalScore = random.Next(200, 1001),
                TotalCount = 100
            };
//                await redisDb.StringSetAsync(cacheKey, RedisProvider.Serialize(rating), DefaultTtl);
            return rating;
        }
    }
}