using System.Collections.Generic;
using System.Threading.Tasks;
using Keylol.Models.DAL;
using Keylol.Provider.CachedDataProvider;
using Keylol.StateTreeManager;

namespace Keylol.States.Search.User
{
    /// <summary>
    /// �û������б�
    /// </summary>
    public class UserResultList : List<UserResult>
    {
        private UserResultList(int capacity) : base(capacity)
        {
        }

        /// <summary>
        /// ͨ���ؼ��������û��б�
        /// </summary>
        /// <param name="keyword">�����ؼ���</param>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="cachedData"><see cref="CachedDataProvider"/></param>
        /// <param name="page">��ҳҳ��</param>
        /// <param name="searchAll">�Ƿ�ȫ����ѯ</param>
        public static async Task<UserResultList> Get(string keyword, [Injected] KeylolDbContext dbContext,
            [Injected] CachedDataProvider cachedData, int page, bool searchAll = true)
        {
            return
                await CreateAsync(StateTreeHelper.GetCurrentUserId(), keyword, dbContext, cachedData, page, searchAll);
        }

        /// <summary>
        /// ���� <see cref="UserResultList"/>
        /// </summary>
        /// <param name="currentUserId">��ǰ��¼�û� ID</param>
        /// <param name="keyword">�����ؼ���</param>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="cachedData"><see cref="CachedDataProvider"/></param>
        /// <param name="page">��ҳҳ��</param>
        /// <param name="searchAll">�Ƿ�ȫ����ѯ</param>
        public static async Task<UserResultList> CreateAsync(string currentUserId, string keyword,
            [Injected] KeylolDbContext dbContext, [Injected] CachedDataProvider cachedData, int page,
            bool searchAll = true)
        {
            var take = searchAll ? 10 : 5;
            var skip = (page - 1)*take;
            keyword = keyword.Replace('"', ' ').Replace('*', ' ').Replace('\'', ' ');
            var queryResult = await dbContext.Database.SqlQuery<UserResult>(@"SELECT
                        *,
                        (SELECT
                            COUNT(1)
                        FROM Articles
                        WHERE AuthorId = [t3].[Id])
                        AS ArticleCount,
                        (SELECT
                            COUNT(1)
                        FROM Activities
                        WHERE AuthorId = [t3].[Id])
                        AS ActivityCount
                    FROM (SELECT
                        *
                    FROM [dbo].[KeylolUsers] AS [t1]
                    INNER JOIN (SELECT
                        *
                    FROM CONTAINSTABLE([dbo].[KeylolUsers], ([UserName]), {0})) AS [t2]
                        ON [t1].[Sid] = [t2].[KEY]) AS [t3]
                    ORDER BY [t3].[RANK] DESC, [ArticleCount] DESC OFFSET {1} ROWS FETCH NEXT {2} ROWS ONLY",
                $"\"{keyword}\" OR \"{keyword}*\"", skip, take).ToListAsync();

            var result = new UserResultList(queryResult.Count);
            foreach (var p in queryResult)
            {
                result.Add(new UserResult
                {
                    Id = searchAll ? p.Id : null,
                    UserName = p.UserName,
                    GamerTag = p.GamerTag,
                    IdCode = p.IdCode,
                    AvatarImage = p.AvatarImage,
                    ArticleCount = p.ArticleCount,
                    ActivityCount = p.ActivityCount,
                    LikeCount = await cachedData.Likes.GetUserLikeCountAsync(p.Id),
                    IsFriend = await cachedData.Users.IsFriendAsync(currentUserId, p.Id)
                });
            }
            return result;
        }
    }

    /// <summary>
    /// �û��������
    /// </summary>
    public class UserResult
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// �û���
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// �û�ʶ����
        /// </summary>
        public string IdCode { get; set; }

        /// <summary>
        /// ��ұ�ǩ
        /// </summary>
        public string GamerTag { get; set; }

        /// <summary>
        /// ͷ��
        /// </summary>
        public string AvatarImage { get; set; }

        /// <summary>
        /// ������
        /// </summary>
        public int? ArticleCount { get; set; }

        /// <summary>
        /// ��̬��
        /// </summary>
        public int? ActivityCount { get; set; }

        /// <summary>
        /// ����Ͽ���
        /// </summary>
        public int? LikeCount { get; set; }

        /// <summary>
        /// �Ƿ��Ǻ���
        /// </summary>
        public bool? IsFriend { get; set; }
    }
}