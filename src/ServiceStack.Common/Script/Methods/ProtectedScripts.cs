using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using ServiceStack.IO;
using ServiceStack.Script;
using ServiceStack.Text;

namespace ServiceStack.Script
{
    // ReSharper disable InconsistentNaming
    
    public class ProtectedScripts : ScriptMethods
    {
        public MemoryVirtualFiles memoryVirtualFiles() => new MemoryVirtualFiles();

        public FileSystemVirtualFiles fileSystemVirtualFiles(string dirPath) => new FileSystemVirtualFiles(dirPath);
        
        public IVirtualFile ResolveFile(string filterName, ScriptScopeContext scope, string virtualPath)
        {
            var file = ResolveFile(scope.Context.VirtualFiles, scope.PageResult.VirtualPath, virtualPath);
            if (file == null)
                throw new FileNotFoundException($"{filterName} '{virtualPath}' in page '{scope.Page.VirtualPath}' was not found");

            return file;
        }

        public IVirtualFile ResolveFile(IVirtualPathProvider virtualFiles, string fromVirtualPath, string virtualPath)
        {
            IVirtualFile file = null;

            var pathMapKey = nameof(ResolveFile) + ">" + fromVirtualPath;
            var pathMapping = Context.GetPathMapping(pathMapKey, virtualPath);
            if (pathMapping != null)
            {
                file = virtualFiles.GetFile(pathMapping);
                if (file != null)
                    return file;                    
                Context.RemovePathMapping(pathMapKey, pathMapping);
            }

            var tryExactMatch = virtualPath.IndexOf('/') >= 0; //if nested path specified, look for an exact match first
            if (tryExactMatch)
            {
                file = virtualFiles.GetFile(virtualPath);
                if (file != null)
                {
                    Context.SetPathMapping(pathMapKey, virtualPath, virtualPath);
                    return file;
                }
            }

            var parentPath = fromVirtualPath.IndexOf('/') >= 0
                ? fromVirtualPath.LastLeftPart('/')
                : "";

            do
            {
                var seekPath = parentPath.CombineWith(virtualPath);
                file = virtualFiles.GetFile(seekPath);
                if (file != null)
                {
                    Context.SetPathMapping(pathMapKey, virtualPath, seekPath);
                    return file;
                }

                if (parentPath == "")
                    break;

                parentPath = parentPath.IndexOf('/') >= 0
                    ? parentPath.LastLeftPart('/')
                    : "";
            } while (true);

            return null;
        }

        //alias
        public Task fileContents(ScriptScopeContext scope, string virtualPath) => includeFile(scope, virtualPath);

        public async Task includeFile(ScriptScopeContext scope, string virtualPath)
        {
            var file = ResolveFile(nameof(includeFile), scope, virtualPath);
            using (var reader = file.OpenRead())
            {
                await reader.CopyToAsync(scope.OutputStream);
            }
        }

        public async Task ifDebugIncludeScript(ScriptScopeContext scope, string virtualPath)
        {
            if (scope.Context.DebugMode)
            {
                await scope.OutputStream.WriteAsync("<script>");
                await includeFile(scope, virtualPath);
                await scope.OutputStream.WriteAsync("</script>");
            }
        }

        IVirtualPathProvider VirtualFiles => Context.VirtualFiles;
        
        public IEnumerable<IVirtualFile> vfsAllFiles() => vfsAllFiles(VirtualFiles);
        public IEnumerable<IVirtualFile> vfsAllFiles(IVirtualPathProvider vfs) => vfs.GetAllFiles();

        public IEnumerable<IVirtualFile> vfsAllRootFiles() => vfsAllRootFiles(VirtualFiles);
        public IEnumerable<IVirtualFile> vfsAllRootFiles(IVirtualPathProvider vfs) => vfs.GetRootFiles();
        public IEnumerable<IVirtualDirectory> vfsAllRootDirectories() => vfsAllRootDirectories(VirtualFiles);
        public IEnumerable<IVirtualDirectory> vfsAllRootDirectories(IVirtualPathProvider vfs) => vfs.GetRootDirectories();
        public string vfsCombinePath(string basePath, string relativePath) => vfsCombinePath(VirtualFiles, basePath, relativePath);
        public string vfsCombinePath(IVirtualPathProvider vfs, string basePath, string relativePath) => vfs.CombineVirtualPath(basePath, relativePath);

