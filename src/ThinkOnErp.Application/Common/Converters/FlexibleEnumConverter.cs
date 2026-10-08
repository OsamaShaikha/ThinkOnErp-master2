using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThinkOnErp.Application.Common.Converters;

/// <summary>
/// Flexible System.Text.Json converter that allows deserializing enums from:
/// 1) Numeric integer values (e.g. 1, 2)
/// 2) Numeric strings (e.g. "1", "2")
/// 3) Case-insensitive enum member names (e.g. "DineIn", "dinein")
/// Serializes to the standard enum name string.
/// </summary>
public class FlexibleEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var intValue))
            {
                return (T)Enum.ToObject(typeof(T), intValue);
            }
            if (reader.TryGetInt64(out var longValue))
            {
                return (T)Enum.ToObject(typeof(T), longValue);
            }
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return default;
            }

            if (int.TryParse(stringValue, out var parsedInt))
            {
                return (T)Enum.ToObject(typeof(T), parsedInt);
            }

            if (Enum.TryParse<T>(stringValue, ignoreCase: true, out var parsedEnum))
            {
                return parsedEnum;
            }

            throw new JsonException($"Unable to convert '{stringValue}' to enum {typeof(T).Name}.");
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} when parsing enum {typeof(T).Name}.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Flexible converter for nullable enums.
/// </summary>
public class FlexibleNullableEnumConverter<T> : JsonConverter<T?> where T : struct, Enum
{
    private readonly FlexibleEnumConverter<T> _underlying = new();

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String && string.IsNullOrWhiteSpace(reader.GetString()))
        {
            return null;
        }

        return _underlying.Read(ref reader, typeof(T), options);
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            _underlying.Write(writer, value.Value, options);
        }
    }
}

/// <summary>
/// Factory that applies FlexibleEnumConverter to any enum or nullable enum type.
/// </summary>
public class FlexibleEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum || (Nullable.GetUnderlyingType(typeToConvert)?.IsEnum == true);
    }

    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert.IsEnum)
        {
            var converterType = typeof(FlexibleEnumConverter<>).MakeGenericType(typeToConvert);
            return (JsonConverter?)Activator.CreateInstance(converterType);
        }

        var underlying = Nullable.GetUnderlyingType(typeToConvert);
        if (underlying != null && underlying.IsEnum)
        {
            var converterType = typeof(FlexibleNullableEnumConverter<>).MakeGenericType(underlying);
            return (JsonConverter?)Activator.CreateInstance(converterType);
        }

        return null;
    }
}
