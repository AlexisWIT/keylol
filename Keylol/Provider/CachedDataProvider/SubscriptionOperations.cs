using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Keylol.Models;
using Keylol.Models.DAL;
using Keylol.Utilities;

namespace Keylol.Provider.CachedDataProvider
{
    /// <summary>
    /// ��������ز���
    /// </summary>
    public class SubscriptionOperations
    {
        private readonly KeylolDbContext _dbContext;
        private readonly RedisProvider _redis;

        private static string UserSubscribedTargetsCacheKey(string userId) => $"user-subscribed-targets:{userId}";

        private static string UserSubscribedTargetCacheValue(string targetId, SubscriptionTargetType targetType) =>
            $"{targetType.ToString().ToCase(NameConventionCase.PascalCase, NameConventionCase.DashedCase)}:{targetId}";

        private static string TargetSubscriberCountCacheKey(string targetId, SubscriptionTargetType targetType) =>
            $"subscriber-count:{targetType.ToString().ToCase(NameConventionCase.PascalCase, NameConventionCase.DashedCase)}:{targetId}";

        /// <summary>
        /// ���� <see cref="SubscriptionOperations"/>
        /// </summary>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="redis"><see cref="RedisProvider"/></param>
        public SubscriptionOperations(KeylolDbContext dbContext, RedisProvider redis)
        {
            _dbContext = dbContext;
            _redis = redis;
        }

        private async Task<string> InitUserSubscribedTargetsAsync([NotNull] string userId)
        {
            var cacheKey = UserSubscribedTargetsCacheKey(userId);
            var redisDb = _redis.GetDatabase();
            if (!await redisDb.KeyExistsAsync(cacheKey))
            {
                foreach (var subscription in await _dbContext.Subscriptions.Where(s => s.SubscriberId == userId)
                    .Select(s => new {s.TargetId, s.TargetType}).ToListAsync())
                {
                    await redisDb.SetAddAsync(cacheKey,
                        UserSubscribedTargetCacheValue(subscription.TargetId, subscription.TargetType));
                }
            }
            await redisDb.KeyExpireAsync(cacheKey, CachedDataProvider.DefaultTtl);
            return cacheKey;
        }

        /// <summary>
        /// �ж�ָ���û��Ƿ��Ĺ�ָ��Ŀ��
        /// </summary>
        /// <param name="userId">�û� ID</param>
        /// <param name="targetId">Ŀ�� ID</param>
        /// <param name="targetType">Ŀ������</param>
        /// <returns>����û����Ĺ������� <c>true</c></returns>
        public async Task<bool> IsSubscribedAsync(string userId, string targetId, SubscriptionTargetType targetType)
        {
            if (userId == null || targetId == null)
                return false;
            var cacheKey = await InitUserSubscribedTargetsAsync(userId);
            return await _redis.GetDatabase()
                .SetContainsAsync(cacheKey, UserSubscribedTargetCacheValue(targetId, targetType));
        }

        /// <summary>
        /// ��ȡָ���û��Ķ�����������ע������
        /// </summary>
        /// <param name="userId">�û� ID</param>
        /// <returns>�û��Ķ�������</returns>
        public async Task<long> GetSubscriptionCountAsync([NotNull] string userId)
        {
            var cacheKey = await InitUserSubscribedTargetsAsync(userId);
            return await _redis.GetDatabase().SetLengthAsync(cacheKey);
        }

