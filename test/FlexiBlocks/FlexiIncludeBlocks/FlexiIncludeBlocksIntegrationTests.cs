﻿using Jering.Markdig.Extensions.FlexiBlocks.FlexiAlertBlocks;
using Jering.Markdig.Extensions.FlexiBlocks.FlexiIncludeBlocks;
using Jering.Markdig.Extensions.FlexiBlocks.FlexiOptionsBlocks;
using Markdig;
using Markdig.Parsers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Jering.Markdig.Extensions.FlexiBlocks.Tests.FlexiIncludeBlocks
{
    // Integration tests that don't fit in amongst the specs.
    public class FlexiIncludeBlocksIntegrationTests : IClassFixture<FlexiIncludeBlocksIntegrationTestsFixture>
    {
        private readonly FlexiIncludeBlocksIntegrationTestsFixture _fixture;

        public FlexiIncludeBlocksIntegrationTests(FlexiIncludeBlocksIntegrationTestsFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void FlexiIncludeBlocks_ConstructsAndExposesFlexiIncludeBlockTrees()
        {
            // Arrange
            const string dummyMarkdown1SourceUri = "./dummyMarkdown1.md";
            const string dummyMarkdown2SourceUri = "./dummyMarkdown2.md";
            const string dummyMarkdown3SourceUri = "./dummyMarkdown3.md";
            string dummyEntryMarkdown = $@"+{{
""type"": ""markdown"",
""sourceUri"": ""{dummyMarkdown1SourceUri}"",
}}

+{{
""type"": ""markdown"",
""sourceUri"": ""{dummyMarkdown2SourceUri}""
}}";
            const string dummyMarkdown1 = "This is dummy markdown";
            string dummyMarkdown2 = $@"+{{
""type"": ""markdown"",
""sourceUri"": ""{dummyMarkdown3SourceUri}"",
}}";
            const string dummyMarkdown3 = "This is dummy markdown";
            string dummyMarkdown1Path = Path.Combine(_fixture.TempDirectory, $"{nameof(dummyMarkdown1)}.md");
            string dummyMarkdown2Path = Path.Combine(_fixture.TempDirectory, $"{nameof(dummyMarkdown2)}.md");
            string dummyMarkdown3Path = Path.Combine(_fixture.TempDirectory, $"{nameof(dummyMarkdown3)}.md");
            File.WriteAllText(dummyMarkdown1Path, dummyMarkdown1);
            File.WriteAllText(dummyMarkdown2Path, dummyMarkdown2);
            File.WriteAllText(dummyMarkdown3Path, dummyMarkdown3);

            // Need to dispose of services after each test so that the in-memory cache doesn't affect results
            var services = new ServiceCollection();
            services.AddFlexiBlocks();
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            using ((IDisposable)serviceProvider)
            {
                var dummyExtensionOptions = new FlexiIncludeBlocksExtensionOptions { RootBaseUri = _fixture.TempDirectory + "/" };
                IFlexiBlocksExtensionFactory<FlexiIncludeBlocksExtension, FlexiIncludeBlocksExtensionOptions> extensionFactory =
                    serviceProvider.GetRequiredService<IFlexiBlocksExtensionFactory<FlexiIncludeBlocksExtension, FlexiIncludeBlocksExtensionOptions>>();
                var dummyMarkdownPipelineBuilder = new MarkdownPipelineBuilder();
                dummyMarkdownPipelineBuilder.Extensions.Add(extensionFactory.Build(dummyExtensionOptions));
                MarkdownPipeline dummyMarkdownPipeline = dummyMarkdownPipelineBuilder.Build();

                // Act
                Markdown.ToHtml(dummyEntryMarkdown, dummyMarkdownPipeline);

                // Assert
                FlexiIncludeBlocksExtension extension = dummyMarkdownPipeline.Extensions.FindExact<FlexiIncludeBlocksExtension>();
                List<FlexiIncludeBlock> trees = extension.GetFlexiIncludeBlockTrees();
                Assert.Equal(2, trees.Count);
                // First FlexiIncludeBlock just includes markdown 1
                Assert.Equal(dummyMarkdown1SourceUri, trees[0].FlexiIncludeBlockOptions.SourceUri);
                // Second FlexiIncludeBlock includes markdown 2, which includes markdown 3
                Assert.Equal(dummyMarkdown2SourceUri, trees[1].FlexiIncludeBlockOptions.SourceUri);
                Assert.Single(trees[1].ChildFlexiIncludeBlocks);
                Assert.Equal(dummyMarkdown3SourceUri, trees[1].ChildFlexiIncludeBlocks[0].FlexiIncludeBlockOptions.SourceUri);
                Assert.Equal(trees[1], trees[1].ChildFlexiIncludeBlocks[0].ParentFlexiIncludeBlock);
                // Convenience method collates source absolute URIs
                HashSet<string> includedSourceAbsoluteURIs = extension.GetIncludedSourcesAbsoluteUris();
                Assert.Contains(new Uri(dummyMarkdown1Path).AbsoluteUri, includedSourceAbsoluteURIs);
                Assert.Contains(new Uri(dummyMarkdown2Path).AbsoluteUri, includedSourceAbsoluteURIs);
                Assert.Contains(new Uri(dummyMarkdown3Path).AbsoluteUri, includedSourceAbsoluteURIs);
            }
        }

        [Theory]
        [MemberData(nameof(FlexiIncludeBlocks_ThrowsFlexiIncludeBlocksExceptionIfACycleIsFound_Data))]
        public void FlexiIncludeBlocks_ThrowsFlexiIncludeBlocksExceptionIfACycleIsFound(string dummyEntryMarkdown, int dummyEntryOffendingFIBLineNum,
            string dummyMarkdown1, int dummyMarkdown1OffendingFIBLineNum,
            string dummyMarkdown2, int dummyMarkdown2OffendingFIBLineNum,
            string dummyMarkdown3,
            string expectedCycleDescription)
        {
            // Arrange
            File.WriteAllText(Path.Combine(_fixture.TempDirectory, $"{nameof(dummyMarkdown1)}.md"), dummyMarkdown1);
            File.WriteAllText(Path.Combine(_fixture.TempDirectory, $"{nameof(dummyMarkdown2)}.md"), dummyMarkdown2);
            File.WriteAllText(Path.Combine(_fixture.TempDirectory, $"{nameof(dummyMarkdown3)}.md"), dummyMarkdown3);

            // Need to dispose of services after each test so that the in-memory cache doesn't affect results
            var services = new ServiceCollection();
            services.AddFlexiBlocks();
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            using ((IDisposable)serviceProvider)
            {
                var dummyExtensionOptions = new FlexiIncludeBlocksExtensionOptions { RootBaseUri = _fixture.TempDirectory + "/" };
                IFlexiBlocksExtensionFactory<FlexiIncludeBlocksExtension, FlexiIncludeBlocksExtensionOptions> extensionFactory =
                    serviceProvider.GetRequiredService<IFlexiBlocksExtensionFactory<FlexiIncludeBlocksExtension, FlexiIncludeBlocksExtensionOptions>>();
                var dummyMarkdownPipelineBuilder = new MarkdownPipelineBuilder();
                dummyMarkdownPipelineBuilder.Extensions.Add(extensionFactory.Build(dummyExtensionOptions));
                MarkdownPipeline dummyMarkdownPipeline = dummyMarkdownPipelineBuilder.Build();
                var dummyRootBaseUri = new Uri(dummyExtensionOptions.RootBaseUri);
                string dummyMarkdown1SourceUri = dummyRootBaseUri + $"{nameof(dummyMarkdown1)}.md";
                string dummyMarkdown2SourceUri = dummyRootBaseUri + $"{nameof(dummyMarkdown2)}.md";

                // Act and assert
                FlexiBlocksException result = Assert.Throws<FlexiBlocksException>(() => MarkdownParser.Parse(dummyEntryMarkdown, dummyMarkdownPipeline));
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiBlocksException_InvalidFlexiBlock, nameof(FlexiIncludeBlock), dummyEntryOffendingFIBLineNum, 0,
                        string.Format(Strings.FlexiBlocksException_FlexiIncludeBlockParser_ExceptionOccurredWhileProcessingSource,
                            dummyMarkdown1SourceUri)),
                    result.Message);
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiBlocksException_InvalidFlexiBlock, nameof(FlexiIncludeBlock), dummyMarkdown1OffendingFIBLineNum, 0,
                        string.Format(Strings.FlexiBlocksException_FlexiIncludeBlockParser_ExceptionOccurredWhileProcessingSource,
                            dummyMarkdown2SourceUri)),
                    result.InnerException.Message);
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiBlocksException_InvalidFlexiBlock, nameof(FlexiIncludeBlock), dummyMarkdown2OffendingFIBLineNum, 0,
                        string.Format(Strings.FlexiBlocksException_FlexiIncludeBlockParser_ExceptionOccurredWhileProcessingSource,
                            dummyMarkdown1SourceUri)),
                    result.InnerException.InnerException.Message);
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiBlocksException_InvalidFlexiBlock, nameof(FlexiIncludeBlock), dummyMarkdown1OffendingFIBLineNum, 0,
                        Strings.FlexiBlocksException_FlexiIncludeBlockParser_ExceptionOccurredWhileProcessingBlock),
                    result.InnerException.InnerException.InnerException.Message);
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiIncludeBlockParser_CycleFound,
                        string.Format(expectedCycleDescription, dummyMarkdown1SourceUri, dummyMarkdown2SourceUri)),
                    result.InnerException.InnerException.InnerException.InnerException.Message,
                    ignoreLineEndingDifferences: true);
            }
        }

        public static IEnumerable<object[]> FlexiIncludeBlocks_ThrowsFlexiIncludeBlocksExceptionIfACycleIsFound_Data()
        {
            return new object[][]
            {
                // Basic circular include
                new object[]
                {
                    @"+{
""type"": ""markdown"",
""sourceUri"": ""./dummyMarkdown1.md""
}",
                    1,
                    @"+{
""type"": ""markdown"",
""sourceUri"": ""./dummyMarkdown2.md""
}",
                    1,
                    @"+{
""type"": ""markdown"",
""sourceUri"": ""./dummyMarkdown1.md""
}",
                    1,
                    null,
                    @"Source URI: {0}, Line: 1 >
