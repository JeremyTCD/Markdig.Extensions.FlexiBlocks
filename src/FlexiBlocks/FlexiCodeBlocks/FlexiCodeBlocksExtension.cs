﻿using Jering.Markdig.Extensions.FlexiBlocks.FlexiOptionsBlocks;
using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Microsoft.Extensions.Options;
using System;
using System.Linq;

namespace Jering.Markdig.Extensions.FlexiBlocks.FlexiCodeBlocks
{
    /// <summary>
    /// <para>A markdig extension for FlexiCodeBlocks.</para>
    /// 
    /// <para>This extension uses the default <see cref="CodeBlock"/> parsers. What makes the generated <see cref="CodeBlock"/>s "FlexiCodeBlocks" is the
    /// addition of <see cref="FlexiCodeBlockOptions"/> to them.</para>
    /// </summary>
    public class FlexiCodeBlocksExtension : FlexiBlocksExtension
    {
        private readonly IFlexiOptionsBlockService _flexiOptionsBlockService;
        private readonly FlexiCodeBlocksExtensionOptions _extensionOptions;
        private readonly FlexiCodeBlockRenderer _flexiCodeBlockRenderer;

        /// <summary>
        /// The key used for storing <see cref="FlexiCodeBlockOptions"/>.
        /// </summary>
        public const string FLEXI_CODE_BLOCK_OPTIONS_KEY = "flexiCodeBlockOptions";

        /// <summary>
        /// Creates a <see cref="FlexiCodeBlocksExtension"/> instance.
        /// </summary>
        /// <param name="flexiCodeBlockRenderer">The renderer for rendering FlexiCodeBlocks as HTML.</param>
        /// <param name="extensionOptionsAccessor">The accessor for <see cref="FlexiCodeBlocksExtensionOptions"/>.</param>
        /// <param name="flexiOptionsBlockService">The service that will handle populating of <see cref="FlexiCodeBlockOptions"/>.</param>
        public FlexiCodeBlocksExtension(FlexiCodeBlockRenderer flexiCodeBlockRenderer,
            IOptions<FlexiCodeBlocksExtensionOptions> extensionOptionsAccessor,
            IFlexiOptionsBlockService flexiOptionsBlockService)
        {
            _flexiCodeBlockRenderer = flexiCodeBlockRenderer ?? throw new ArgumentNullException(nameof(flexiCodeBlockRenderer));
            _extensionOptions = extensionOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(extensionOptionsAccessor));
            _flexiOptionsBlockService = flexiOptionsBlockService ?? throw new ArgumentNullException(nameof(flexiOptionsBlockService));
        }

        /// <summary>
        /// Registers <see cref="CodeBlock"/> parsers if they are't already registered.
        /// </summary>
        /// <param name="pipeline">The pipeline builder to register the parsers for.</param>
        public override void Setup(MarkdownPipelineBuilder pipeline)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            // FencedCodeBlockParser and IndentedCodeBlockParser are default parsers registered in MarkdownPipelineBuilder's constructor.
            FencedCodeBlockParser fencedCodeBlockParser = pipeline.BlockParsers.Find<FencedCodeBlockParser>();
            if (fencedCodeBlockParser != null)
            {
                fencedCodeBlockParser.Closed += OnClosed;
            }

            IndentedCodeBlockParser indentedCodeBlockParser = pipeline.BlockParsers.Find<IndentedCodeBlockParser>();
            if (indentedCodeBlockParser != null)
            {
                indentedCodeBlockParser.Closed += OnClosed;
            }
        }

        /// <summary>
        /// Registers a <see cref="FlexiCodeBlockRenderer"/> if one isn't already registered.
        /// </summary>
        /// <param name="pipeline">Unused.</param>
        /// <param name="renderer">The root renderer to register the renderer for.</param>
        public override void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            if (renderer is HtmlRenderer htmlRenderer)
            {
                if (!htmlRenderer.ObjectRenderers.Contains<FlexiCodeBlockRenderer>())
                {
                    htmlRenderer.ObjectRenderers.Insert(0, _flexiCodeBlockRenderer);
                }

                CodeBlockRenderer codeBlockRenderer = htmlRenderer.ObjectRenderers.Find<CodeBlockRenderer>();
                if (codeBlockRenderer != null)
                {
                    htmlRenderer.ObjectRenderers.Remove(codeBlockRenderer);
                }
            }
        }

        /// <summary>
        /// Called when a FlexiCodeBlock is closed. Creates and validates the <see cref="FlexiCodeBlockOptions"/> for the block.
        /// </summary>
        /// <param name="processor">The block processor for the FlexiCodeBlock that has been closed.</param>
        /// <param name="block">The FlexiCodeBlock that has been closed.</param>
        /// <exception cref="FlexiBlocksException">Thrown if highlight line ranges are not a subset of the full range of lines.</exception>
        /// <exception cref="FlexiBlocksException">Thrown if line number line ranges are not a subset of the full range of lines.</exception>
        protected override void OnFlexiBlockClosed(BlockProcessor processor, Block block)
        {
            if(block == null)
            {
                throw new ArgumentNullException(nameof(block));
            }

            FlexiCodeBlockOptions flexiCodeBlockOptions = _extensionOptions.DefaultBlockOptions.Clone();

            // Apply FlexiOptionsBlock options if they exist
            _flexiOptionsBlockService.TryPopulateOptions(processor, flexiCodeBlockOptions, block.Line);

            // Validate line ranges
            int numLines = ((LeafBlock)block).Lines.Count;
            if (flexiCodeBlockOptions.HighlightLineRanges?.Count > 0)
            {
                ValidateLineRange(flexiCodeBlockOptions.HighlightLineRanges.Last(), numLines, nameof(FlexiCodeBlockOptions.HighlightLineRanges));
            }

            if (flexiCodeBlockOptions.LineNumberLineRanges?.Count > 0)
            {
                ValidateLineRange(flexiCodeBlockOptions.LineNumberLineRanges.Last().LineRange, numLines, nameof(FlexiCodeBlockOptions.LineNumberLineRanges));
            }

            block.SetData(FLEXI_CODE_BLOCK_OPTIONS_KEY, flexiCodeBlockOptions);
        }

        internal virtual void ValidateLineRange(LineRange lineRange, int numLines, string propertyName)
        {
            // Line ranges must be a subset of the full range of lines.
            if (lineRange.StartLineNumber > numLines || lineRange.EndLineNumber > numLines)
            {
                throw new FlexiBlocksException(string.Format(Strings.FlexiBlocksException_OptionLineRangeNotASubset, lineRange.ToString(), propertyName, numLines));
            }
        }
    }
}