        public IVirtualDirectory dir(string virtualPath) => dir(VirtualFiles,virtualPath);
        public IVirtualDirectory dir(IVirtualPathProvider vfs, string virtualPath) => vfs.GetDirectory(virtualPath);
        public bool dirExists(string virtualPath) => VirtualFiles.DirectoryExists(virtualPath);
        public bool dirExists(IVirtualPathProvider vfs, string virtualPath) => vfs.DirectoryExists(virtualPath);
        public IVirtualFile dirFile(string dirPath, string fileName) => dirFile(VirtualFiles,dirPath,fileName);
        public IVirtualFile dirFile(IVirtualPathProvider vfs, string dirPath, string fileName) => vfs.GetDirectory(dirPath)?.GetFile(fileName);
        public IEnumerable<IVirtualFile> dirFiles(string dirPath) => dirFiles(VirtualFiles,dirPath);
        public IEnumerable<IVirtualFile> dirFiles(IVirtualPathProvider vfs, string dirPath) => vfs.GetDirectory(dirPath)?.GetFiles() ?? new List<IVirtualFile>();
        public IVirtualDirectory dirDirectory(string dirPath, string dirName) => dirDirectory(VirtualFiles,dirPath,dirName);
        public IVirtualDirectory dirDirectory(IVirtualPathProvider vfs, string dirPath, string dirName) => vfs.GetDirectory(dirPath)?.GetDirectory(dirName);
        public IEnumerable<IVirtualDirectory> dirDirectories(string dirPath) => dirDirectories(VirtualFiles,dirPath);
        public IEnumerable<IVirtualDirectory> dirDirectories(IVirtualPathProvider vfs, string dirPath) => vfs.GetDirectory(dirPath)?.GetDirectories() ?? new List<IVirtualDirectory>();
        public IEnumerable<IVirtualFile> dirFilesFind(string dirPath, string globPattern) => dirFilesFind(VirtualFiles,dirPath,globPattern);
        public IEnumerable<IVirtualFile> dirFilesFind(IVirtualPathProvider vfs, string dirPath, string globPattern) => vfs.GetDirectory(dirPath)?.GetAllMatchingFiles(globPattern);

        public IEnumerable<IVirtualFile> filesFind(string globPattern) => filesFind(VirtualFiles,globPattern);
        public IEnumerable<IVirtualFile> filesFind(IVirtualPathProvider vfs, string globPattern) => vfs.GetAllMatchingFiles(globPattern);
        public bool fileExists(string virtualPath) => fileExists(VirtualFiles,virtualPath);
        public bool fileExists(IVirtualPathProvider vfs, string virtualPath) => vfs.FileExists(virtualPath);
        public IVirtualFile file(string virtualPath) => file(VirtualFiles,virtualPath);
        public IVirtualFile file(IVirtualPathProvider vfs, string virtualPath) => vfs.GetFile(virtualPath);
        public string fileWrite(string virtualPath, object contents) => fileWrite(VirtualFiles, virtualPath, contents);
        public string fileWrite(IVirtualPathProvider vfs, string virtualPath, object contents)
        {
            if (contents is string s)
                vfs.WriteFile(virtualPath, s);
            else if (contents is byte[] bytes)
                vfs.WriteFile(virtualPath, bytes);
            else if (contents is Stream stream)
                vfs.WriteFile(virtualPath, stream);
            else
                return null;

            return virtualPath;
        }

