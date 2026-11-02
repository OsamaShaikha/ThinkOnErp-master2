using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Application.Common.Converters;

/// <summary>
/// JSON converter for WarehouseType (as integer in APIs, supporting integer numbers or legacy strings).
/// </summary>
public class WarehouseTypeIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str))
                return (int)WarehouseType.Main;

            if (int.TryParse(str, out var num))
                return num;

            return str.ToUpperInvariant() switch
            {
                "MAIN" => (int)WarehouseType.Main,
                "BRANCH" => (int)WarehouseType.Branch,
                "TRANSIT" => (int)WarehouseType.Transit,
                "VIRTUAL" => (int)WarehouseType.Virtual,
                _ => (int)WarehouseType.Main
            };
        }

        return (int)WarehouseType.Main;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

/// <summary>
/// JSON converter for nullable WarehouseType.
/// </summary>
public class NullableWarehouseTypeIntConverter : JsonConverter<int?>
{
    private static readonly WarehouseTypeIntConverter _inner = new();

    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String && string.IsNullOrWhiteSpace(reader.GetString()))
            return null;

        return _inner.Read(ref reader, typeof(int), options);
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}

/// <summary>
/// JSON converter for ZoneType (as integer in APIs, supporting integer numbers or legacy strings).
/// </summary>
public class ZoneTypeIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str))
                return (int)ZoneType.Storage;

            if (int.TryParse(str, out var num))
                return num;

            return str.ToUpperInvariant() switch
            {
                "STORAGE" => (int)ZoneType.Storage,
                "RECEIVING" => (int)ZoneType.Receiving,
                "STAGING" => (int)ZoneType.Staging,
                "QUARANTINE" => (int)ZoneType.Quarantine,
                "DAMAGED" => (int)ZoneType.Damaged,
                "RETURNS" => (int)ZoneType.Returns,
                _ => (int)ZoneType.Storage
            };
        }

        return (int)ZoneType.Storage;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

/// <summary>
/// JSON converter for nullable ZoneType.
/// </summary>
public class NullableZoneTypeIntConverter : JsonConverter<int?>
{
    private static readonly ZoneTypeIntConverter _inner = new();

    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String && string.IsNullOrWhiteSpace(reader.GetString()))
            return null;

        return _inner.Read(ref reader, typeof(int), options);
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
            writer.WriteNumberValue(value.Value);
        else
            writer.WriteNullValue();
    }
}
