smartdocs fails with 
2026-11-26T20:50:50.362 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c   36m        4        ReaderAccess.GetFolderAsync(prefix:"plugindeliveryservice/03-architecture") START
2026-11-26T20:50:50.387 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   25m        5          FolderRecordProvider.GetAsync(prefix:"plugindeliveryservice/03-architecture") START
2026-11-26T20:50:50.512 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c  125m        6            FolderRecordProvider.BuildAsync(prefix:"plugindeliveryservice/03-architecture") START
2026-11-26T20:50:50.657 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c  144m        7              CachedDynamicNavBuilder.GetChildrenAsync(prefix:"plugindeliveryservice") START
2026-11-26T20:50:50.701 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   44m        8                GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, operationOptions:SmartCacheOperationOptions{%, callerType:CachedDynamicNavBuilder) START
2026-11-26T20:50:50.792 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   91m        9                  GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, timestamp:$2026-11-26T20:50:50.0000000+00:00$, maybeMinimumCreationDate:$2026-11-26T20:45:50.0000000+00:00$, absExpiration:□, sldExpiration:□) START
2026-11-26T20:50:50.801 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c    9m        9                   Cache entry found
2026-11-26T20:50:50.803 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c    2m        9                   Cache hit: valid creation date (minimum: 2026-11-26T20:45:50, newer: 2026-11-26T20:49:50)
2026-11-26T20:50:50.842 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   39m   90m  9                  GetAsync END
2026-11-26T20:50:50.901 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c        212m  8                GetAsync END
2026-11-26T20:50:50.942 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c        378m  7              CachedDynamicNavBuilder.GetChildrenAsync => ¤{% END
2026-11-26T20:50:50.978 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        559m  6            FolderRecordProvider.BuildAsync END
2026-11-26T20:50:51.043 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        659m  5          FolderRecordProvider.GetAsync => ¤{% END
2026-11-26T20:50:51.070 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c        714m  4        ReaderAccess.GetFolderAsync END
2026-11-26T20:50:51.111 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c   41m        4        ReaderAccess.GetFolderAsync(prefix:"plugindeliveryservice/04-use-cases") START
2026-11-26T20:50:51.144 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   34m        5          FolderRecordProvider.GetAsync(prefix:"plugindeliveryservice/04-use-cases") START
2026-11-26T20:50:51.172 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   27m        6            FolderRecordProvider.BuildAsync(prefix:"plugindeliveryservice/04-use-cases") START
2026-11-26T20:50:51.203 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c   31m        7              CachedDynamicNavBuilder.GetChildrenAsync(prefix:"plugindeliveryservice") START
2026-11-26T20:50:51.287 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   84m        8                GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, operationOptions:SmartCacheOperationOptions{%, callerType:CachedDynamicNavBuilder) START
2026-11-26T20:50:51.367 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   79m        9                  GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, timestamp:$2026-11-26T20:50:51.0000000+00:00$, maybeMinimumCreationDate:$2026-11-26T20:45:51.0000000+00:00$, absExpiration:□, sldExpiration:□) START
2026-11-26T20:50:51.376 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c    9m        9                   Cache entry found
2026-11-26T20:50:51.380 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c    4m        9                   Cache hit: valid creation date (minimum: 2026-11-26T20:45:51, newer: 2026-11-26T20:49:50)
2026-11-26T20:50:51.401 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   21m   64m  9                  GetAsync END
2026-11-26T20:50:51.441 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c        198m  8                GetAsync END
2026-11-26T20:50:51.497 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c        291m  7              CachedDynamicNavBuilder.GetChildrenAsync => ¤{% END
2026-11-26T20:50:51.567 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        395m  6            FolderRecordProvider.BuildAsync END
2026-11-26T20:50:51.630 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        469m  5          FolderRecordProvider.GetAsync => ¤{% END
2026-11-26T20:50:51.676 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c        567m  4        ReaderAccess.GetFolderAsync END
2026-11-26T20:50:51.711 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c   35m        4        ReaderAccess.GetFolderAsync(prefix:"plugindeliveryservice/05-infrastructure") START
2026-11-26T20:50:51.741 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   30m        5          FolderRecordProvider.GetAsync(prefix:"plugindeliveryservice/05-infrastructure") START
2026-11-26T20:50:51.784 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   43m        6            FolderRecordProvider.BuildAsync(prefix:"plugindeliveryservice/05-infrastructure") START
2026-11-26T20:50:51.841 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c   57m        7              CachedDynamicNavBuilder.GetChildrenAsync(prefix:"plugindeliveryservice") START
2026-11-26T20:50:51.909 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   68m        8                GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, operationOptions:SmartCacheOperationOptions{%, callerType:CachedDynamicNavBuilder) START
2026-11-26T20:50:51.926 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   17m        9                  GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, timestamp:$2026-11-26T20:50:51.0000000+00:00$, maybeMinimumCreationDate:$2026-11-26T20:45:51.0000000+00:00$, absExpiration:□, sldExpiration:□) START
2026-11-26T20:50:51.967 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   41m        9                   Cache entry found
2026-11-26T20:50:51.984 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   17m        9                   Cache hit: valid creation date (minimum: 2026-11-26T20:45:51, newer: 2026-11-26T20:49:50)
2026-11-26T20:50:52.034 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   50m  105m  9                  GetAsync END
2026-11-26T20:50:52.071 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c        175m  8                GetAsync END
2026-11-26T20:50:52.120 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c        306m  7              CachedDynamicNavBuilder.GetChildrenAsync => ¤{% END
2026-11-26T20:50:52.152 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        386m  6            FolderRecordProvider.BuildAsync END
2026-11-26T20:50:52.228 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        449m  5          FolderRecordProvider.GetAsync => ¤{% END
2026-11-26T20:50:52.317 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c        585m  4        ReaderAccess.GetFolderAsync END
2026-11-26T20:50:52.364 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c   46m        4        ReaderAccess.GetFolderAsync(prefix:"plugindeliveryservice/06-reference") START
2026-11-26T20:50:52.445 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   81m        5          FolderRecordProvider.GetAsync(prefix:"plugindeliveryservice/06-reference") START
2026-11-26T20:50:52.542 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   92m        6            FolderRecordProvider.BuildAsync(prefix:"plugindeliveryservice/06-reference") START
2026-11-26T20:50:52.610 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c   68m        7              CachedDynamicNavBuilder.GetChildrenAsync(prefix:"plugindeliveryservice") START
2026-11-26T20:50:52.649 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   38m        8                GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, operationOptions:SmartCacheOperationOptions{%, callerType:CachedDynamicNavBuilder) START
2026-11-26T20:50:52.693 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   43m        9                  GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, timestamp:$2026-11-26T20:50:52.0000000+00:00$, maybeMinimumCreationDate:$2026-11-26T20:45:52.0000000+00:00$, absExpiration:□, sldExpiration:□) START
2026-11-26T20:50:52.700 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c    8m        9                   Cache entry found
2026-11-26T20:50:52.729 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   29m        9                   Cache hit: valid creation date (minimum: 2026-11-26T20:45:52, newer: 2026-11-26T20:49:50)
2026-11-26T20:50:52.746 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   16m   73m  9                  GetAsync END
2026-11-26T20:50:52.772 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c        148m  8                GetAsync END
2026-11-26T20:50:52.805 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c        238m  7              CachedDynamicNavBuilder.GetChildrenAsync => ¤{% END
2026-11-26T20:50:52.876 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        359m  6            FolderRecordProvider.BuildAsync END
2026-11-26T20:50:52.929 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        493m  5          FolderRecordProvider.GetAsync => ¤{% END
2026-11-26T20:50:52.995 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c        632m  4        ReaderAccess.GetFolderAsync END
2026-11-26T20:50:53.062 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c   67m        4        ReaderAccess.GetFolderAsync(prefix:"plugindeliveryservice/07-other-components") START
2026-11-26T20:50:53.134 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   72m        5          FolderRecordProvider.GetAsync(prefix:"plugindeliveryservice/07-other-components") START
2026-11-26T20:50:53.180 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   46m        6            FolderRecordProvider.BuildAsync(prefix:"plugindeliveryservice/07-other-components") START
2026-11-26T20:50:53.236 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c   56m        7              CachedDynamicNavBuilder.GetChildrenAsync(prefix:"plugindeliveryservice") START
2026-11-26T20:50:53.275 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   39m        8                GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, operationOptions:SmartCacheOperationOptions{%, callerType:CachedDynamicNavBuilder) START
2026-11-26T20:50:53.330 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   55m        9                  GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, timestamp:$2026-11-26T20:50:53.0000000+00:00$, maybeMinimumCreationDate:$2026-11-26T20:45:53.0000000+00:00$, absExpiration:□, sldExpiration:□) START
2026-11-26T20:50:53.333 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c    4m        9                   Cache entry found
2026-11-26T20:50:53.335 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c    2m        9                   Cache hit: valid creation date (minimum: 2026-11-26T20:45:53, newer: 2026-11-26T20:49:50)
2026-11-26T20:50:53.354 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   18m   28m  9                  GetAsync END
2026-11-26T20:50:53.419 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c        136m  8                GetAsync END
2026-11-26T20:50:53.539 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c        285m  7              CachedDynamicNavBuilder.GetChildrenAsync => ¤{% END
2026-11-26T20:50:53.599 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        423m  6            FolderRecordProvider.BuildAsync END
2026-11-26T20:50:53.642 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        517m  5          FolderRecordProvider.GetAsync => ¤{% END
2026-11-26T20:50:53.746 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c        683m  4        ReaderAccess.GetFolderAsync END
2026-11-26T20:50:53.844 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c   98m        4        ReaderAccess.GetFolderAsync(prefix:"plugindeliveryservice/08-validation") START
2026-11-26T20:50:53.893 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   49m        5          FolderRecordProvider.GetAsync(prefix:"plugindeliveryservice/08-validation") START
2026-11-26T20:50:53.952 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   59m        6            FolderRecordProvider.BuildAsync(prefix:"plugindeliveryservice/08-validation") START
2026-11-26T20:50:54.011 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c   59m        7              CachedDynamicNavBuilder.GetChildrenAsync(prefix:"plugindeliveryservice") START
2026-11-26T20:50:54.049 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   39m        8                GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, operationOptions:SmartCacheOperationOptions{%, callerType:CachedDynamicNavBuilder) START
2026-11-26T20:50:54.129 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   79m        9                  GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, timestamp:$2026-11-26T20:50:54.0000000+00:00$, maybeMinimumCreationDate:$2026-11-26T20:45:54.0000000+00:00$, absExpiration:□, sldExpiration:□) START
2026-11-26T20:50:54.145 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   14m        9                   Cache entry found
2026-11-26T20:50:54.150 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c    6m        9                   Cache hit: valid creation date (minimum: 2026-11-26T20:45:54, newer: 2026-11-26T20:49:50)
2026-11-26T20:50:54.203 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   52m  106m  9                  GetAsync END
2026-11-26T20:50:54.309 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c        240m  8                GetAsync END
2026-11-26T20:50:54.402 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c        376m  7              CachedDynamicNavBuilder.GetChildrenAsync => ¤{% END
2026-11-26T20:50:54.416 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        506m  6            FolderRecordProvider.BuildAsync END
2026-11-26T20:50:54.436 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        573m  5          FolderRecordProvider.GetAsync => ¤{% END
2026-11-26T20:50:54.518 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c        681m  4        ReaderAccess.GetFolderAsync END
2026-11-26T20:50:54.578 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c   60m        4        ReaderAccess.GetFolderAsync(prefix:"plugindeliveryservice/10-devops") START
2026-11-26T20:50:54.608 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   30m        5          FolderRecordProvider.GetAsync(prefix:"plugindeliveryservice/10-devops") START
2026-11-26T20:50:54.653 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   45m        6            FolderRecordProvider.BuildAsync(prefix:"plugindeliveryservice/10-devops") START
2026-11-26T20:50:54.704 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c   51m        7              CachedDynamicNavBuilder.GetChildrenAsync(prefix:"plugindeliveryservice") START
2026-11-26T20:50:54.744 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   40m        8                GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, operationOptions:SmartCacheOperationOptions{%, callerType:CachedDynamicNavBuilder) START
2026-11-26T20:50:54.798 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   54m        9                  GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, timestamp:$2026-11-26T20:50:54.0000000+00:00$, maybeMinimumCreationDate:$2026-11-26T20:45:54.0000000+00:00$, absExpiration:□, sldExpiration:□) START
2026-11-26T20:50:54.810 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   12m        9                   Cache entry found
2026-11-26T20:50:54.811 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c    1m        9                   Cache hit: valid creation date (minimum: 2026-11-26T20:45:54, newer: 2026-11-26T20:49:50)
2026-11-26T20:50:54.843 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   32m   61m  9                  GetAsync END
2026-11-26T20:50:54.885 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c        151m  8                GetAsync END
2026-11-26T20:50:54.944 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c        238m  7              CachedDynamicNavBuilder.GetChildrenAsync => ¤{% END
2026-11-26T20:50:55.054 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        385m  6            FolderRecordProvider.BuildAsync END
2026-11-26T20:50:55.172 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        538m  5          FolderRecordProvider.GetAsync => ¤{% END
2026-11-26T20:50:55.278 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c        696m  4        ReaderAccess.GetFolderAsync END
2026-11-26T20:50:55.335 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c   57m        4        ReaderAccess.GetFolderAsync(prefix:"plugindeliveryservice/11-appendix") START
2026-11-26T20:50:55.353 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   18m        5          FolderRecordProvider.GetAsync(prefix:"plugindeliveryservice/11-appendix") START
2026-11-26T20:50:55.373 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c   20m        6            FolderRecordProvider.BuildAsync(prefix:"plugindeliveryservice/11-appendix") START
2026-11-26T20:50:55.422 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c   49m        7              CachedDynamicNavBuilder.GetChildrenAsync(prefix:"plugindeliveryservice") START
2026-11-26T20:50:55.444 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   22m        8                GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, operationOptions:SmartCacheOperationOptions{%, callerType:CachedDynamicNavBuilder) START
2026-11-26T20:50:55.470 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   27m        9                  GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice"}, timestamp:$2026-11-26T20:50:55.0000000+00:00$, maybeMinimumCreationDate:$2026-11-26T20:45:55.0000000+00:00$, absExpiration:□, sldExpiration:□) START
2026-11-26T20:50:55.491 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   20m        9                   Cache entry found
2026-11-26T20:50:55.492 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c    1m        9                   Cache hit: valid creation date (minimum: 2026-11-26T20:45:55, newer: 2026-11-26T20:49:50)
2026-11-26T20:50:55.510 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   18m   42m  9                  GetAsync END
2026-11-26T20:50:55.529 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c         92m  8                GetAsync END
2026-11-26T20:50:55.574 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c        180m  7              CachedDynamicNavBuilder.GetChildrenAsync => ¤{% END
2026-11-26T20:50:55.596 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        224m  6            FolderRecordProvider.BuildAsync END
2026-11-26T20:50:55.637 …ocs.Web.Navigation.FolderRecordProvider DBUG fa8edc9578486dea5e09e1425b78190c        281m  5          FolderRecordProvider.GetAsync => ¤{% END
2026-11-26T20:50:55.686 …martDocs.Web.Authorization.ReaderAccess DBUG fa8edc9578486dea5e09e1425b78190c        342m  4        ReaderAccess.GetFolderAsync END
2026-11-26T20:50:55.785 …rtDocs.Web.Navigation.ServerNavProvider DBUG fa8edc9578486dea5e09e1425b78190c       6596m  3      ServerNavProvider.GetChildrenAsync END
2026-11-26T20:50:55.835 …rtDocs.Web.Navigation.ServerNavProvider DBUG fa8edc9578486dea5e09e1425b78190c   50m        3      ServerNavProvider.GetChildrenAsync(prefix:"plugindeliveryservice/02-getting-started") START
2026-11-26T20:50:55.854 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c   19m        4        CachedDynamicNavBuilder.GetChildrenAsync(prefix:"plugindeliveryservice/02-getting-started") START
2026-11-26T20:50:55.874 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   20m        5          GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice/02-getting-started"}, operationOptions:SmartCacheOperationOptions{%, callerType:CachedDynamicNavBuilder) START
2026-11-26T20:50:55.922 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   47m        6            GetAsync(key:ContentPathCacheKey{Kind:"nav-level-v2", Path:"plugindeliveryservice/02-getting-started"}, timestamp:$2026-11-26T20:50:55.0000000+00:00$, maybeMinimumCreationDate:$2026-11-26T20:45:55.0000000+00:00$, absExpiration:□, sldExpiration:□) START
2026-11-26T20:50:55.937 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   15m        6             Cache entry found
2026-11-26T20:50:55.946 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c    9m        6             Cache hit: valid creation date (minimum: 2026-11-26T20:45:55, newer: 2026-11-26T20:47:05)
2026-11-26T20:50:55.966 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c   20m   52m  6            GetAsync END
2026-11-26T20:50:56.055 Diginsight.SmartCache.SmartCache         DBUG fa8edc9578486dea5e09e1425b78190c        174m  5          GetAsync END
2026-11-26T20:50:56.131 ….Web.Navigation.CachedDynamicNavBuilder DBUG fa8edc9578486dea5e09e1425b78190c        247m  4        CachedDynamicNavBuilder.GetChildrenAsync => ¤{% END
2026-11-26T20:50:56.188 …rtDocs.Web.Navigation.ServerNavProvider DBUG fa8edc9578486dea5e09e1425b78190c        362m  3      ServerNavProvider.GetChildrenAsync END
2026-11-26T20:50:56.219 …ucture.ComponentStatePersistenceManager EROR fa8edc9578486dea5e09e1425b78190c   31m        2     There was an error executing a callback while pausing the application.
                                                                                                                          System.ArgumentException: There is already a persisted object under the same key '2TFhF9VIhLQe9Klz82t+Act0SvCKRlqv2nJUoSqG6RY='
                                                                                                                             at Microsoft.AspNetCore.Components.PersistentComponentState.PersistAsJson(String key, Object instance, Type type)
                                                                                                                             at Microsoft.AspNetCore.Components.Infrastructure.PersistentValueProviderComponentSubscription.PersistProperty()
                                                                                                                             at Microsoft.AspNetCore.Components.Infrastructure.ComponentStatePersistenceManager.<TryPauseAsync>g__TryExecuteCallback|21_0(Func`1 callback, ILogger`1 logger)

![
    

](image.png)

can yo help me understand why ? 
and fix it?

