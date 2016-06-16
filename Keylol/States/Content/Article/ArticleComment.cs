﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Keylol.Identity;
using Keylol.Models;
using Keylol.Models.DAL;
using Keylol.Provider.CachedDataProvider;
using Keylol.StateTreeManager;
using Keylol.Utilities;

namespace Keylol.States.Content.Article
{
    /// <summary>
    /// 文章评论列表
    /// </summary>
    public class ArticleCommentList : List<ArticleComment>
    {
        private const int RecordsPerPage = 10;

        private ArticleCommentList(int capacity) : base(capacity)
        {
        }

        /// <summary>
        /// 获取指定文章的评论列表
        /// </summary>
        /// <param name="articleId">文章 ID</param>
        /// <param name="page">分页页码</param>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="cachedData"><see cref="CachedDataProvider"/></param>
        /// <param name="userManager"><see cref="KeylolUserManager"/></param>
        /// <returns><see cref="ArticleCommentList"/></returns>
        public static async Task<ArticleCommentList> Get(string articleId, int page,
            [Injected] KeylolDbContext dbContext, [Injected] CachedDataProvider cachedData,
            [Injected] KeylolUserManager userManager)
        {
            var article = await dbContext.Articles
                .Include(a => a.Author)
                .Include(a => a.TargetPoint)
                .Where(a => a.Id == articleId)
                .SingleOrDefaultAsync();

            if (article == null)
                return new ArticleCommentList(0);

            return (await CreateAsync(article, page, StateTreeHelper.GetCurrentUserId(), false, dbContext, cachedData,
                userManager)).Item1;
        }

        /// <summary>
        /// 创建 <see cref="ArticleCommentList"/>
        /// </summary>
        /// <param name="article">文章对象</param>
        /// <param name="page">分页页码</param>
        /// <param name="currentUserId">当前登录用户 ID</param>
        /// <param name="returnMeta">是否返回元数据（总页数、总评论数、最新评论时间）</param>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="cachedData"><see cref="CachedDataProvider"/></param>
        /// <param name="userManager"><see cref="KeylolUserManager"/></param>
        /// <returns>Item1 表示 <see cref="ArticleCommentList"/>， Item2 表示总评论数，Item3 表示最新评论时间，Item4 表示总页数</returns>
        public static async Task<Tuple<ArticleCommentList, int, DateTime?, int>> CreateAsync(Models.Article article,
            int page, string currentUserId, bool returnMeta, KeylolDbContext dbContext, CachedDataProvider cachedData,
            KeylolUserManager userManager)
        {
            var conditionQuery = from comment in dbContext.ArticleComments
                where comment.ArticleId == article.Id
                orderby comment.Sid
                select comment;
            var queryResult = await conditionQuery.Select(c => new
            {
                TotalCount = returnMeta ? conditionQuery.Count() : 1,
                Author = c.Commentator,
                c.Id,
                c.PublishTime,
                c.SidForArticle,
                c.Content,
                c.Archived,
                c.Warned
            }).TakePage(page, RecordsPerPage).ToListAsync();

            var result = new ArticleCommentList(queryResult.Count);
            foreach (var c in queryResult)
            {
                var articleComment = new ArticleComment
                {
                    SidForArticle = c.SidForArticle,
                    Archived = c.Archived != ArchivedState.None
                };
                // ReSharper disable once PossibleInvalidOperationException
                if (!articleComment.Archived.Value || currentUserId == c.Author.Id ||
                    await userManager.IsInRoleAsync(currentUserId, KeylolRoles.Operator))
                {
                    articleComment.AuthorIdCode = c.Author.IdCode;
                    articleComment.AuthorAvatarImage = c.Author.AvatarImage;
                    articleComment.AuthorUserName = c.Author.UserName;
                    articleComment.AuthorPlayedTime = article.TargetPoint.SteamAppId == null
                        ? null
                        : (await dbContext.UserSteamGameRecords
                            .Where(r => r.UserId == c.Author.Id && r.SteamAppId == article.TargetPoint.SteamAppId)
                            .SingleOrDefaultAsync())?.TotalPlayedTime;
                    articleComment.LikeCount =
                        await cachedData.Likes.GetTargetLikeCountAsync(c.Id, LikeTargetType.ArticleComment);
                    articleComment.PublishTime = c.PublishTime;
                    articleComment.Content = c.Content;
                    articleComment.Warned = c.Warned;
                }
                result.Add(articleComment);
            }
            var firstRecord = queryResult.FirstOrDefault();
            var latestCommentTime = returnMeta
                ? await (from comment in dbContext.ArticleComments
                    where comment.ArticleId == article.Id
                    orderby comment.Sid descending
                    select comment.PublishTime).FirstOrDefaultAsync()
                : default(DateTime);
            return new Tuple<ArticleCommentList, int, DateTime?, int>(result,
                firstRecord?.TotalCount ?? 0,
                latestCommentTime == default(DateTime) ? (DateTime?) null : latestCommentTime,
                (int) Math.Ceiling(firstRecord?.TotalCount/(double) RecordsPerPage ?? 1));
        }
    }

    /// <summary>
    /// 文章评论
    /// </summary>
    public class ArticleComment
    {
        /// <summary>
        /// 作者识别码
        /// </summary>
        public string AuthorIdCode { get; set; }

        /// <summary>
        /// 作者头像
        /// </summary>
        public string AuthorAvatarImage { get; set; }

        /// <summary>
        /// 作者用户名
        /// </summary>
        public string AuthorUserName { get; set; }

        /// <summary>
        /// 作者在档时间
        /// </summary>
        public double? AuthorPlayedTime { get; set; }

        /// <summary>
        /// 认可数
        /// </summary>
        public int? LikeCount { get; set; }

        /// <summary>
        /// 发布时间
        /// </summary>
        public DateTime? PublishTime { get; set; }

        /// <summary>
        /// 楼层号
        /// </summary>
        public int SidForArticle { get; set; }

        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 是否被封存
        /// </summary>
        public bool? Archived { get; set; }

        /// <summary>
        /// 是否被警告
        /// </summary>
        public bool? Warned { get; set; }
    }
}