        public string fileAppend(string virtualPath, object contents) => fileAppend(VirtualFiles, virtualPath, contents);
        public string fileAppend(IVirtualPathProvider vfs, string virtualPath, object contents)
        {
            if (contents is string s)
                vfs.AppendFile(virtualPath, s);
            else if (contents is byte[] bytes)
                vfs.AppendFile(virtualPath, bytes);
            else if (contents is Stream stream)
                vfs.AppendFile(virtualPath, stream);
            else
                return null;

            return virtualPath;
        }

        public string fileDelete(string virtualPath) => fileDelete(VirtualFiles, virtualPath);
        public string fileDelete(IVirtualPathProvider vfs, string virtualPath)
        {
            vfs.DeleteFile(virtualPath);
            return virtualPath;
        }

        public string dirDelete(string virtualPath) => fileDelete(VirtualFiles, virtualPath);
        public string dirDelete(IVirtualPathProvider vfs, string virtualPath)
        {
            vfs.DeleteFolder(virtualPath);
            return virtualPath;
        }

        public string fileReadAll(string virtualPath) => fileReadAll(VirtualFiles,virtualPath);
        public string fileReadAll(IVirtualPathProvider vfs, string virtualPath) => vfs.GetFile(virtualPath)?.ReadAllText();
        public byte[] fileReadAllBytes(string virtualPath) => fileReadAllBytes(VirtualFiles, virtualPath);
        public byte[] fileReadAllBytes(IVirtualPathProvider vfs, string virtualPath) => vfs.GetFile(virtualPath)?.ReadAllBytes();
        public string fileHash(string virtualPath) => fileHash(VirtualFiles,virtualPath);
        public string fileHash(IVirtualPathProvider vfs, string virtualPath) => vfs.GetFileHash(virtualPath);

        //alias
        public Task urlContents(ScriptScopeContext scope, string url) => includeUrl(scope, url, null);
        public Task urlContents(ScriptScopeContext scope, string url, object options) => includeUrl(scope, url, options);

        public Task includeUrl(ScriptScopeContext scope, string url) => includeUrl(scope, url, null);
        public async Task includeUrl(ScriptScopeContext scope, string url, object options)
        {
            var scopedParams = scope.AssertOptions(nameof(includeUrl), options);

            var webReq = (HttpWebRequest)WebRequest.Create(url);
            var dataType = scopedParams.TryGetValue("dataType", out object value)
                ? ConvertDataTypeToContentType((string)value)
                : null;

            if (scopedParams.TryGetValue("method", out value))
                webReq.Method = (string)value;
            if (scopedParams.TryGetValue("contentType", out value) || dataType != null)
                webReq.ContentType = (string)value ?? dataType;            
            if (scopedParams.TryGetValue("accept", out value) || dataType != null) 
                webReq.Accept = (string)value ?? dataType;            
            if (scopedParams.TryGetValue("userAgent", out value))
                PclExport.Instance.SetUserAgent(webReq, (string)value);

            if (scopedParams.TryRemove("data", out object data))
            {
                if (webReq.Method == null)
                    webReq.Method = HttpMethods.Post;
                    
                if (webReq.ContentType == null)
                    webReq.ContentType = MimeTypes.FormUrlEncoded;

                var body = ConvertDataToString(data, webReq.ContentType);
                using (var stream = await webReq.GetRequestStreamAsync())
                {
                    await stream.WriteAsync(body);
                }
            }

            using (var webRes = await webReq.GetResponseAsync())
            using (var stream = webRes.GetResponseStream())
            {
                await stream.CopyToAsync(scope.OutputStream);
            }
        }

        private static string ConvertDataTypeToContentType(string dataType)
        {
            switch (dataType)
            {
                case "json":
                    return MimeTypes.Json;
                case "jsv":
                    return MimeTypes.Jsv;
                case "csv":
                    return MimeTypes.Csv;
                case "xml":
                    return MimeTypes.Xml;
                case "text":
                    return MimeTypes.PlainText;
                case "form":
                    return MimeTypes.FormUrlEncoded;
            }
            
            throw new NotSupportedException($"Unknown dataType '{dataType}'");
        }