Source URI: {1}, Line: 1 >
Source URI: {0}, Line: 1"
                },
                // Valid includes don't affect identification of circular includes
                new object[]
                {
                    @"+{
""type"": ""markdown"",
""sourceUri"": ""./dummyMarkdown1.md"",
""clippings"": [{""startLineNumber"": 2, ""endLineNumber"": 2}]
}

+{
""type"": ""markdown"",
""sourceUri"": ""./dummyMarkdown1.md""
}",
                    7,
                    @"+{
""type"": ""markdown"",
""sourceUri"": ""./dummyMarkdown3.md""
}

+{
""type"": ""markdown"",
""sourceUri"": ""./dummyMarkdown2.md""
}",
                    6,
                    @"+{
""type"": ""Code"",
""sourceUri"": ""./dummyMarkdown1.md""
}

+{
""type"": ""markdown"",
""sourceUri"": ""./dummyMarkdown1.md""
}",
                    6,
                    "This is a line",
                    @"Source URI: {0}, Line: 6 >
Source URI: {1}, Line: 6 >
Source URI: {0}, Line: 6"
                },
                // Circular includes that uses clippings are caught
                new object[]
                {
                    @"+{
""type"": ""markdown"",
""sourceUri"": ""./dummyMarkdown1.md"",
""clippings"": [{""startLineNumber"": 2, ""endLineNumber"": 2}]
}

