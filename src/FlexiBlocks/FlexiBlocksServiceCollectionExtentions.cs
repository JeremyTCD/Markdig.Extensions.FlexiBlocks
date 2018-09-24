﻿using Jering.IocServices.Newtonsoft.Json;
using Jering.IocServices.System.IO;
using Jering.IocServices.System.Net.Http;
using Jering.Markdig.Extensions.FlexiBlocks.FlexiAlertBlocks;
using Jering.Markdig.Extensions.FlexiBlocks.FlexiCodeBlocks;
using Jering.Markdig.Extensions.FlexiBlocks.FlexiIncludeBlocks;
using Jering.Markdig.Extensions.FlexiBlocks.FlexiOptionsBlocks;
using Jering.Web.SyntaxHighlighters.HighlightJS;
using Jering.Web.SyntaxHighlighters.Prism;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Jering.Markdig.Extensions.FlexiBlocks
{
    public static class FlexiBlocksServiceCollectionExtentions
    {
        public static void AddFlexiBlocks(this IServiceCollection services)
        {
            // Shared
            services.AddLogging();
            services.AddOptions();
            services.TryAddSingleton<IFileService, FileService>();
            services.TryAddSingleton<IDirectoryService, DirectoryService>();
            services.TryAddSingleton<IHttpClientService, HttpClientService>();
            services.TryAddSingleton<IJsonSerializerService, JsonSerializerService>();
            services.AddSingleton(typeof(IOptions<>), typeof(ExposedOptionsManager<>));

            // FlexAlertBlocks
            services.TryAddTransient<FlexiAlertBlockParser>();
            services.TryAddTransient<FlexiAlertBlockRenderer>();
            services.TryAddTransient<FlexiAlertBlocksExtension>();

            // FlexiCodeBlocks
            services.AddPrism();
            services.AddHighlightJS();
            services.TryAddSingleton<ILineEmbellisherService, LineEmbellisherService>();
            services.TryAddTransient<FlexiCodeBlockRenderer>();
            services.TryAddTransient<FlexiCodeBlocksExtension>();

            // FlexiIncludeBlocks
            services.TryAddSingleton<IDiskCacheService, DiskCacheService>();
            services.TryAddSingleton<ISourceRetrieverService, SourceRetrieverService>();
            services.TryAddSingleton<ILeadingWhitespaceEditorService, LeadingWhitespaceEditorService>();
            services.TryAddTransient<FlexiIncludeBlockParser>();
            services.TryAddTransient<FlexiIncludeBlocksExtension>();

            // FlexiOptionsBlocks
            services.TryAddSingleton<IFlexiOptionsBlockService, FlexiOptionsBlockService>();
            services.TryAddTransient<FlexiOptionsBlockParser>();
            services.TryAddTransient<FlexiOptionsBlocksExtension>();
        }
    }
}
