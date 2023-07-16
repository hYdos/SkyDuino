using System.Text.Json;
using System.Text.Json.Serialization;

namespace CLI.nfc; 

public class NfcTagSerializer : JsonConverter<NfcTag> {

    public override NfcTag Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, NfcTag value, JsonSerializerOptions options) {
        writer.WriteString("uid", BitConverter.ToString(value.Uid));
        writer.WriteStartArray("keys");
        // ...
        writer.WriteEndArray();
        throw new NotImplementedException();
    }
}