+{
""type"": ""markdown"",
""sourceUri"": ""./dummyMarkdown1.md"",
""clippings"": [{""startLineNumber"": 6, ""endLineNumber"": -1}]
}",
                    7,
                    @"+{
""type"": ""markdown"",
""sourceUri"": ""./dummyMarkdown3.md""
}

+{
""type"": ""markdown"",
""sourceUri"": ""./dummyMarkdown2.md"",
""clippings"": [{""startLineNumber"": 6, ""endLineNumber"": -1}]
}",
                    6,
                    @"+{
""type"": ""Code"",
""sourceUri"": ""./dummyMarkdown1.md""
}

+{
""type"": ""markdown"",
""sourceUri"": ""./dummyMarkdown1.md""
}",
                    6,
                    "This is a line",
                    @"Source URI: {0}, Line: 6 >
Source URI: {1}, Line: 6 >
Source URI: {0}, Line: 6"
                }
            };
        }

        // This test is similar to the theory above.The thing is that, messages differ for before/after content.
        // The exception chain is stupidly long. It is a cycle, and we need the context for each FlexiIncludeBlock, but
        // some kind of simplification should be attempted if time permits.
        [Fact]
        public void FlexiIncludeBlocks_ThrowsFlexiIncludeBlocksExceptionIfACycleThatPassesThroughBeforeOrAfterContentIsFound()
        {
            // Arrange
            const string dummyEntryMarkdown = @"+{
    ""type"": ""markdown"",
    ""sourceUri"": ""./dummyMarkdown1.md""
}";
            const int dummyEntryOffendingFIBLineNum = 1;
            const string dummyMarkdown1 = @"+{
    ""type"": ""markdown"",
    ""sourceUri"": ""./dummyMarkdown3.md"",
    ""clippings"": [{
                        ""beforeContent"": ""This is a line.
+{
                            \""type\"": \""markdown\"",
                            \""sourceUri\"": \""./dummyMarkdown2.md\""
                        }""
                    }]
}";
            const int dummyMarkdown1OffendingFIBLineNum = 1;
            const string dummyMarkdown2 = @"+{
    ""type"": ""markdown"",
    ""sourceUri"": ""./dummyMarkdown3.md"",
    ""clippings"": [{
                        ""afterContent"": ""+{
                            \""type\"": \""markdown\"",
                            \""sourceUri\"": \""./dummyMarkdown1.md\""
                        }""
                    }]
}";
            const string dummyMarkdown3 = "This is a line.";
            const string expectedCycleDescription = @"Source URI: {0}, Line: 1 >
