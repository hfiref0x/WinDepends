/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2024 - 2026
*
*  TITLE:       CBUFFERCHAIN.CS
*
*  VERSION:     1.00
*
*  DATE:        23 May 2026
*  
*  Linked receive buffer chain for variable-length server responses.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Text;

namespace WinDepends;

/// <summary>
/// Represents a linked chain of character buffers used for receiving data from the server.
/// </summary>
/// <remarks>
/// This class implements a simple linked list of character arrays to handle variable-length
/// server responses without requiring pre-allocation of large buffers.
/// </remarks>
public class CBufferChain // keep as simple as possible
{
    private CBufferChain _next;

    /// <summary>
    /// Gets or sets the number of characters stored in this buffer node.
    /// </summary>
    public uint DataSize;

    /// <summary>
    /// Gets or sets the character array containing the data.
    /// </summary>
    public char[] Data;

    /// <summary>
    /// Gets or sets the next buffer node in the chain.
    /// </summary>
    public CBufferChain Next { get => _next; set => _next = value; }

    public CBufferChain()
    {
        Data = new char[CConsts.CoreServerChainSizeMax];
    }

    /// <summary>
    /// Concatenates all nodes in this buffer chain into a single string, 
    /// trimming trailing nulls per node and skipping carriage-return and line-feed characters.
    /// </summary>
    /// <returns>The concatenated content without CR/LF characters.</returns>
    public string BufferToStringNoCRLF()
    {
        int estimatedLength = 0;
        var chain = this;

        // First pass: calculate total length
        do
        {
            if (chain.Data is { Length: > 0 })
            {
                int length = chain.Data.Length;
                while (length > 0 && chain.Data[length - 1] == '\0')
                    length--;
                estimatedLength += length;
            }
            chain = chain.Next;
        } while (chain != null);

        var sb = new StringBuilder(estimatedLength > 0 ? estimatedLength : 256);
        chain = this;

        // Second pass: build string
        do
        {
            if (chain.Data is { Length: > 0 } data)
            {
                // Find last non-null character index
                int length = data.Length;
                while (length > 0 && data[length - 1] == '\0')
                    length--;

                // Process characters
                for (int i = 0; i < length; i++)
                {
                    char c = data[i];
                    if (c is not ('\n' or '\r'))
                        sb.Append(c);
                }
            }
            chain = chain.Next;
        } while (chain != null);

        return sb.ToString();
    }
}
