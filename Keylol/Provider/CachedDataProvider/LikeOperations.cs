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
    /// �����Ͽ���ز���
    /// </summary>
    public class LikeOperations
    {
        private readonly KeylolDbContext _dbContext;
        private readonly RedisProvider _redis;

        /// <summary>
        /// ���� <see cref="LikeOperations"/>
        /// </summary>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="redis"><see cref="RedisProvider"/></param>
        public LikeOperations(KeylolDbContext dbContext, RedisProvider redis)
        {
            _dbContext = dbContext;
            _redis = redis;
        }

        private static string UserLikeCountCacheKey(string userId) => $"user-like-count:{userId}";

        private static string TargetLikeCountCacheKey(string targetId, LikeTargetType targetType) =>
            $"target-like-count:{targetType.ToString().ToCase(NameConventionCase.PascalCase, NameConventionCase.DashedCase)}:{targetId}";

        private static string UserLikedTargetsCacheKey(string userId) => $"user-liked-targets:{userId}";

        private static string UserLikedTargetCacheValue(string targetId, LikeTargetType targetType) =>
            $"{targetType.ToString().ToCase(NameConventionCase.PascalCase, NameConventionCase.DashedCase)}:{targetId}";

        /// <summary>
        ///     ��ȡָ���û���õ����Ͽ���
        /// </summary>
        /// <param name="userId">�û� ID</param>
        /// <exception cref="ArgumentNullException"><paramref name="userId"/> Ϊ null</exception>
        /// <returns>�û���õ����Ͽ���</returns>
        public async Task<int> GetUserLikeCountAsync([NotNull] string userId)
        {
            if (userId == null)
                throw new ArgumentNullException(nameof(userId));

            var cacheKey = UserLikeCountCacheKey(userId);
            var redisDb = _redis.GetDatabase();
            var cachedResult = await redisDb.StringGetAsync(cacheKey);
            if (cachedResult.HasValue)
            {
                await redisDb.KeyExpireAsync(cacheKey, CachedDataProvider.DefaultTtl);
                return (int) cachedResult;
            }

            var articleLikeCount = await (from article in _dbContext.Articles
                join like in _dbContext.Likes on article.Id equals like.TargetId
                where like.TargetType == LikeTargetType.Article && article.AuthorId == userId
                select article
                ).CountAsync();
            var articleCommentLikeCount = await (from comment in _dbContext.ArticleComments
                join like in _dbContext.Likes on comment.Id equals like.TargetId
                where like.TargetType == LikeTargetType.ArticleComment && comment.CommentatorId == userId
                select comment
                ).CountAsync();
            var activityLikeCount = await (from activity in _dbContext.Activities
                join like in _dbContext.Likes on activity.Id equals like.TargetId
                where like.TargetType == LikeTargetType.Activity && activity.AuthorId == userId
                select activity
                ).CountAsync();
            var activityCommentLikeCount = await (from comment in _dbContext.ActivityComments
                join like in _dbContext.Likes on comment.Id equals like.TargetId
                where like.TargetType == LikeTargetType.ActivityComment && comment.CommentatorId == userId
                select comment
                ).CountAsync();
            var conferenceEntryLikeCount = await (from entry in _dbContext.ConferenceEntries
                join like in _dbContext.Likes on entry.Id equals like.TargetId
                where like.TargetType == LikeTargetType.ConferenceEntry && entry.AuthorId == userId
                select entry
                ).CountAsync();

            var likeCount = articleLikeCount + articleCommentLikeCount + activityLikeCount +
                            activityCommentLikeCount + conferenceEntryLikeCount;
            await redisDb.StringSetAsync(cacheKey, likeCount, CachedDataProvider.DefaultTtl);
            return likeCount;
        }

        /// <summary>
        /// ��ȡָ��Ŀ���ȡ���Ͽ���
        /// </summary>
        /// <param name="targetId">Ŀ�� ID</param>
        /// <param name="targetType">Ŀ������</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetId"/> Ϊ null</exception>
        /// <returns>ָ��Ŀ���ȡ���Ͽ���</returns>
        public async Task<int> GetTargetLikeCountAsync([NotNull] string targetId, LikeTargetType targetType)
        {
            if (targetId == null)
                throw new ArgumentNullException(nameof(targetId));

            var cacheKey = TargetLikeCountCacheKey(targetId, targetType);
            var redisDb = _redis.GetDatabase();
            var cachedResult = await redisDb.StringGetAsync(cacheKey);
            if (cachedResult.HasValue)
            {
                await redisDb.KeyExpireAsync(cacheKey, CachedDataProvider.DefaultTtl);
                return (int) cachedResult;
            }

            var likeCount =
                await _dbContext.Likes.CountAsync(l => l.TargetId == targetId && l.TargetType == targetType);
            await redisDb.StringSetAsync(cacheKey, likeCount, CachedDataProvider.DefaultTtl);
            return likeCount;
        }

        /// <summary>
        /// �ж�ָ���û��Ƿ��Ͽɹ�ָ��Ŀ��
        /// </summary>
        /// <param name="userId">�û� ID</param>
        /// <param name="targetId">Ŀ�� ID</param>
        /// <param name="targetType">Ŀ������</param>
        /// <returns>����û��Ͽɹ������� <c>true</c></returns>
        public async Task<bool> IsLikedAsync(string userId, string targetId, LikeTargetType targetType)
        {
            if (userId == null || targetId == null)
                return false;

            var cacheKey = UserLikedTargetsCacheKey(userId);
            var redisDb = _redis.GetDatabase();
            if (!await redisDb.KeyExistsAsync(cacheKey))
            {
                foreach (var like in await _dbContext.Likes.Where(l => l.OperatorId == userId)
                    .Select(l => new {l.TargetId, l.TargetType}).ToListAsync())
                {
                    await redisDb.SetAddAsync(cacheKey, UserLikedTargetCacheValue(like.TargetId, like.TargetType));
                }
            }
            await redisDb.KeyExpireAsync(cacheKey, CachedDataProvider.DefaultTtl);
            return await redisDb.SetContainsAsync(cacheKey, UserLikedTargetCacheValue(targetId, targetType));
        }

        private async Task IncreaseUserLikeCountAsync([NotNull] string userId, long value)
        {
            var cacheKey = UserLikeCountCacheKey(userId);
            var redisDb = _redis.GetDatabase();
            if (await redisDb.KeyExistsAsync(cacheKey))
            {
                if (value >= 0)
                    await redisDb.StringIncrementAsync(cacheKey, value);
                else
                    await redisDb.StringDecrementAsync(cacheKey, -value);
            }
        }

        private async Task IncreaseTargetLikeCountAsync([NotNull] string targetId, LikeTargetType targetType, long value)
        {
            var cacheKey = TargetLikeCountCacheKey(targetId, targetType);
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
        /// ���һ�����Ͽ�
        /// </summary>
        /// <param name="operatorId">�Ͽɲ����� ID</param>
        /// <param name="targetId">Ŀ�� ID</param>
        /// <param name="targetType">Ŀ������</param>
        /// <returns>�Ͽɳɹ����� <c>true</c></returns>
        /// <exception cref="ArgumentNullException">�в���Ϊ null</exception>
        public async Task<bool> AddAsync([NotNull] string operatorId, [NotNull] string targetId, LikeTargetType targetType)
        {
            if (operatorId == null)
                throw new ArgumentNullException(nameof(operatorId));
            if (targetId == null)
                throw new ArgumentNullException(nameof(targetId));

            if (await IsLikedAsync(operatorId, targetId, targetType))
                return false;

            string likeReceiverId;
            switch (targetType)
            {
                case LikeTargetType.Article:
                    likeReceiverId = await _dbContext.Articles.Where(a => a.Id == targetId)
                        .Select(a => a.AuthorId).SingleAsync();
                    break;

                case LikeTargetType.ArticleComment:
                    likeReceiverId = await _dbContext.ArticleComments.Where(c => c.Id == targetId)
                        .Select(c => c.CommentatorId).SingleAsync();
                    break;

                case LikeTargetType.Activity:
                    likeReceiverId = await _dbContext.Activities.Where(a => a.Id == targetId)
                        .Select(a => a.AuthorId).SingleAsync();
                    break;

                case LikeTargetType.ActivityComment:
                    likeReceiverId = await _dbContext.ActivityComments.Where(c => c.Id == targetId)
                        .Select(c => c.CommentatorId).SingleAsync();
                    break;

                case LikeTargetType.ConferenceEntry:
                    likeReceiverId = await _dbContext.ConferenceEntries.Where(e => e.Id == targetId)
                        .Select(e => e.AuthorId).SingleAsync();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(targetType), targetType, null);
            }

            _dbContext.Likes.Add(new Like
            {
                OperatorId = operatorId,
                TargetId = targetId,
                TargetType = targetType
            });
            await _dbContext.SaveChangesAsync();

            var redisDb = _redis.GetDatabase();
            await redisDb.SetAddAsync(UserLikedTargetsCacheKey(operatorId),
                UserLikedTargetCacheValue(targetId, targetType));
            await IncreaseUserLikeCountAsync(likeReceiverId, 1);
            await IncreaseTargetLikeCountAsync(targetId, targetType, 1);
            return true;
        }

        /// <summary>
        /// ����һ�������߷������Ͽ�
        /// </summary>
        /// <param name="operatorId">������ ID</param>
        /// <param name="targetId">Ŀ�� ID</param>
        /// <param name="targetType">Ŀ������</param>
        /// <exception cref="ArgumentNullException">�в���Ϊ null</exception>
        public async Task RemoveAsync([NotNull] string operatorId, [NotNull] string targetId,
            LikeTargetType targetType)
        {
            if (operatorId == null)
                throw new ArgumentNullException(nameof(operatorId));
            if (targetId == null)
                throw new ArgumentNullException(nameof(targetId));

            if (!await IsLikedAsync(operatorId, targetId, targetType))
                return;

            string likeReceiverId;
            switch (targetType)
            {
                case LikeTargetType.Article:
                    likeReceiverId = await _dbContext.Articles.Where(a => a.Id == targetId)
                        .Select(a => a.AuthorId).SingleAsync();
                    break;

                case LikeTargetType.ArticleComment:
                    likeReceiverId = await _dbContext.ArticleComments.Where(c => c.Id == targetId)
                        .Select(c => c.CommentatorId).SingleAsync();
                    break;

                case LikeTargetType.Activity:
                    likeReceiverId = await _dbContext.Activities.Where(a => a.Id == targetId)
                        .Select(a => a.AuthorId).SingleAsync();
                    break;

                case LikeTargetType.ActivityComment:
                    likeReceiverId = await _dbContext.ActivityComments.Where(c => c.Id == targetId)
                        .Select(c => c.CommentatorId).SingleAsync();
                    break;

                case LikeTargetType.ConferenceEntry:
                    likeReceiverId = await _dbContext.ConferenceEntries.Where(e => e.Id == targetId)
                        .Select(e => e.AuthorId).SingleAsync();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(targetType), targetType, null);
            }

            var likes = await _dbContext.Likes.Where(l => l.OperatorId == operatorId &&
                                                          l.TargetId == targetId && l.TargetType == targetType)
                .ToListAsync();
            _dbContext.Likes.RemoveRange(likes);
            await _dbContext.SaveChangesAsync();

            var redisDb = _redis.GetDatabase();
            var cacheKey = UserLikedTargetsCacheKey(operatorId);
            foreach (var like in likes)
            {
                await redisDb.SetRemoveAsync(cacheKey, UserLikedTargetCacheValue(like.TargetId, like.TargetType));
            }
            await IncreaseUserLikeCountAsync(likeReceiverId, -likes.Count);
            await IncreaseTargetLikeCountAsync(targetId, targetType, -likes.Count);
        }
    }
}