        private static string ConvertDataToString(object data, string contentType)
        {
            if (data is string s)
                return s;
            switch (contentType)
            {
                case MimeTypes.PlainText:
                    return data.ToString();
                case MimeTypes.Json:
                    return data.ToJson();
                case MimeTypes.Csv:
                    return data.ToCsv();
                case MimeTypes.Jsv:
                    return data.ToJsv();
                case MimeTypes.Xml:
                    return data.ToXml();
                case MimeTypes.FormUrlEncoded:
                    WriteComplexTypeDelegate holdQsStrategy = QueryStringStrategy.FormUrlEncoded;
                    QueryStringSerializer.ComplexTypeStrategy = QueryStringStrategy.FormUrlEncoded;
                    var urlEncodedBody = QueryStringSerializer.SerializeToString(data);
                    QueryStringSerializer.ComplexTypeStrategy = holdQsStrategy;
                    return urlEncodedBody;
            }

            throw new NotSupportedException($"Can not serialize to unknown Content-Type '{contentType}'");
        }

        public static string CreateCacheKey(string url, Dictionary<string,object> options=null)
        {
            var sb = StringBuilderCache.Allocate()
                .Append(url);
            
            if (options != null)
            {
                foreach (var entry in options)
                {
                    sb.Append(entry.Key)
                      .Append('=')
                      .Append(entry.Value);
                }
            }

            return StringBuilderCache.ReturnAndFree(sb);
        }
        
        //alias
        public Task fileContentsWithCache(ScriptScopeContext scope, string virtualPath) => includeFileWithCache(scope, virtualPath, null);
        public Task fileContentsWithCache(ScriptScopeContext scope, string virtualPath, object options) => includeFileWithCache(scope, virtualPath, options);

        public Task includeFileWithCache(ScriptScopeContext scope, string virtualPath) => includeFileWithCache(scope, virtualPath, null);
        public async Task includeFileWithCache(ScriptScopeContext scope, string virtualPath, object options)
        {
            var scopedParams = scope.AssertOptions(nameof(includeUrl), options);
            var expireIn = scopedParams.TryGetValue("expireInSecs", out object value)
                ? TimeSpan.FromSeconds(value.ConvertTo<int>())
                : (TimeSpan)scope.Context.Args[ScriptConstants.DefaultFileCacheExpiry];
            
            var cacheKey = CreateCacheKey("file:" + scope.PageResult.VirtualPath + ">" + virtualPath, scopedParams);
            if (Context.ExpiringCache.TryGetValue(cacheKey, out Tuple<DateTime, object> cacheEntry))
            {
                if (cacheEntry.Item1 > DateTime.UtcNow && cacheEntry.Item2 is byte[] bytes)
                {
                    await scope.OutputStream.WriteAsync(bytes);
                    return;
                }
            }

            var file = ResolveFile(nameof(includeFileWithCache), scope, virtualPath);
            var ms = MemoryStreamFactory.GetStream();
            using (ms)
            {
                using (var reader = file.OpenRead())
                {
                    await reader.CopyToAsync(ms);
                }

                ms.Position = 0;
                var bytes = ms.ToArray();
                Context.ExpiringCache[cacheKey] = Tuple.Create(DateTime.UtcNow.Add(expireIn),(object)bytes);
                await scope.OutputStream.WriteAsync(bytes);
            }
        }

        //alias
        public Task urlContentsWithCache(ScriptScopeContext scope, string url) => includeUrlWithCache(scope, url, null);
        public Task urlContentsWithCache(ScriptScopeContext scope, string url, object options) => includeUrlWithCache(scope, url, options);
        
