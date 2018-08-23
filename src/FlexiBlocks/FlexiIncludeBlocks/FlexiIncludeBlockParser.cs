﻿using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Jering.Markdig.Extensions.FlexiBlocks.FlexiIncludeBlocks
{
    public class FlexiIncludeBlockParser : BlockParser
    {
        private const string CLOSING_FLEXI_INCLUDE_BLOCKS_KEY = "closingFlexiIncludeBlocksKey";
        private static readonly StringSlice _codeBlockFence = new StringSlice("```");

        private readonly FlexiIncludeBlocksExtensionOptions _extensionOptions;
        private readonly IContentRetrievalService _contentRetrievalService;


        /// <summary>
        /// Creates a <see cref="FlexiIncludeBlockParser"/> instance.
        /// </summary>
        /// <param name="extensionOptionsAccessor"></param>
        /// <param name="contentRetrievalService"></param>
        public FlexiIncludeBlockParser(IOptions<FlexiIncludeBlocksExtensionOptions> extensionOptionsAccessor,
            IContentRetrievalService contentRetrievalService)
        {
            _extensionOptions = extensionOptionsAccessor?.Value ?? new FlexiIncludeBlocksExtensionOptions();
            _contentRetrievalService = contentRetrievalService;

            OpeningCharacters = new[] { '+' };
        }

        /// <summary>
        /// Opens a FlexiIncludeBlock if a line begins with "+{".
        /// </summary>
        /// <param name="processor"></param>
        /// <returns>
        /// <see cref="BlockState.None"/> if the current line has code indent or if the current line does not start with +{.
        /// <see cref="BlockState.Break"/> if the current line contains the entire JSON string.
        /// <see cref="BlockState.Continue"/> if the current line contains part of the JSON string.
        /// </returns>
        public override BlockState TryOpen(BlockProcessor processor)
        {
            if (processor.IsCodeIndent)
            {
                return BlockState.None;
            }

            // First line of a FlexiOptionsBlock must begin with +{
            if (processor.Line.PeekChar() != '{')
            {
                return BlockState.None;
            }

            // Dispose of + (BlockProcessor appends processor.Line to the new FlexiIncludeBlock, so it must start at the curly bracket)
            processor.Line.Start++;

            var flexiIncludeBlock = new FlexiIncludeBlock(this)
            {
                Column = processor.Column,
                Span = { Start = processor.Line.Start }
            };
            processor.NewBlocks.Push(flexiIncludeBlock);

            return TryContinue(processor, flexiIncludeBlock);
        }

        /// <summary>
        /// Determines whether or not the <see cref="FlexiIncludeBlock"/> is complete by checking whether all opening curly brackets have been closed. 
        /// The JSON spec allows for unescaped curly brackets within strings - https://www.json.org/, so this method ignores everything between unescaped quotes.
        /// 
        /// TODO This function can be improved - it does not verify that what has been read is valid JSON. Use JsonTextReader?
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="block"></param>
        /// <returns>
        /// <see cref="BlockState.Continue"/> if <paramref name="block"/> is still open.
        /// <see cref="BlockState.Break"/> if <paramref name="block"/> has ended and should be closed.
        /// </returns>
        public override BlockState TryContinue(BlockProcessor processor, Block block)
        {
            var flexiIncludeBlock = (FlexiIncludeBlock)block;

            StringSlice line = processor.Line;
            char pc = line.PeekCharExtra(-1);
            char c = line.CurrentChar;

            while (c != '\0')
            {
                if (!flexiIncludeBlock.EndsInString)
                {
                    if (c == '{')
                    {
                        flexiIncludeBlock.NumOpenBrackets++;
                    }
                    else if (c == '}')
                    {
                        if (--flexiIncludeBlock.NumOpenBrackets == 0)
                        {
                            flexiIncludeBlock.UpdateSpanEnd(line.End);

                            // End block
                            return BlockState.Break;
                        }
                    }
                    else if (pc != '\\' && c == '"')
                    {
                        flexiIncludeBlock.EndsInString = true;
                    }
                }
                else if (pc != '\\' && c == '"')
                {
                    flexiIncludeBlock.EndsInString = false;
                }

                pc = c;
                c = line.NextChar();
            }

            return BlockState.Continue;
        }

        public override bool Close(BlockProcessor processor, Block block)
        {
            var flexiIncludeBlock = (FlexiIncludeBlock)block;
            string json = flexiIncludeBlock.Lines.ToString();
            IncludeOptions includeOptions = JsonConvert.DeserializeObject<IncludeOptions>(json);

            // Check for cycles in includes
            Stack<FlexiIncludeBlock> closingFlexiIncludeBlocks = null;
            if (includeOptions.ContentType == ContentType.Markdown)
            {
                closingFlexiIncludeBlocks = processor.Document.GetData(CLOSING_FLEXI_INCLUDE_BLOCKS_KEY) as Stack<FlexiIncludeBlock>;
                if (closingFlexiIncludeBlocks == null)
                {
                    closingFlexiIncludeBlocks = new Stack<FlexiIncludeBlock>();
                    processor.Document.SetData(CLOSING_FLEXI_INCLUDE_BLOCKS_KEY, closingFlexiIncludeBlocks);
                }

                CheckForCyclesInIncludes(closingFlexiIncludeBlocks, flexiIncludeBlock, includeOptions);
            }

            // Retrieve content (read as lines since we will most probably only be using a subset of all the lines)
            ReadOnlyCollection<string> content = _contentRetrievalService.GetContent(includeOptions.Source,
                includeOptions.CacheOnDisk ? _extensionOptions.FileCacheDirectory : null,
                _extensionOptions.SourceBaseUri);

            // Convert content into blocks and replace flexiIncludeBlock with the newly created blocks
            ReplaceFlexiIncludeBlock(processor, flexiIncludeBlock, content, includeOptions);

            // Remove flexi include block from closing blocks once it has been processed
            closingFlexiIncludeBlocks?.Pop();

            // If true is returned, the block is kept as a child of its parent for rendering later on. If false is returned,
            // the block is discarded. We don't need the block any more.
            return false;
        }

        internal virtual void CheckForCyclesInIncludes(Stack<FlexiIncludeBlock> closingFlexiIncludeBlocks, FlexiIncludeBlock flexiIncludeBlock, IncludeOptions includeOptions)
        {
            flexiIncludeBlock.Source = includeOptions.Source;

            if (closingFlexiIncludeBlocks.Count > 0)
            {
                FlexiIncludeBlock parentFlexiIncludeBlock = closingFlexiIncludeBlocks.Peek();
                flexiIncludeBlock.ContainingSource = parentFlexiIncludeBlock.Source;
                flexiIncludeBlock.LineNumberInContainingSource = parentFlexiIncludeBlock.LineNumberOfLastProcessedLineInSource - flexiIncludeBlock.Lines.Count + 1;
            }
            else
            {
                // Root source, line number is always line index + 1
                flexiIncludeBlock.LineNumberInContainingSource = flexiIncludeBlock.Line + 1;
            }

            for (int i = closingFlexiIncludeBlocks.Count - 1; i > -1; i--)
            {
                FlexiIncludeBlock closingFlexiIncludeBlock = closingFlexiIncludeBlocks.ElementAt(i);

                if (closingFlexiIncludeBlock.ContainingSource == flexiIncludeBlock.ContainingSource &&
                    closingFlexiIncludeBlock.LineNumberInContainingSource == flexiIncludeBlock.LineNumberInContainingSource)
                {
                    // Create string describing cycle
                    string cycleDescription = "";
                    for (; i > -1; i--)
                    {
                        FlexiIncludeBlock cycleFlexiIncludeBlock = closingFlexiIncludeBlocks.ElementAt(i);
                        cycleDescription += $"Source: {cycleFlexiIncludeBlock.ContainingSource}, Line: {cycleFlexiIncludeBlock.LineNumberInContainingSource} >\n";
                    }
                    cycleDescription += $"Source: {closingFlexiIncludeBlock.ContainingSource}, Line: {closingFlexiIncludeBlock.LineNumberInContainingSource}";

                    throw new InvalidOperationException(string.Format(Strings.InvalidOperationException_CycleInIncludes, cycleDescription));
                }
            }

            closingFlexiIncludeBlocks.Push(flexiIncludeBlock);
        }

        internal virtual void ProcessText(BlockProcessor processor, string text)
        {
            if (text?.Length == 0) // If text is an empty string, LineReader.ReadLine immediately returns null
            {
                processor.ProcessLine(new StringSlice(text));

                return;
            }

            var lineReader = new LineReader(text);
            while (true)
            {
                // Get the precise position of the begining of the line
                StringSlice? lineText = lineReader.ReadLine();

                // If this is the end of file and the last line is empty
                if (lineText == null)
                {
                    break;
                }
                processor.ProcessLine(lineText.Value);
            }
        }

        internal virtual void DedentAndCollapseLeadingWhiteSpace(ref StringSlice line, int dedentLength, float collapseRatio)
        {
            if (line.Text.Length == 0)
            {
                return;
            }

            line.Start = 0;

            // Dedent
            if (dedentLength > 0)
            {
                for (int start = 0; start < dedentLength; start++)
                {
                    if (!line.PeekChar(start).IsWhitespace())
                    {
                        line.Start = start;
                        return; // No more white space to dedent or collapse
                    }
                }

                line.Start = dedentLength;
            }

            // Collapse
            if (collapseRatio == 0)
            {
                line.TrimStart(); // Remove all leading white space
            }
            else if (collapseRatio < 1) // If collapse ratio is 1, do nothing
            {
                int leadingWhiteSpaceCount = 0;
                while (line.PeekChar(leadingWhiteSpaceCount).IsWhitespace())
                {
                    leadingWhiteSpaceCount++;
                }

                if (leadingWhiteSpaceCount == 0)
                {
                    return;
                }

                // collapseRatio is defined as finalLeadingWhiteSpaceCount/initialLeadingWhiteSpaceCount,
                // so collapseLength = initialLeadingWhiteSpaceCount - finalLeadingWhiteSpaceCount = initialLeadingWhiteSpaceCount - initialLeadingWhiteSpaceCount*collapseRatio
                int collapseLength = leadingWhiteSpaceCount - (int)Math.Round(leadingWhiteSpaceCount * collapseRatio);

                for (int start = 0; start < collapseLength; start++)
                {
                    line.NextChar();
                }
            }
        }

        /// <summary>
        /// Processes <paramref name="flexiIncludeBlock"/>'s contents, replacing the flexi include block with the results.
        /// The method used here is also used by GridTable, basically, a child BlockProcessor is used to avoid conflicts with existing 
        /// open blocks in <paramref name="processor"/>.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="flexiIncludeBlock"></param>
        /// <param name="content"></param>
        /// <param name="includeOptions"></param>
        internal virtual void ReplaceFlexiIncludeBlock(BlockProcessor processor,
            FlexiIncludeBlock flexiIncludeBlock,
            ReadOnlyCollection<string> content,
            IncludeOptions includeOptions)
        {
            ContainerBlock parent = flexiIncludeBlock.Parent;
            
            // Remove the flexi include block
            parent.Remove(flexiIncludeBlock);

            BlockProcessor childProcessor = processor.CreateChild();
            childProcessor.Open(parent);

            // TODO what else is line index used for? 
            // TODO printing of errors when in a child processor, line numbers etc
            // MarkdownObject.Line is the line that the block starts at, it is set by BlockProcessor.ProcessNewBlocks. We need to set 
            // LineIndex to the line that the include block starts at for FlexiOptionsBlocks to work.
            childProcessor.LineIndex = flexiIncludeBlock.Line;

            // Clip content
            if (includeOptions.ContentType != ContentType.Markdown) // If content is code, start with ```
            {
                childProcessor.ProcessLine(_codeBlockFence);
            }

            // Clipping need not be sequential, they can also overlap
            foreach (Clipping clipping in includeOptions.Clippings)
            {
                if (clipping.BeforeText != null)
                {
                    ProcessText(childProcessor, clipping.BeforeText);
                }

                int startLineNumber = -1;
                if (clipping.StartDemarcationLineSubstring != null)
                {
                    for (int i = 0; i < content.Count - 1; i++) // Since demarcation lines are not included in the clipping, the last line cannot be a start demarcation line.
                    {
                        if (content[i].Contains(clipping.StartDemarcationLineSubstring))
                        {
                            startLineNumber = i + 2;
                            break;
                        }
                    }

                    if (startLineNumber == -1)
                    {
                        throw new InvalidOperationException(string.Format(Strings.InvalidOperationException_InvalidClippingNoLineContainsStartLineSubstring, clipping.StartDemarcationLineSubstring));
                    }
                }
                else
                {
                    startLineNumber = clipping.StartLineNumber;
                }

                for (int lineNumber = startLineNumber; lineNumber <= content.Count; lineNumber++)
                {
                    string line = content[lineNumber - 1];
                    var stringSlice = new StringSlice(line);

                    DedentAndCollapseLeadingWhiteSpace(ref stringSlice, clipping.DedentLength, clipping.CollapseRatio);

                    // TODO document, -1 by default to prevent issues with before and after?
                    flexiIncludeBlock.LineNumberOfLastProcessedLineInSource = lineNumber;
                    childProcessor.ProcessLine(stringSlice);

                    // Check whether we've reached the end of the clipping
                    if (clipping.EndDemarcationLineSubstring != null)
                    {
                        if (lineNumber == content.Count)
                        {
                            throw new InvalidOperationException(string.Format(Strings.InvalidOperationException_InvalidClippingNoLineContainsEndLineSubstring, clipping.EndDemarcationLineSubstring));
                        }

                        // Check if next line contains the end line substring
                        if (content[lineNumber].Contains(clipping.EndDemarcationLineSubstring))
                        {
                            break;
                        }
                    }
                    else if (lineNumber == clipping.EndLineNumber)
                    {
                        break;
                    }
                }

                if (clipping.AfterText != null)
                {
                    ProcessText(childProcessor, clipping.AfterText);
                }
            }

            if (includeOptions.ContentType != ContentType.Markdown) // If content is code, end with ```
            {
                childProcessor.ProcessLine(_codeBlockFence);
            }

            // Ensure that the last replacement block has been closed. While the block never makes it to the OpenedBlocks collection in the root processor, 
            // calling Close for it ensures that it and its children's Close methods and events get called.
            childProcessor.Close(parent.LastChild);

            // BlockProcessors are pooled. Once we're done with innerProcessor, we must release it. This also removes all references to
            // tempContainerBlock, which should allow it to be collected quickly.
            childProcessor.ReleaseChild();
        }
    }
}