Source URI: {0}, Line: 1, BeforeContent >
Source URI: {1}, Line: 1 >
Source URI: {1}, Line: 1, AfterContent >
Source URI: {0}, Line: 1";
            File.WriteAllText(Path.Combine(_fixture.TempDirectory, $"{nameof(dummyMarkdown1)}.md"), dummyMarkdown1);
            File.WriteAllText(Path.Combine(_fixture.TempDirectory, $"{nameof(dummyMarkdown2)}.md"), dummyMarkdown2);
            File.WriteAllText(Path.Combine(_fixture.TempDirectory, $"{nameof(dummyMarkdown3)}.md"), dummyMarkdown3);

            // Need to dispose of services after each test so that the in-memory cache doesn't affect results
            var services = new ServiceCollection();
            services.AddFlexiBlocks();
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            using ((IDisposable)serviceProvider)
            {
                var dummyExtensionOptions = new FlexiIncludeBlocksExtensionOptions { RootBaseUri = _fixture.TempDirectory + "/" };
                IFlexiBlocksExtensionFactory<FlexiIncludeBlocksExtension, FlexiIncludeBlocksExtensionOptions> extensionFactory =
                    serviceProvider.GetRequiredService<IFlexiBlocksExtensionFactory<FlexiIncludeBlocksExtension, FlexiIncludeBlocksExtensionOptions>>();
                var dummyMarkdownPipelineBuilder = new MarkdownPipelineBuilder();
                dummyMarkdownPipelineBuilder.Extensions.Add(extensionFactory.Build(dummyExtensionOptions));
                MarkdownPipeline dummyMarkdownPipeline = dummyMarkdownPipelineBuilder.Build();
                var dummyRootBaseUri = new Uri(dummyExtensionOptions.RootBaseUri);
                string dummyMarkdown1SourceUri = dummyRootBaseUri + $"{nameof(dummyMarkdown1)}.md";
                string dummyMarkdown2SourceUri = dummyRootBaseUri + $"{nameof(dummyMarkdown2)}.md";

                // Act and assert
                FlexiBlocksException result = Assert.Throws<FlexiBlocksException>(() => MarkdownParser.Parse(dummyEntryMarkdown, dummyMarkdownPipeline));
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiBlocksException_InvalidFlexiBlock, nameof(FlexiIncludeBlock), dummyEntryOffendingFIBLineNum, 0,
                        string.Format(Strings.FlexiBlocksException_FlexiIncludeBlockParser_ExceptionOccurredWhileProcessingSource,
                            dummyMarkdown1SourceUri)),
                    result.Message);
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiBlocksException_InvalidFlexiBlock, nameof(FlexiIncludeBlock), dummyMarkdown1OffendingFIBLineNum, 0,
                        string.Format(Strings.FlexiBlocksException_FlexiIncludeBlockParser_ExceptionOccurredWhileProcessingContent, nameof(ClippingProcessingStage.BeforeContent))),
                    result.InnerException.Message);
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiBlocksException_InvalidFlexiBlock, nameof(FlexiIncludeBlock), dummyMarkdown1OffendingFIBLineNum, 0,
                        string.Format(Strings.FlexiBlocksException_FlexiIncludeBlockParser_ExceptionOccurredWhileProcessingSource,
                            dummyMarkdown2SourceUri)),
                    result.InnerException.InnerException.Message);
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiBlocksException_InvalidFlexiBlock, nameof(FlexiIncludeBlock), dummyMarkdown1OffendingFIBLineNum, 0,
                        string.Format(Strings.FlexiBlocksException_FlexiIncludeBlockParser_ExceptionOccurredWhileProcessingContent, nameof(ClippingProcessingStage.AfterContent))),
                    result.InnerException.InnerException.InnerException.Message);
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiBlocksException_InvalidFlexiBlock, nameof(FlexiIncludeBlock), dummyMarkdown1OffendingFIBLineNum, 0,
                        string.Format(Strings.FlexiBlocksException_FlexiIncludeBlockParser_ExceptionOccurredWhileProcessingSource,
                            dummyMarkdown1SourceUri)),
                    result.InnerException.InnerException.InnerException.InnerException.Message);
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiBlocksException_InvalidFlexiBlock, nameof(FlexiIncludeBlock), dummyMarkdown1OffendingFIBLineNum, 0,
                        Strings.FlexiBlocksException_FlexiIncludeBlockParser_ExceptionOccurredWhileProcessingBlock),
                    result.InnerException.InnerException.InnerException.InnerException.InnerException.Message);
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiIncludeBlockParser_CycleFound,
                        string.Format(expectedCycleDescription, dummyMarkdown1SourceUri, dummyMarkdown2SourceUri)),
                    result.InnerException.InnerException.InnerException.InnerException.InnerException.InnerException.Message,
                    ignoreLineEndingDifferences: true);
            }
        }

        [Fact]
        public void FlexiIncludeBlocks_ThrowsFlexiIncludeBlocksExceptionIfAnIncludedSourceHasInvalidBlocks()
        {
            // Arrange
            const string dummyClassesFormat = "dummy-{0}-{1}";
            const string dummyEntryMarkdown = @"+{
    ""type"": ""markdown"",
    ""sourceUri"": ""./dummyMarkdown1.md""
}";
            string dummyMarkdown1 = $@"@{{
    ""classesFormat"": ""{dummyClassesFormat}""
}}
! This is a FlexiAlertBlock.
";
            // Need to dispose of services after each test so that the in-memory cache doesn't affect results
            var services = new ServiceCollection();
            services.AddFlexiBlocks();
            IServiceProvider serviceProvider = services.BuildServiceProvider();
            using ((IDisposable)serviceProvider)
            {
                var dummyExtensionOptions = new FlexiIncludeBlocksExtensionOptions { RootBaseUri = _fixture.TempDirectory + "/" };
                IFlexiBlocksExtensionFactory<FlexiIncludeBlocksExtension, FlexiIncludeBlocksExtensionOptions> extensionFactory =
                    serviceProvider.GetRequiredService<IFlexiBlocksExtensionFactory<FlexiIncludeBlocksExtension, FlexiIncludeBlocksExtensionOptions>>();
                var dummyMarkdownPipelineBuilder = new MarkdownPipelineBuilder();
                dummyMarkdownPipelineBuilder.Extensions.Add(extensionFactory.Build(dummyExtensionOptions));
                dummyMarkdownPipelineBuilder.
                    UseFlexiAlertBlocks().
                    UseFlexiOptionsBlocks();
                MarkdownPipeline dummyMarkdownPipeline = dummyMarkdownPipelineBuilder.Build();
                var dummyRootBaseUri = new Uri(dummyExtensionOptions.RootBaseUri);
                File.WriteAllText(Path.Combine(dummyRootBaseUri.AbsolutePath, $"{nameof(dummyMarkdown1)}.md"), dummyMarkdown1);

                // Act and assert
                FlexiBlocksException result = Assert.Throws<FlexiBlocksException>(() => MarkdownParser.Parse(dummyEntryMarkdown, dummyMarkdownPipeline));
                // From bottom to top, this is the exception chain: 
                // FormatException > FlexiBlocksException for invalid option > FlexiBlocksException for invalid FlexiOptionsBlock > FlexiBlocksException for invalid FlexiIncludeBlock
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiBlocksException_InvalidFlexiBlock, nameof(FlexiIncludeBlock), 1, 0,
                        string.Format(Strings.FlexiBlocksException_FlexiIncludeBlockParser_ExceptionOccurredWhileProcessingSource,
                            dummyRootBaseUri + $"{nameof(dummyMarkdown1)}.md")),
                    result.Message);
                Assert.Equal(string.Format(Strings.FlexiBlocksException_FlexiBlocksException_InvalidFlexiBlock, nameof(FlexiOptionsBlock), 1, 0, Strings.FlexiBlocksException_FlexiBlocksException_ExceptionOccurredWhileProcessingABlock),
                    result.InnerException.Message);
                Assert.Equal(string.Format(Strings.FlexiBlocksException_Shared_OptionIsAnInvalidFormat, nameof(FlexiAlertBlockOptions.ClassesFormat), dummyClassesFormat),
                    result.InnerException.InnerException.Message);
                Assert.IsType<FormatException>(result.InnerException.InnerException.InnerException);
            }
        }
    }

    public class FlexiIncludeBlocksIntegrationTestsFixture : IDisposable
    {
        public string TempDirectory { get; } = Path.Combine(Path.GetTempPath(), nameof(FlexiIncludeBlocksIntegrationTests)); // Dummy file for creating dummy file streams

        public FlexiIncludeBlocksIntegrationTestsFixture()
        {
            TryDeleteDirectory();
            Directory.CreateDirectory(TempDirectory);
        }

        private void TryDeleteDirectory()
        {
            try
            {
                Directory.Delete(TempDirectory, true);
            }
            catch
            {
                // Do nothing
            }
        }

        public void Dispose()
        {
            TryDeleteDirectory();
        }
    }
}
