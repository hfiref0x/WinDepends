/*******************************************************************************
*
*  (C) COPYRIGHT AUTHORS, 2025 - 2026
*
*  TITLE:       CCORECLIENT.SERIALIZATION.CS
*
*  VERSION:     1.00
*
*  DATE:        14 Jul 2026
*  
*  Data serialization for Core Server communication class.
*
* THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
* ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
* TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
* PARTICULAR PURPOSE.
*
*******************************************************************************/
using System.Runtime.Serialization.Json;
using System.Text;

namespace WinDepends;

public partial class CCoreClient
{
    /// <summary>
    /// Gets a cached JSON serializer for the specified type.
    /// </summary>
    /// <param name="objectType">The type to get a serializer for.</param>
    /// <returns>A <see cref="DataContractJsonSerializer"/> for the specified type.</returns>
    private DataContractJsonSerializer GetSerializerForType(Type objectType)
    {
        if (_serializerCache.TryGetValue(objectType, out var serializer))
            return serializer;

        // Fallback for unknown types
        return new DataContractJsonSerializer(objectType);
    }

    /// <summary>
    /// Deserializes JSON data into an object of the specified type.
    /// </summary>
    /// <param name="FileName">The filename for error reporting.</param>
    /// <param name="objectType">The type to deserialize into.</param>
    /// <param name="data">The JSON data string.</param>
    /// <returns>The deserialized object, or null if deserialization fails.</returns>
    object DeserializeDataJSON(string FileName, Type objectType, string data)
    {
        if (string.IsNullOrEmpty(data))
            return null;

        try
        {
            // Try to find pre-created serializer
            DataContractJsonSerializer serializer = GetSerializerForType(objectType);
            using MemoryStream ms = new(Encoding.Unicode.GetBytes(data));
            return serializer.ReadObject(ms);
        }
        catch (Exception ex)
        {
            _addLogMessage($"Data deserialization failed: {ex.Message}", LogMessageType.ErrorOrWarning);
            _addLogMessage($"Failed to analyze {FileName}", LogMessageType.ErrorOrWarning);
            return null;
        }
    }

    /// <summary>
    /// Deserializes JSON data into an object of the specified type.
    /// </summary>
    /// <param name="objectType">The type to deserialize into.</param>
    /// <param name="data">The JSON data string.</param>
    /// <returns>The deserialized object, or null if deserialization fails.</returns>
    object DeserializeDataJSON(Type objectType, string data)
    {
        if (string.IsNullOrEmpty(data))
            return null;

        try
        {
            // Try to find pre-created serializer
            DataContractJsonSerializer serializer = GetSerializerForType(objectType);
            using MemoryStream ms = new(Encoding.Unicode.GetBytes(data));
            return serializer.ReadObject(ms);
        }
        catch (Exception ex)
        {
            _addLogMessage($"Data deserialization failed: {ex.Message}", LogMessageType.ErrorOrWarning);
            return null;
        }
    }
}
