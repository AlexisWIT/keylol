using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Keylol.Models;
using Keylol.Models.DAL;
using Keylol.Provider.CachedDataProvider;
using Keylol.StateTreeManager;

namespace Keylol.States.Search.Article
{
    /// <summary>
    /// ������������б�
    /// </summary>
    public class ArticleResultList : List<ArticleResult>
    {
        private ArticleResultList(int capacity) : base(capacity)
        {
        }

        /// <summary>
        /// ͨ���ؼ������������б�
        /// </summary>
        /// <param name="keyword">�����ؼ���</param>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="cachedData"><see cref="CachedDataProvider"/></param>
        /// <param name="page">��ҳҳ��</param>
        /// <param name="searchAll">�Ƿ��ѯȫ��</param>
        public static async Task<ArticleResultList> Get(string keyword, [Injected] KeylolDbContext dbContext,
            [Injected] CachedDataProvider cachedData, int page, bool searchAll = true)
        {
            return await CreateAsync(keyword, dbContext, cachedData, page, searchAll);
        }

        /// <summary>
        /// ���� <see cref="ArticleResultList"/>
        /// </summary>
        /// <param name="keyword">�����ؼ���</param>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="cachedData"><see cref="CachedDataProvider"/></param>
        /// <param name="page">��ҳҳ��</param>
        /// <param name="searchAll">�Ƿ��ѯȫ��</param>
        public static async Task<ArticleResultList> CreateAsync(string keyword, [Injected] KeylolDbContext dbContext,
            [Injected] CachedDataProvider cachedData, int page, bool searchAll = true)
        {
            var take = searchAll ? 10 : 5;
            var skip = (page - 1)*take;
            var searchResult = await dbContext.Database.SqlQuery<ArticleResult>(@"SELECT
                        *,
                        (SELECT
                            IdCode
                        FROM KeylolUsers
                        WHERE t3.AuthorId = Id)
                        AS AuthorIdCode
                    FROM (SELECT
                        [t1].*,
                        [t2].*,
                        [t5].[AvatarImage] AS PointAvatarImage,
                        [t5].[ChineseName] AS PointChineseName,
                        [t5].[EnglishName] AS PointEnglishName
                    FROM [dbo].[Articles] AS [t1]
                    INNER JOIN (SELECT
                        *
                    FROM FREETEXTTABLE([dbo].[Articles], ([Title], [Subtitle], [UnstyledContent]), {0})) AS [t2]
                        ON [t1].[Sid] = [t2].[KEY]
                    INNER JOIN [dbo].[Points] AS [t5]
                        ON [t1].[TargetPointId] = [t5].[Id]
                    WHERE [t1].[Archived] = 0 AND [t1].[Rejected] = 0) AS [t3]
                    ORDER BY [t3].[RANK] DESC OFFSET {1} ROWS FETCH NEXT {2} ROWS ONLY",
                keyword, skip, take).ToListAsync();

            var result = new ArticleResultList(searchResult.Count);
            foreach (var p in searchResult)
            {
                result.Add(new ArticleResult
                {
                    Title = p.Title,
                    SubTitle = p.SubTitle,
                    AuthorIdCode = p.AuthorIdCode,
                    SidForAuthor = p.SidForAuthor,
                    PointChineseName = p.PointChineseName,
                    PointEnglishName = p.PointEnglishName,
                    PointAvaterImage = p.PointAvaterImage,
                    PointIdCode = p.PointIdCode,
                    LikeCount = await cachedData.Likes.GetTargetLikeCountAsync(p.Id, LikeTargetType.Article),
                    CommentCount =
                        searchAll ? await cachedData.ArticleComments.GetArticleCommentCountAsync(p.Id) : (long?)null,
                    PublishTime = searchAll ? p.PublishTime : null
                });
            }
            return result;
        }
    }

    /// <summary>
    /// �����������
    /// </summary>
    public class ArticleResult
    {
        /// <summary>
        /// ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// ����
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// ������
        /// </summary>
        public string SubTitle { get; set; }

        /// <summary>
        /// ����ʶ����
        /// </summary>
        public string AuthorIdCode { get; set; }

        /// <summary>
        /// �����������������
        /// </summary>
        public int? SidForAuthor { get; set; }

        /// <summary>
        /// �ݵ�������
        /// </summary>
        public string PointChineseName { get; set; }

        /// <summary>
        /// �ݵ�Ӣ����
        /// </summary>
        public string PointEnglishName { get; set; }

        /// <summary>
        /// �ݵ�ͷ��
        /// </summary>
        public string PointAvaterImage { get; set; }

        /// <summary>
        /// �ݵ�ʶ����
        /// </summary>
        public string PointIdCode { get; set; }

        /// <summary>
        /// ������
        /// </summary>
        public long? LikeCount { get; set; }

        /// <summary>
        /// ������
        /// </summary>
        public long? CommentCount { get; set; }

        /// <summary>
        /// ����ʱ��
        /// </summary>
        public DateTime? PublishTime { get; set; }
    }
}