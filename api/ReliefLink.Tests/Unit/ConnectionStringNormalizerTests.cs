using Npgsql;
using ReliefLink.Api.Data;

namespace ReliefLink.Tests.Unit;

public class ConnectionStringNormalizerTests
{
    [Fact]
    public void A_neon_style_uri_becomes_an_npgsql_connection_string()
    {
        var result = ConnectionStringNormalizer.Normalize("postgresql://app_user:s3cret@ep-cool-123.ap-southeast-1.aws.neon.tech/relieflink?sslmode=require");
        var b = new NpgsqlConnectionStringBuilder(result);

        Assert.Equal("ep-cool-123.ap-southeast-1.aws.neon.tech", b.Host);
        Assert.Equal(5432, b.Port);
        Assert.Equal("relieflink", b.Database);
        Assert.Equal("app_user", b.Username);
        Assert.Equal("s3cret", b.Password);
        Assert.Equal(SslMode.Require, b.SslMode);
    }

    [Fact]
    public void The_postgres_scheme_works_too_and_a_port_is_kept()
    {
        var b = new NpgsqlConnectionStringBuilder(ConnectionStringNormalizer.Normalize("postgres://u:p@db.example.com:6543/mydb"));

        Assert.Equal("db.example.com", b.Host);
        Assert.Equal(6543, b.Port);
        Assert.Equal("mydb", b.Database);
    }

    [Fact]
    public void Special_characters_in_the_password_survive_percent_encoding()
    {
        var b = new NpgsqlConnectionStringBuilder(ConnectionStringNormalizer.Normalize("postgresql://u:p%40ss%3Bw%3Dord%22x@h.example.com/db"));

        Assert.Equal("p@ss;w=ord\"x", b.Password);
        Assert.Equal("h.example.com", b.Host);
    }

    [Fact]
    public void With_no_sslmode_it_encrypts_by_default()
    {
        var b = new NpgsqlConnectionStringBuilder(ConnectionStringNormalizer.Normalize("postgresql://u:p@h.example.com/db"));

        Assert.Equal(SslMode.Require, b.SslMode);
    }

    [Theory]
    [InlineData("disable", SslMode.Disable)]
    [InlineData("prefer", SslMode.Prefer)]
    [InlineData("require", SslMode.Require)]
    [InlineData("verify-full", SslMode.VerifyFull)]
    [InlineData("verify-ca", SslMode.VerifyCA)]
    public void An_sslmode_in_the_uri_is_honoured(string given, SslMode expected)
    {
        var b = new NpgsqlConnectionStringBuilder(ConnectionStringNormalizer.Normalize($"postgresql://u:p@h.example.com/db?sslmode={given}"));

        Assert.Equal(expected, b.SslMode);
    }

    [Fact]
    public void Other_query_parameters_such_as_channel_binding_are_ignored()
    {
        var b = new NpgsqlConnectionStringBuilder(ConnectionStringNormalizer.Normalize("postgresql://u:p@h.example.com/db?sslmode=require&channel_binding=require"));

        Assert.Equal("db", b.Database);
    }

    [Fact]
    public void A_string_already_in_npgsql_form_is_not_touched()
    {
        const string original = "Host=localhost;Port=5432;Database=relieflink;Username=postgres;Password=abc";

        Assert.Equal(original, ConnectionStringNormalizer.Normalize(original));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_stays_nothing(string? value) => Assert.Equal(value, ConnectionStringNormalizer.Normalize(value));

    [Fact]
    public void A_broken_uri_gives_a_message_a_person_can_act_on()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => ConnectionStringNormalizer.Normalize("postgresql://"));

        Assert.Contains("could not be read", ex.Message);
    }

    [Fact]
    public void Surrounding_spaces_from_a_copy_and_paste_are_forgiven()
    {
        var b = new NpgsqlConnectionStringBuilder(ConnectionStringNormalizer.Normalize("  postgresql://u:p@h.example.com/db  "));

        Assert.Equal("h.example.com", b.Host);
    }
}
