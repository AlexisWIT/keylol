﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Keylol.Models;
using Keylol.Models.DAL;
using Keylol.Provider;
using Keylol.Provider.CachedDataProvider;
using Keylol.States.Shared;
using Keylol.StateTreeManager;
using Keylol.Utilities;

namespace Keylol.States.Entrance.Discovery
{
    /// <summary>
    /// 是日优惠据点列表
    /// </summary>
    public class OnSalePointList : List<PointBasicInfo>
    {
        private const int RecordsPerPage = 10;

        private OnSalePointList(int capacity) : base(capacity)
        {
        }

        /// <summary>
        /// 获取指定页码的是日优惠据点列表
        /// </summary>
        /// <param name="page">分页页码</param>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="cachedData"><see cref="CachedDataProvider"/></param>
        /// <returns>是日优惠据点列表</returns>
        public static async Task<OnSalePointList> Get(int page, [Injected] KeylolDbContext dbContext,
            [Injected] CachedDataProvider cachedData)
        {
            return (await CreateAsync(StateTreeHelper.GetCurrentUserId(),
                page, false, false, dbContext, cachedData)).Item1;
        }

        /// <summary>
        /// 创建 <see cref="OnSalePointList"/>
        /// </summary>
        /// <param name="currentUserId">当前登录用户 ID</param>
        /// <param name="page">分页页码</param>
        /// <param name="returnPageCount">是否返回总页数</param>
        /// <param name="returnFirstHeaderImage">是否返回第一个据点头部图</param>
        /// <param name="dbContext"><see cref="KeylolDbContext"/></param>
        /// <param name="cachedData"><see cref="CachedDataProvider"/></param>
        /// <returns>Item1 表示 <see cref="OnSalePointList"/>，Item2 表示总页数，Item3 表示第一个据点头部图，Item4 表示第二个据点头部图</returns>
        public static async Task<Tuple<OnSalePointList, int, string, string>> CreateAsync(string currentUserId, int page,
            bool returnPageCount, bool returnFirstHeaderImage, KeylolDbContext dbContext, CachedDataProvider cachedData)
        {
            SteamCrawlerProvider.UpdateOnSalePoints();
            var conditionQuery = from feed in dbContext.Feeds
                where feed.StreamName == OnSalePointStream.Name
                join point in dbContext.Points on feed.Entry equals point.Id
                orderby feed.Id descending
                select point;
            var queryResult = await conditionQuery.Select(p => new
            {
                Count = returnPageCount ? conditionQuery.Count() : 1,
                HeaderImage = returnFirstHeaderImage ? p.HeaderImage : null,
                p.Id,
                p.IdCode,
                p.ThumbnailImage,
                p.ChineseName,
                p.EnglishName,
                p.SteamPrice,
                p.SteamDiscountedPrice,
                p.SteamAppId
            }).TakePage(page, RecordsPerPage).ToListAsync();

            var result = new OnSalePointList(queryResult.Count);
            foreach (var p in queryResult)
            {
                result.Add(new PointBasicInfo
                {
                    IdCode = p.IdCode,
                    ThumbnailImage = p.ThumbnailImage,
                    ChineseName = p.ChineseName,
                    EnglishName = p.EnglishName,
                    AverageRating = (await cachedData.Points.GetRatingsAsync(p.Id)).AverageRating,
                    SteamPrice = p.SteamPrice,
                    SteamDiscountedPrice = p.SteamDiscountedPrice,
                    InLibrary = string.IsNullOrWhiteSpace(currentUserId) || p.SteamAppId == null
                        ? (bool?) null
                        : await cachedData.Users.IsSteamAppInLibraryAsync(currentUserId, p.SteamAppId.Value)
                });
            }
            var pointWithHeaders = queryResult.Where(r => !string.IsNullOrWhiteSpace(r.HeaderImage)).ToList();
            var firstRecord = pointWithHeaders.FirstOrDefault();
            var secondRecord = pointWithHeaders.Skip(1).FirstOrDefault();
            return new Tuple<OnSalePointList, int, string, string>(
                result,
                (int) Math.Ceiling(firstRecord?.Count/(double) RecordsPerPage ?? 1),
                firstRecord?.HeaderImage,
                secondRecord?.HeaderImage);
        }
    }
}