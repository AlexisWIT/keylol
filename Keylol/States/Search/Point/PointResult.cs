using System.Collections.Generic;
using System.Threading.Tasks;
using Keylol.Models;
using Keylol.Models.DAL;
using Keylol.Provider.CachedDataProvider;
using Keylol.StateTreeManager;

namespace Keylol.States.Search.Point
{
    /// <summary>
    /// �ݵ���������б�
    /// </summary>
    public class PointResultList : List<PointResult>
    {
        private PointResultList(int capacity) : base(capacity)
        {
        }

        /// <summary>
        /// ͨ���ؼ��������ݵ��б�
        /// </summary>
        /// <param name="keyword">�����ؼ���</param>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="cachedData"><see cref="CachedDataProvider"/></param>
        /// <param name="page">��ҳҳ��</param>
        /// <param name="searchAll">�Ƿ�ȫ����ѯ</param>
        /// <returns><see cref="PointResultList"/></returns>
        public static async Task<PointResultList> Get(string keyword, [Injected] KeylolDbContext dbContext,
            [Injected] CachedDataProvider cachedData, int page, bool searchAll = true)
        {
            return
                await CreateAsync(StateTreeHelper.GetCurrentUserId(), keyword, dbContext, cachedData, page, searchAll);
        }

        /// <summary>
        /// ���� <see cref="PointResultList"/>
        /// </summary>
        /// <param name="currentUserId">��ǰ�û� ID</param>
        /// <param name="keyword">�����ؼ���</param>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="cachedData"><see cref="CachedDataProvider"/></param>
        /// <param name="page">��ҳҳ��</param>
        /// <param name="searchAll">�Ƿ�ȫ����ѯ</param>
        public static async Task<PointResultList> CreateAsync(string currentUserId, string keyword,
            [Injected] KeylolDbContext dbContext, [Injected] CachedDataProvider cachedData, int page,
            bool searchAll = true)
        {
            var take = searchAll ? 10 : 5;
            var skip = (page - 1)*take;
            keyword = keyword.Replace('"', ' ').Replace('*', ' ').Replace('\'', ' ');
            var searchResult = await dbContext.Database.SqlQuery<PointResult>(@"SELECT
                        *,
                        (SELECT
                            COUNT(1)
                        FROM Articles
                        WHERE TargetPointId = t4.Id)
                        AS ArticleCount,
                        (SELECT
                            COUNT(1)
                        FROM Activities
                        WHERE TargetPointId = t4.Id)
                        AS ActivityCount
                    FROM (SELECT
                        *
                    FROM [dbo].[Points] AS [t1]
                    INNER JOIN (SELECT
                        [t2].[KEY],
                        MAX([t2].[RANK]) AS RANK
                    FROM (SELECT
                        *
                    FROM CONTAINSTABLE([dbo].[Points], ([EnglishName], [EnglishAliases]), {0})
                    UNION ALL
                    SELECT
                        *
                    FROM CONTAINSTABLE([dbo].[Points], ([ChineseName], [ChineseAliases]), {0})) AS [t2]
                    GROUP BY [t2].[KEY]) AS [t3]
                        ON [t1].[Sid] = [t3].[KEY]) AS [t4]
                    ORDER BY [t4].[RANK] DESC, [ArticleCount] DESC OFFSET {1} ROWS FETCH NEXT {2} ROWS ONLY",
                $"\"{keyword}\" OR \"{keyword}*\"", skip, take).ToListAsync();

            var result = new PointResultList(searchResult.Count);
            foreach (var p in searchResult)
            {
                result.Add(new PointResult
                {
                    Id = searchAll ? p.Id : null,
                    IdCode = p.IdCode,
                    ChineseName = p.ChineseName,
                    EnglishName = p.EnglishName,
                    AvatarImage = p.AvatarImage,
                    SubscriberCount = searchAll
                        ? await cachedData.Subscriptions.GetSubscriberCountAsync(p.Id, SubscriptionTargetType.User)
                        : (long?) null,
                    ArticleCount = searchAll ? p.ArticleCount : null,
                    ActivityCount = searchAll ? p.ActivityCount : null,
                    Subscribed = searchAll && !string.IsNullOrWhiteSpace(currentUserId)
                        ? await cachedData.Subscriptions.IsSubscribedAsync(currentUserId, p.Id,
                            SubscriptionTargetType.User)
                        : (bool?) null,
                    InLibrary = p.SteamAppId == null || string.IsNullOrWhiteSpace(currentUserId)
                        ? (bool?) null
                        : await cachedData.Users.IsSteamAppInLibraryAsync(currentUserId, p.SteamAppId.Value)
                });
            }
            return result;
        }
    }

    /// <summary>
    /// �ݵ��������
    /// </summary>
    public class PointResult
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// ʶ����
        /// </summary>
        public string IdCode { get; set; }

        /// <summary>
        /// ������
        /// </summary>
        public string ChineseName { get; set; }

        /// <summary>
        /// Ӣ����
        /// </summary>
        public string EnglishName { get; set; }

        /// <summary>
        /// Steam App ID
        /// </summary>
        public int? SteamAppId { get; set; }

        /// <summary>
        /// ͷ��
        /// </summary>
        public string AvatarImage { get; set; }

        /// <summary>
        /// ������
        /// </summary>
        public long? SubscriberCount { get; set; }

        /// <summary>
        /// ����������
        /// </summary>
        public int? ArticleCount { get; set; }

        /// <summary>
        /// ��̬��
        /// </summary>
        public int? ActivityCount { get; set; }

        /// <summary>
        /// �Ƿ񱻶���
        /// </summary>
        public bool? Subscribed { get; set; }

        /// <summary>
        /// �Ƿ������
        /// </summary>
        public bool? InLibrary { get; set; }
    }
}