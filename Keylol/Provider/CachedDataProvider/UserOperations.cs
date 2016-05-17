using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Keylol.Models;
using Keylol.Models.DAL;

namespace Keylol.Provider.CachedDataProvider
{
    /// <summary>
    /// �����û���ز���
    /// </summary>
    public class UserOperations
    {
        private readonly SubscriptionOperations _subscription;
        private readonly RedisProvider _redis;
        private readonly KeylolDbContext _dbContext;

        /// <summary>
        /// ���� <see cref="UserOperations"/>
        /// </summary>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="redis"><see cref="RedisProvider"/></param>
        /// <param name="subscription"><see cref="SubscriptionOperations"/></param>
        public UserOperations(KeylolDbContext dbContext, RedisProvider redis, SubscriptionOperations subscription)
        {
            _subscription = subscription;
            _redis = redis;
            _dbContext = dbContext;
        }

        private static string UserSteamAppLibraryCacheKey(string userId) => $"user-steam-app-library:{userId}";

        /// <summary>
        /// �ж�ָ�������û��Ƿ��Ǻ��ѣ������ע��
        /// </summary>
        /// <param name="userId1">A �û� ID</param>
        /// <param name="userId2">B �û� ID</param>
        /// <returns>����Ǻ��ѣ����� <c>true</c></returns>
        public async Task<bool> IsFriend(string userId1, string userId2)
        {
            if (userId1 == null || userId2 == null)
                return false;
            return await _subscription.IsSubscribedAsync(userId1, userId2, SubscriptionTargetType.User)
                   && await _subscription.IsSubscribedAsync(userId2, userId1, SubscriptionTargetType.User);
        }

        /// <summary>
        /// �ж�ָ�� Steam App �Ƿ������
        /// </summary>
        /// <param name="userId">�û� ID</param>
        /// <param name="steamAppId">Steam App ID</param>
        /// <returns>�������⣬���� <c>true</c></returns>
        public async Task<bool> IsSteamAppInLibrary(string userId, int steamAppId)
        {
            if (userId == null)
                return false;
            var cacheKey = UserSteamAppLibraryCacheKey(userId);
            var redisDb = _redis.GetDatabase();
            if (!await redisDb.KeyExistsAsync(cacheKey))
            {
                foreach (var appId in await _dbContext.UserSteamGameRecords.Where(r => r.UserId == userId)
                    .Select(r => r.SteamAppId).ToListAsync())
                {
                    await redisDb.SetAddAsync(cacheKey, appId);
                }
            }
            await redisDb.KeyExpireAsync(cacheKey, CachedDataProvider.DefaultTtl);
            return await redisDb.SetContainsAsync(cacheKey, steamAppId);
        }

        /// <summary>
        /// ���ָ���û��� Steam App �⻺��
        /// </summary>
        /// <param name="userId">�û� ID</param>
        public async Task PurgeSteamAppLibraryCacheAsync([NotNull] string userId)
        {
            if (userId == null)
                throw new ArgumentNullException(nameof(userId));
            await _redis.GetDatabase().KeyDeleteAsync(UserSteamAppLibraryCacheKey(userId));
        }
    }
}