using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Core.Services.Persistence
{
    /// <summary>
    /// Custom System.Text.Json converter for the Investigation domain aggregate.
    /// Serializes Investigation → InvestigationPersistenceDto (captures all data).
    /// Deserializes InvestigationPersistenceDto → Investigation (reconstructs domain object).
    /// This converter is registered on the JsonSerializerOptions used by FileWorkspacePersistence.
    /// </summary>
    public sealed class InvestigationJsonConverter : JsonConverter<Investigation>
    {
        public override Investigation? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Deserialize the JSON into the DTO, then map to the domain model.
            // Use separate options without this converter to avoid infinite recursion.
            try
            {
                var dtoOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var dto = JsonSerializer.Deserialize<InvestigationPersistenceDto>(ref reader, dtoOptions);
                if (dto == null) return null;
                return InvestigationPersistenceMapper.FromDto(dto);
            }
            catch
            {
                // If deserialization or mapping fails (e.g., old format), return null
                // rather than crashing the workspace load.
                return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, Investigation value, JsonSerializerOptions options)
        {
            // Map the domain model to DTO, then serialize the DTO
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            var dto = InvestigationPersistenceMapper.ToDto(value);
            JsonSerializer.Serialize(writer, dto, options);
        }
    }
}
