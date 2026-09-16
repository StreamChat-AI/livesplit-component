using System.Collections.Generic;
using StreamChatAI.LiveSplit.Core;
using Xunit;

namespace StreamChatAI.LiveSplit.Tests
{
    public class JsonTests
    {
        [Fact]
        public void Serializes_nested_payloads()
        {
            var json = Json.Serialize(new Dictionary<string, object>
            {
                ["event"] = "split",
                ["n"] = 3,
                ["ms"] = -12_400L,
                ["gold"] = true,
                ["none"] = null,
                ["run"] = new Dictionary<string, object> { ["game"] = "Super \"Mario\" 64\\\n" },
            });

            Assert.Equal("{\"event\":\"split\",\"n\":3,\"ms\":-12400,\"gold\":true,\"none\":null,\"run\":{\"game\":\"Super \\\"Mario\\\" 64\\\\\\n\"}}", json);
        }

        [Fact]
        public void Reads_the_flat_fields_of_a_response_and_skips_nested_ones()
        {
            var fields = Json.ParseFlatObject("{\"status\":\"connected\",\"token\":\"lsc_a\\\"b\",\"errors\":{\"x\":[1,\"}\"]},\"expires_in\": 600, \"missing\": null}");

            Assert.Equal("connected", fields["status"]);
            Assert.Equal("lsc_a\"b", fields["token"]);
            Assert.Equal("600", fields["expires_in"]);
            Assert.False(fields.ContainsKey("errors"));
            Assert.False(fields.ContainsKey("missing"));
        }

        [Fact]
        public void Garbage_is_an_empty_result_not_an_exception()
        {
            Assert.Empty(Json.ParseFlatObject("<html>502 Bad Gateway</html>"));
            Assert.Empty(Json.ParseFlatObject(""));
        }
    }
}