        /// <summary>
        /// ��ȡָ���û��ĺ��ѣ��໥��ע������
        /// </summary>
        /// <param name="userId">�û� ID</param>
        /// <returns>�û��ĺ�������</returns>
        public async Task<long> GetFriendCountAsync([NotNull] string userId)
        {
            var cacheKey = await InitUserSubscribedTargetsAsync(userId);
            long count = 0;
            foreach (var member in await _redis.GetDatabase().SetMembersAsync(cacheKey))
            {
                var parts = ((string) member).Split(':');
                var type = parts[0].ToCase(NameConventionCase.DashedCase, NameConventionCase.PascalCase)
                    .ToEnum<SubscriptionTargetType>();
                if (type == SubscriptionTargetType.User &&
                    await IsSubscribedAsync(parts[1], userId, SubscriptionTargetType.User))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// ��ȡָ��Ŀ��Ķ���������
        /// </summary>
        /// <param name="targetId">Ŀ�� ID</param>
        /// <param name="targetType">Ŀ������</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetId"/> Ϊ null</exception>
        /// <returns>Ŀ��Ķ���������</returns>
        public async Task<long> GetSubscriberCountAsync([NotNull] string targetId, SubscriptionTargetType targetType)
        {
            if (targetId == null)
                throw new ArgumentNullException(nameof(targetId));
            var cacheKey = TargetSubscriberCountCacheKey(targetId, targetType);
            var redisDb = _redis.GetDatabase();
            var cacheResult = await redisDb.StringGetAsync(cacheKey);
            if (cacheResult.HasValue)
            {
                await redisDb.KeyExpireAsync(cacheKey, CachedDataProvider.DefaultTtl);
                return (long) cacheResult;
            }

            var subscriberCount = await _dbContext.Subscriptions
                .LongCountAsync(s => s.TargetId == targetId && s.TargetType == targetType);
            await redisDb.StringSetAsync(cacheKey, subscriberCount, CachedDataProvider.DefaultTtl);
            return subscriberCount;
        }

        private async Task IncreaseSubscriberCountAsync([NotNull] string targetId, SubscriptionTargetType targetType,
            long value)
        {
            var cacheKey = TargetSubscriberCountCacheKey(targetId, targetType);
            var redisDb = _redis.GetDatabase();
            if (await redisDb.KeyExistsAsync(cacheKey))
            {
                if (value >= 0)
                    await redisDb.StringIncrementAsync(cacheKey, value);
                else
                    await redisDb.StringDecrementAsync(cacheKey, -value);
            }
        }

        /// <summary>
        /// ���һ���¶���
        /// </summary>
        /// <param name="subscriberId">������ ID</param>
        /// <param name="targetId">Ŀ�� ID</param>
        /// <param name="targetType">Ŀ������</param>
        /// <exception cref="ArgumentNullException">�в���Ϊ null</exception>
        public async Task AddAsync([NotNull] string subscriberId, [NotNull] string targetId,
            SubscriptionTargetType targetType)
        {
            if (subscriberId == null)
                throw new ArgumentNullException(nameof(subscriberId));
            if (targetId == null)
                throw new ArgumentNullException(nameof(targetId));

            if (subscriberId == targetId || await IsSubscribedAsync(subscriberId, targetId, targetType))
                return;

            _dbContext.Subscriptions.Add(new Subscription
            {
                SubscriberId = subscriberId,
                TargetId = targetId,
                TargetType = targetType
            });
            await _dbContext.SaveChangesAsync();

            var redisDb = _redis.GetDatabase();
            await redisDb.SetAddAsync(UserSubscribedTargetsCacheKey(subscriberId),
                UserSubscribedTargetCacheValue(targetId, targetType));
            await IncreaseSubscriberCountAsync(targetId, targetType, 1);
        }

        /// <summary>
        /// ����һ�����������еĶ���
        /// </summary>
        /// <param name="subscriberId">������ ID</param>
        /// <param name="targetId">Ŀ�� ID</param>
        /// <param name="targetType">Ŀ������</param>
        /// <exception cref="ArgumentNullException">�в���Ϊ null</exception>
        public async Task RemoveAsync([NotNull] string subscriberId, [NotNull] string targetId,
            SubscriptionTargetType targetType)
        {
            if (subscriberId == null)
                throw new ArgumentNullException(nameof(subscriberId));
            if (targetId == null)
                throw new ArgumentNullException(nameof(targetId));

            if (subscriberId == targetId || !await IsSubscribedAsync(subscriberId, targetId, targetType))
                return;

            var subscriptions = await _dbContext.Subscriptions.Where(s => s.SubscriberId == subscriberId &&
                                                                          s.TargetId == targetId &&
                                                                          s.TargetType == targetType)
                .ToListAsync();
            _dbContext.Subscriptions.RemoveRange(subscriptions);
            await _dbContext.SaveChangesAsync();

            var redisDb = _redis.GetDatabase();
            var cacheKey = UserSubscribedTargetsCacheKey(subscriberId);
            foreach (var subscription in subscriptions)
            {
                await redisDb.SetRemoveAsync(cacheKey,
                    UserSubscribedTargetCacheValue(subscription.TargetId, subscription.TargetType));
            }
            await IncreaseSubscriberCountAsync(targetId, targetType, -subscriptions.Count);
        }
    }
}