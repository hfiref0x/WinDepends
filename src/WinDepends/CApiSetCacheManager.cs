/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2025
*
*  TITLE:       CAPISETCACHEMANAGER.CS
*
*  VERSION:     1.00
*
*  DATE:        20 Dec 2025
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Collections.Concurrent;

namespace WinDepends;

static class CApiSetCacheManager
{
    private struct ApiSetItem
    {
        public string ResolvedName;
    }

    static readonly ConcurrentDictionary<string, ApiSetItem> apiSetCache = new();

    public static void AddApiSet(string apiSetName, string resolvedName)
    {
        var item = new ApiSetItem { ResolvedName = resolvedName };
        apiSetCache.AddOrUpdate(apiSetName, item, (key, oldValue) => item);
    }

    public static string GetResolvedNameByApiSetName(string apiSetName)
    {
        if (apiSetCache.TryGetValue(apiSetName, out var item))
        {
            return item.ResolvedName;
        }
        else
        {
            return null;
        }
    }
    public static void ClearCache()
    {
        apiSetCache.Clear();
    }
}