        public Task includeUrlWithCache(ScriptScopeContext scope, string url) => includeUrlWithCache(scope, url, null);
        public async Task includeUrlWithCache(ScriptScopeContext scope, string url, object options)
        {
            var scopedParams = scope.AssertOptions(nameof(includeUrl), options);
            var expireIn = scopedParams.TryGetValue("expireInSecs", out object value)
                ? TimeSpan.FromSeconds(value.ConvertTo<int>())
                : (TimeSpan)scope.Context.Args[ScriptConstants.DefaultUrlCacheExpiry];

            var cacheKey = CreateCacheKey("url:" + url, scopedParams);
            if (Context.ExpiringCache.TryGetValue(cacheKey, out Tuple<DateTime, object> cacheEntry))
            {
                if (cacheEntry.Item1 > DateTime.UtcNow && cacheEntry.Item2 is byte[] bytes)
                {
                    await scope.OutputStream.WriteAsync(bytes);
                    return;
                }
            }

            var dataType = scopedParams.TryGetValue("dataType", out value)
                ? ConvertDataTypeToContentType((string)value)
                : null;

            if (scopedParams.TryGetValue("method", out value) && !((string)value).EqualsIgnoreCase("GET"))
                throw new NotSupportedException($"Only GET requests can be used in {nameof(includeUrlWithCache)} filters in page '{scope.Page.VirtualPath}'");
            if (scopedParams.TryGetValue("data", out value))
                throw new NotSupportedException($"'data' is not supported in {nameof(includeUrlWithCache)} filters in page '{scope.Page.VirtualPath}'");

            var ms = MemoryStreamFactory.GetStream();
            using (ms)
            {
                var captureScope = scope.ScopeWithStream(ms);
                await includeUrl(captureScope, url, options);

                ms.Position = 0;
                var expireAt = DateTime.UtcNow.Add(expireIn);

                var bytes = ms.ToArray();
                Context.ExpiringCache[cacheKey] = cacheEntry = Tuple.Create(expireAt,(object)bytes);
                await scope.OutputStream.WriteAsync(bytes);
            }
        }
        
        static readonly string[] AllCacheNames = {
            nameof(ScriptContext.Cache),
            nameof(ScriptContext.CacheMemory),
            nameof(ScriptContext.ExpiringCache),
            nameof(SharpPageUtils.BinderCache),
            nameof(ScriptContext.JsTokenCache),
            nameof(ScriptContext.AssignExpressionCache),
            nameof(ScriptContext.PathMappings),
        };

        internal IDictionary GetCache(string cacheName)
        {
            switch (cacheName)
            {
                case nameof(ScriptContext.Cache):
                    return Context.Cache;
                case nameof(ScriptContext.CacheMemory):
                    return Context.CacheMemory;
                case nameof(ScriptContext.ExpiringCache):
                    return Context.ExpiringCache;
                case nameof(SharpPageUtils.BinderCache):
                    return SharpPageUtils.BinderCache;
                case nameof(ScriptContext.JsTokenCache):
                    return Context.JsTokenCache;
                case nameof(ScriptContext.AssignExpressionCache):
                    return Context.AssignExpressionCache;
                case nameof(ScriptContext.PathMappings):
                    return Context.PathMappings;
            }
            return null;
        }

        public object cacheClear(ScriptScopeContext scope, object cacheNames)
        {
            IEnumerable<string> caches;
            if (cacheNames is string strName)
            {
                caches = strName.EqualsIgnoreCase("all")
                    ? AllCacheNames
                    : new[]{ strName };
            }
            else if (cacheNames is IEnumerable<string> nameList)
            {
                caches = nameList;
            }
            else throw new NotSupportedException(nameof(cacheClear) + 
                 " expects a cache name or list of cache names but received: " + (cacheNames.GetType()?.Name ?? "null"));

            int entriesRemoved = 0;
            foreach (var cacheName in caches)
            {
                var cache = GetCache(cacheName);
                if (cache == null)
                    throw new NotSupportedException(nameof(cacheClear) + $": Unknown cache '{cacheName}'");

                entriesRemoved += cache.Count;
                cache.Clear();
            }

            return entriesRemoved;
        }

        public object invalidateAllCaches(ScriptScopeContext scope)
        {
            cacheClear(scope, "all");
            return scope.Context.InvalidateCachesBefore = DateTime.UtcNow;
        }
    }
}