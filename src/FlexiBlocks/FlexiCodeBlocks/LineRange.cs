﻿using Newtonsoft.Json;
using System;

namespace Jering.Markdig.Extensions.FlexiBlocks.FlexiCodeBlocks
{
    /// <summary>
    /// Represents a range of lines.
    /// </summary>
    public class LineRange
    {
        /// <summary>
        /// Creates a <see cref="LineRange"/> instance.
        /// </summary>
        /// <param name="startLineNumber">
        /// <para>Start line number of this range.</para>
        /// <para>This value must be greater than 0.</para>
        /// <para>Defaults to 1.</para>
        /// </param>
        /// <param name="endLineNumber">
        /// <para>End line number of this range.</para>
        /// <para>If this value is -1 the range that extends to infinity. If it is not -1, it must be greater than or equal to <paramref name="startLineNumber"/></para>
        /// <para>Defaults to -1.</para>
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="startLineNumber"/> is less than 1.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="endLineNumber"/> is not -1 and is less than <paramref name="startLineNumber"/>.</exception>
        public LineRange(int startLineNumber = 1, int endLineNumber = -1)
        {
            if(startLineNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(startLineNumber),
                    string.Format(Strings.ArgumentOutOfRangeException_LineNumberMustBeGreaterThan0, startLineNumber));
            }

            if(endLineNumber != -1 && endLineNumber < startLineNumber)
            {
                throw new ArgumentOutOfRangeException(nameof(endLineNumber),
                    string.Format(Strings.ArgumentOutOfRangeException_EndLineNumberMustBeMinus1OrGreaterThanOrEqualToStartLineNumber, endLineNumber, startLineNumber));
            }

            StartLineNumber = startLineNumber;
            EndLineNumber = endLineNumber;
        }

        /// <summary>
        /// Gets the start line number of this range.
        /// </summary>
        [JsonProperty]
        public int StartLineNumber { get; }

        /// <summary>
        /// Gets the end line number of this range.
        /// </summary>
        [JsonProperty]
        public int EndLineNumber { get; }

        /// <summary>
        /// Gets the number of lines in this range.
        /// </summary>
        public int NumLines => EndLineNumber == -1 ? -1 : EndLineNumber - StartLineNumber + 1;

        /// <summary>
        /// Checks whether <paramref name="lineNumber"/> is within this range.
        /// </summary>
        /// <param name="lineNumber">The line number to check.</param>
        /// <returns>True if <paramref name="lineNumber"/> is within this range, otherwise false.</returns>
        public bool Contains(int lineNumber)
        {
            return lineNumber >= StartLineNumber && (EndLineNumber == -1 || lineNumber <= EndLineNumber);
        }

        /// <summary>
        /// Checks whether this range occurs before <paramref name="lineNumber"/>.
        /// </summary>
        /// <param name="lineNumber">The line number to check.</param>
        /// <returns>True if this range occurs before <paramref name="lineNumber"/>, otherwise false.</returns>
        public bool Before(int lineNumber)
        {
            return EndLineNumber != -1 && lineNumber > EndLineNumber;
        }

        /// <summary>
        /// Returns the string representation of this instance.
        /// </summary>
        public override string ToString()
        {
            return $"[{StartLineNumber}, {EndLineNumber}]";
        }
    }
}
